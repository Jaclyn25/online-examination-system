using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
namespace Aperture.Tests;
public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task RegistrationCannotGrantInstructorAndLoginLogoutWork()
    {
        using var client = factory.Client();
        await ApiFactory.Csrf(client);
        var email = Guid.NewGuid() + "@example.test";
        var password = "Fictional!" + Guid.NewGuid() + "A1";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "Avery Student", role = "Instructor" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.Equal("Student", (await registration.Content.ReadFromJsonAsync<JsonObject>())!["role"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
        Assert.Equal("Student", (await client.GetFromJsonAsync<JsonObject>("/api/auth/me"))!["role"]!.GetValue<string>());
        await ApiFactory.Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task AuthenticationFailureIsUnauthorized()
    {
        using var client = factory.Client(); await ApiFactory.Csrf(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.test", password = "Invalid!123456" })).StatusCode);
    }
    [Fact]
    public async Task InvalidRegistrationRejected()
    {
        using var client = factory.Client(); await ApiFactory.Csrf(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { email = "invalid", password = "short", displayName = "" })).StatusCode);
    }
    [Fact]
    public async Task MissingCsrfRejected()
    {
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.test", password = "Invalid!123456" })).StatusCode);
    }
    [Fact]
    public async Task AnonymousProfileRejected()
    {
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
}
