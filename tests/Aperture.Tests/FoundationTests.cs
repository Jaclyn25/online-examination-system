namespace Aperture.Tests;

public class FoundationTests
{
    [Fact]
    public void ApplicationTargetsSupportedRuntime() => Assert.Equal(10, Environment.Version.Major);
}
