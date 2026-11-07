using System.Threading.RateLimiting;
using Aperture.Api.Auth;
using Aperture.Api.Data;
using Aperture.Api.Infrastructure;
using Aperture.Api.Exams;
using Aperture.Api.Attempts;
using Aperture.Api.Reports;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
if (args.Contains("--healthcheck"))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        Environment.ExitCode = (await client.GetAsync("http://127.0.0.1:8080/api/health")).IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { Environment.ExitCode = 1; }
    catch (TaskCanceledException) { Environment.ExitCode = 1; }
    return;
}
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 128 * 1024);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
if (builder.Configuration["DataProtection:KeyPath"] is { } keyPath)
    builder.Services.AddDataProtection().SetApplicationName("Aperture.Exams").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AttemptService>();
builder.Services.AddScoped<ReportService>();
if (!builder.Environment.IsEnvironment("Testing")) builder.Services.AddHostedService<ExpirationWorker>();
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Exams") ?? throw new InvalidOperationException("ConnectionStrings:Exams is required.")));
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 12;
    o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
}).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "aperture.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(4);
    o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.SameSite = SameSiteMode.Strict; });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:Auth", 20), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("work", c => RateLimitPartition.GetFixedWindowLimiter(c.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async c =>
{
    var error = c.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var status = error is ApiError api ? api.Status : error is BadHttpRequestException ? 400 : 500;
    await Results.Problem(statusCode: status, title: status == 500 ? "An unexpected error occurred." : error!.Message).ExecuteAsync(c);
}));
app.Use(async (c, next) =>
{
    c.Response.Headers["X-Content-Type-Options"] = "nosniff";
    c.Response.Headers["Referrer-Policy"] = "same-origin";
    c.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (c, next) =>
{
    if (c.Request.Method is "POST" or "PUT" or "DELETE" or "PATCH")
    {
        try { await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c); }
        catch (AntiforgeryValidationException) { await Results.Problem(statusCode: 400, title: "Refresh the page and retry (invalid security token).").ExecuteAsync(c); return; }
    }
    await next();
});
app.MapOpenApi();
app.MapAuth();
app.MapExams();
app.MapQuestions();
app.MapPublishing();
app.MapAttempts();
app.MapReports();
app.MapGet("/api/health", async (AppDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "healthy", product = "Aperture Exams" }) : Results.StatusCode(503));
if (app.Configuration.GetValue<bool>("Database:Migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDb>().Database.MigrateAsync();
    await AuthEndpoints.InitializeIdentity(app.Services, app.Configuration, app.Environment);
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Demo:Enabled") && app.Configuration.GetValue<bool>("Database:Seed")) await DemoSeed.Run(app.Services);
}
app.Run();
public partial class Program;
