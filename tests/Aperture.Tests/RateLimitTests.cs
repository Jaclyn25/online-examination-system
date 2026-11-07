using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
namespace Aperture.Tests;

public class RateLimitedFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimits:Auth"] = "20" }));
    }
}
public class RateLimitTests(RateLimitedFactory factory) : IClassFixture<RateLimitedFactory>
{
    [Fact]
    public async Task AuthenticationRequestsAreLimited()
    {
        using var c = factory.Client(); await ApiFactory.Csrf(c);
        for (var i = 0; i < 20; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.test", password = Guid.NewGuid().ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await c.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.test", password = Guid.NewGuid().ToString() })).StatusCode);
    }
}
