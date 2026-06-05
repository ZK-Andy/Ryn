// Every test class here shells out to `dotnet run`/`dotnet build` against the SAME Ryn.Cli
// project. Run in parallel, those concurrent builds race on Ryn.Cli's bin/obj and intermittently
// fail with "The build failed." Serialize the whole assembly so only one CLI build runs at a time.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
