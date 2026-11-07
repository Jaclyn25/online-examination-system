using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aperture.Api.Auth;
using Aperture.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
namespace Aperture.Tests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string database = "aperture_test_" + Guid.NewGuid().ToString("N");
    private readonly string connection = Environment.GetEnvironmentVariable("EXAMS_TEST_CONNECTION") ?? throw new InvalidOperationException("EXAMS_TEST_CONNECTION is required; tests use real PostgreSQL.");
    public string TestConnection => new NpgsqlConnectionStringBuilder(connection) { Database = database }.ToString();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Exams"] = TestConnection, ["Database:Migrate"] = "false" }));
    }
    public async Task InitializeAsync()
    {
        await using var connectionDb = new NpgsqlConnection(connection);
        await connectionDb.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connectionDb);
        await cmd.ExecuteNonQueryAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDb>().Database.MigrateAsync();
        await AuthEndpoints.InitializeIdentity(Services, new ConfigurationBuilder().Build(), new TestEnvironment());
    }
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var c = new NpgsqlConnection(connection);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", c);
        await cmd.ExecuteNonQueryAsync();
    }
    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
    public static async Task Csrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!["token"]!.GetValue<string>());
    }
    public async Task<HttpClient> User(string role = "Student")
    {
        var email = Guid.NewGuid().ToString("N") + "@aperture.test";
        var password = "Test!" + Guid.NewGuid().ToString("N") + "A1";
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = email, Email = email, DisplayName = "Fictional QA" };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        await users.AddToRoleAsync(user, role);
        var client = Client();
        await Csrf(client);
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password })).EnsureSuccessStatusCode();
        await Csrf(client);
        return client;
    }
    private sealed class TestEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Aperture.Tests";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
