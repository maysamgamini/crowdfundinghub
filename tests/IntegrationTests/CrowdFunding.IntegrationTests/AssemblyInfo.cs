// Several fixtures here (MultiDatabaseConnectionDecouplingTests, ApiPlatformE2ETests' rate-limit
// override) mutate process-wide environment variables that Program.cs reads at host-build time.
// Different xunit collections run in parallel by default, which would let one test's env var
// mutation leak into another collection's host build mid-flight — disabling cross-collection
// parallelism trades a few extra seconds of wall-clock time for that not being possible.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
