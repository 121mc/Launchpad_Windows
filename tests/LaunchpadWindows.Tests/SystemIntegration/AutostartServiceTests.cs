using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows.Tests.SystemIntegration;

public sealed class AutostartServiceTests
{
    [Fact]
    public void SetEnabled_WritesQuotedExecutablePath()
    {
        FakeRunKey runKey = new();
        AutostartService service = new(runKey, () => @"C:\Apps\Launchpad Windows\LaunchpadWindows.exe");

        AutostartResult result = service.SetEnabled(true);

        Assert.True(result.Success);
        Assert.Equal("\"C:\\Apps\\Launchpad Windows\\LaunchpadWindows.exe\"", runKey.Values["LaunchpadWindows"]);
    }

    [Fact]
    public void SetEnabledFalse_RemovesValue()
    {
        FakeRunKey runKey = new();
        runKey.Values["LaunchpadWindows"] = "\"app.exe\"";
        AutostartService service = new(runKey, () => "app.exe");

        AutostartResult result = service.SetEnabled(false);

        Assert.True(result.Success);
        Assert.False(runKey.Values.ContainsKey("LaunchpadWindows"));
    }

    [Fact]
    public void SetEnabled_ReturnsFailureWhenRunKeyWriteFails()
    {
        FakeRunKey runKey = new(throwOnSet: true);
        AutostartService service = new(runKey, () => @"C:\Apps\LaunchpadWindows.exe");

        AutostartResult result = service.SetEnabled(true);

        Assert.False(result.Success);
        Assert.Contains("registry denied", result.Message);
        Assert.Empty(runKey.Values);
    }

    [Fact]
    public void SetEnabledFalse_ReturnsFailureWhenRunKeyDeleteFails()
    {
        FakeRunKey runKey = new(throwOnDelete: true);
        runKey.Values["LaunchpadWindows"] = "\"app.exe\"";
        AutostartService service = new(runKey, () => "app.exe");

        AutostartResult result = service.SetEnabled(false);

        Assert.False(result.Success);
        Assert.Contains("registry denied", result.Message);
        Assert.True(runKey.Values.ContainsKey("LaunchpadWindows"));
    }

    private sealed class FakeRunKey(bool throwOnSet = false, bool throwOnDelete = false) : IRunKey
    {
        public Dictionary<string, string> Values { get; } = [];
        public void SetValue(string name, string value)
        {
            if (throwOnSet)
            {
                throw new UnauthorizedAccessException("registry denied");
            }

            Values[name] = value;
        }

        public void DeleteValue(string name)
        {
            if (throwOnDelete)
            {
                throw new UnauthorizedAccessException("registry denied");
            }

            Values.Remove(name);
        }
    }
}
