using Xunit;

// Every test class in this assembly drives the same single emulator/device through one Appium
// session at a time — two sessions fighting over the same device's UI concurrently would make both
// flaky. DisableTestParallelization makes xUnit run all test classes in this assembly sequentially
// (tests within a class were already sequential by xUnit's own default).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
