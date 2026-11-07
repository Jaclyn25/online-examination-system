using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Aperture.Api.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
namespace Aperture.Api.Auth;

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        app.MapGet("/api/auth/csrf", (IAntiforgery csrf, HttpContext context) => Results.Ok(new { token = csrf.GetAndStoreTokens(context).RequestToken }));
        var group = app.MapGroup("/api/auth").RequireRateLimiting("auth");
        group.MapPost("/register", async (RegisterRequest input, UserManager<AppUser> users) =>
        {
            if (string.IsNullOrWhiteSpace(input.Email) || !new EmailAddressAttribute().IsValid(input.Email) || input.Email.Length > 254 || string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Trim().Length > 100 || string.IsNullOrEmpty(input.Password) || input.Password.Length > 128)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["registration"] = ["Enter a valid email, name and password."] });
            var user = new AppUser { UserName = input.Email.Trim(), Email = input.Email.Trim(), DisplayName = input.DisplayName.Trim() };
            var result = await users.CreateAsync(user, input.Password);
            if (!result.Succeeded) return Results.ValidationProblem(new Dictionary<string, string[]> { ["registration"] = ["Use an unused email and a password of at least 12 characters with uppercase, lowercase, a number and a symbol."] });
            if (!(await users.AddToRoleAsync(user, "Student")).Succeeded) throw new InvalidOperationException("Role assignment failed.");
            return Results.Created("/api/auth/me", new { user.Id, user.Email, user.DisplayName, role = "Student" });
        });
        group.MapPost("/login", async (LoginRequest input, UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 || string.IsNullOrEmpty(input.Password) || input.Password.Length > 128)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["login"] = ["Enter an email and password of valid length."] });
            var user = await users.FindByEmailAsync(input.Email.Trim());
            if (user is null || input.Password.Length > 128) return Results.Problem(statusCode: 401, title: "Invalid email or password.");
            var result = await signIn.PasswordSignInAsync(user, input.Password, false, true);
            return result.Succeeded ? Results.NoContent() : Results.Problem(statusCode: 401, title: "Invalid email or password.");
        });
        group.MapPost("/logout", async (ClaimsPrincipal principal, UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
        {
            var user = (await users.GetUserAsync(principal))!;
            await users.UpdateSecurityStampAsync(user);
            await signIn.SignOutAsync(); return Results.NoContent();
        }).RequireAuthorization();
        app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<AppUser> users) =>
        {
            var user = (await users.GetUserAsync(principal))!;
            return Results.Ok(new { user.Id, user.Email, user.DisplayName, role = principal.IsInRole("Instructor") ? "Instructor" : "Student" });
        }).RequireAuthorization();
    }
    public static async Task InitializeIdentity(IServiceProvider services, IConfiguration config, IHostEnvironment environment)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Instructor", "Student" })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
        if (!environment.IsDevelopment() || !config.GetValue<bool>("Demo:Enabled")) return;
        var password = config["Demo:InstructorPassword"] ?? throw new InvalidOperationException("Supply demo password externally.");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        const string email = "instructor@aperture.test";
        if (await users.FindByEmailAsync(email) is not null) return;
        var user = new AppUser { UserName = email, Email = email, DisplayName = "Dr. Maya Ellis" };
        if (!(await users.CreateAsync(user, password)).Succeeded) throw new InvalidOperationException("Demo password violates policy.");
        await users.AddToRoleAsync(user, "Instructor");
    }
}
