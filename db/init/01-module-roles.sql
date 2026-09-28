-- Per-module database roles: the database itself enforces which module owns which data.
-- See docs/data-ownership.md, section 5.
--
-- Run as a superuser, connected to the application database (a dedicated database: every
-- non-extension table and function in its public schema is treated as this application's).
-- On a new database run it BEFORE the first `dotnet run -- migrate`. Idempotent: safe to re-run,
-- and safe on a database migrated before roles existed, in either order relative to `migrate`
-- (it takes over ownership of the existing objects and grants on them).
--
-- Creates:
--   crowdfunding_migrator   owns every module schema and object; runs migrations (DDL). The
--                           application never connects with it at run time.
--   <schema>_app            one login role per module: USAGE on its own schema, SELECT/INSERT/
--                           UPDATE/DELETE on its tables, USAGE/SELECT on its sequences. No CREATE,
--                           no TRUNCATE, no TRIGGER, no ownership: it cannot run DDL, disable the
--                           ledger triggers or touch another module's schema.
--   audit_app               SELECT/INSERT on the shared system.audit_records trail only.
--
-- Passwords are NOT set here. Development sets them in 02-dev-passwords.sql; every other
-- environment sets them from its secret store (ALTER ROLE ... PASSWORD ...).

DO $roles$
DECLARE
    owned_schema text;
    app_role     text;
    privileges   text;
    object_name  text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'crowdfunding_migrator') THEN
        CREATE ROLE crowdfunding_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
    END IF;

    -- EF's EnsureSchema issues CREATE SCHEMA IF NOT EXISTS, which checks CREATE on the database.
    EXECUTE format('GRANT CONNECT, CREATE ON DATABASE %I TO crowdfunding_migrator', current_database());

    -- Nobody but the migrator creates objects. The migrator also needs public: the earliest
    -- migrations create tables there and MoveTablesToModuleSchema then moves them into their
    -- module's schema.
    REVOKE CREATE ON SCHEMA public FROM PUBLIC;
    GRANT USAGE, CREATE ON SCHEMA public TO crowdfunding_migrator;

    -- A database migrated before module schemas existed keeps the module tables and the ledger's
    -- trigger functions in public until MoveTablesToModuleSchema runs; that migration, run as the
    -- migrator, must own them. Extension objects are left alone.
    FOR object_name IN
        SELECT format('%I.%I', n.nspname, c.relname)
        FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p')
          AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = c.oid AND d.deptype = 'e')
    LOOP
        EXECUTE format('ALTER TABLE %s OWNER TO crowdfunding_migrator', object_name);
    END LOOP;

    FOR object_name IN
        SELECT p.oid::regprocedure::text
        FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'public'
          AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = p.oid AND d.deptype = 'e')
    LOOP
        EXECUTE format('ALTER FUNCTION %s OWNER TO crowdfunding_migrator', object_name);
    END LOOP;

    FOREACH owned_schema IN ARRAY ARRAY[
        'campaigns', 'contributions', 'identity', 'moderation', 'notifications', 'campaignupdates', 'system']
    LOOP
        app_role   := CASE owned_schema WHEN 'system' THEN 'audit_app' ELSE owned_schema || '_app' END;
        -- The audit trail is append-only: its writer may insert and read back, never change.
        privileges := CASE owned_schema WHEN 'system' THEN 'SELECT, INSERT' ELSE 'SELECT, INSERT, UPDATE, DELETE' END;

        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = app_role) THEN
            EXECUTE format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOINHERIT', app_role);
        END IF;
        EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), app_role);

        EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I AUTHORIZATION crowdfunding_migrator', owned_schema);
        EXECUTE format('ALTER SCHEMA %I OWNER TO crowdfunding_migrator', owned_schema);

        -- A database migrated before this script ran: hand every existing table (and the
        -- sequences that belong to it) and function in the schema to the migrator.
        FOR object_name IN
            SELECT format('%I.%I', n.nspname, c.relname)
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = owned_schema AND c.relkind IN ('r', 'p')
        LOOP
            EXECUTE format('ALTER TABLE %s OWNER TO crowdfunding_migrator', object_name);
        END LOOP;

        FOR object_name IN
            SELECT p.oid::regprocedure::text
            FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = owned_schema
        LOOP
            EXECUTE format('ALTER FUNCTION %s OWNER TO crowdfunding_migrator', object_name);
        END LOOP;

        -- The schema is the unit of access: only its module's role may even look inside.
        EXECUTE format('REVOKE ALL ON SCHEMA %I FROM PUBLIC', owned_schema);
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', owned_schema, app_role);

        -- Objects that exist now ...
        EXECUTE format('GRANT %s ON ALL TABLES IN SCHEMA %I TO %I', privileges, owned_schema, app_role);
        EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I', owned_schema, app_role);

        -- The migrations history is the migrator's business only.
        IF to_regclass(format('%I.%I', owned_schema, '__EFMigrationsHistory')) IS NOT NULL THEN
            EXECUTE format('REVOKE ALL ON TABLE %I.%I FROM %I', owned_schema, '__EFMigrationsHistory', app_role);
        END IF;

        -- ... and every table or sequence a later migration (run by the migrator) creates.
        EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE crowdfunding_migrator IN SCHEMA %I GRANT %s ON TABLES TO %I',
            owned_schema, privileges, app_role);
        EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE crowdfunding_migrator IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO %I',
            owned_schema, app_role);
    END LOOP;
END
$roles$;
