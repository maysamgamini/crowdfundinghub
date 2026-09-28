-- DEVELOPMENT AND TESTS ONLY. Well-known passwords for the roles created by 01-module-roles.sql,
-- matching the connection strings in src/API/CrowdFunding.API/appsettings.Development.json.
-- Never run this against a shared or production database: set real passwords from a secret store.

ALTER ROLE crowdfunding_migrator PASSWORD 'crowdfunding_migrator_dev';
ALTER ROLE campaigns_app         PASSWORD 'campaigns_app_dev';
ALTER ROLE contributions_app     PASSWORD 'contributions_app_dev';
ALTER ROLE identity_app          PASSWORD 'identity_app_dev';
ALTER ROLE moderation_app        PASSWORD 'moderation_app_dev';
ALTER ROLE notifications_app     PASSWORD 'notifications_app_dev';
ALTER ROLE campaignupdates_app   PASSWORD 'campaignupdates_app_dev';
ALTER ROLE audit_app             PASSWORD 'audit_app_dev';
