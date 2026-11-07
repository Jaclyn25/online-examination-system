using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aperture.Api.Attempts;
using Aperture.Api.Data;
using Aperture.Api.Exams;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Aperture.Tests;

public class SecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task FinalizedAnswerCannotBeDeleted()
    {
        using var instructor = await factory.User("Instructor"); using var student = await factory.User();
        var examId = await AttemptTests.Published(instructor);
        var a = (await (await student.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        var id = a["id"]!.GetValue<Guid>(); var q = a["questions"]![0]!; var questionId = q["id"]!.GetValue<Guid>();
        (await student.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{questionId}", new AnswerInput(q["choices"]![0]!["id"]!.GetValue<Guid>(), 1))).EnsureSuccessStatusCode();
        (await student.PostAsync($"/api/student/attempts/{id}/submit", null)).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.StudentAnswers.Remove((await db.StudentAnswers.FindAsync(id, questionId))!);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task LogoutRevokesPreviouslyIssuedCookie()
    {
        using var c = factory.Client(); await ApiFactory.Csrf(c);
        var email = Guid.NewGuid() + "@example.test"; var password = "QA!" + Guid.NewGuid() + "Aa1";
        (await c.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "Fictional session test" })).EnsureSuccessStatusCode();
        var login = await c.PostAsJsonAsync("/api/auth/login", new { email, password }); login.EnsureSuccessStatusCode();
        var cookie = login.Headers.GetValues("Set-Cookie").Single(s => s.StartsWith("aperture.session=")).Split(';')[0];
        Assert.Contains("httponly", login.Headers.GetValues("Set-Cookie").Single(s => s.StartsWith("aperture.session=")).ToLowerInvariant());
        await ApiFactory.Csrf(c); (await c.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        using var replay = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }); replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/auth/me")).StatusCode);
        using var scope = factory.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = (await users.FindByEmailAsync(email))!; Assert.NotEqual(password, user.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed, users.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash!, password));
    }
    [Fact]
    public async Task FiveBadPasswordsLockTheAccount()
    {
        using var c = factory.Client(); await ApiFactory.Csrf(c); var email = Guid.NewGuid() + "@example.test"; var password = "QA!" + Guid.NewGuid() + "Aa1";
        (await c.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "Fictional lockout test" })).EnsureSuccessStatusCode();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email, password = Guid.NewGuid().ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
    }
    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/login")]
    public async Task NullCredentialsAreValidationErrors(string path)
    {
        using var c = factory.Client(); await ApiFactory.Csrf(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(path, new { email = (string?)null, password = (string?)null, displayName = (string?)null })).StatusCode);
    }
    [Fact]
    public async Task PublishedVersionPreservesActiveAndCompletedAttempt()
    {
        using var instructor = await factory.User("Instructor"); using var student = await factory.User();
        var examId = await AttemptTests.Published(instructor); var path = $"/api/instructor/exams/{examId}";
        var a = (await (await student.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        var id = a["id"]!.GetValue<Guid>(); var q = a["questions"]![0]!;
        (await student.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{q["id"]!.GetValue<Guid>()}", new AnswerInput(q["choices"]![0]!["id"]!.GetValue<Guid>(), 1))).EnsureSuccessStatusCode();
        (await instructor.PutAsJsonAsync(path + "/status", new StatusInput("Draft", 3))).EnsureSuccessStatusCode();
        var original = (await instructor.GetFromJsonAsync<List<Question>>(path + "/questions"))!.Single();
        (await instructor.PutAsJsonAsync(path + $"/questions/{original.Id}", new QuestionInput("Changed future question", 100, [new("Different key", false), new("New correct answer", true)], 4))).EnsureSuccessStatusCode();
        (await instructor.PostAsJsonAsync(path + "/publish", new PublishInput(5))).EnsureSuccessStatusCode();
        var result = (await (await student.PostAsync($"/api/student/attempts/{id}/submit", null)).Content.ReadFromJsonAsync<AttemptResult>())!;
        Assert.Equal(5, result.TotalMarks); Assert.Equal(5, result.EarnedMarks);
        var report = await student.GetStringAsync($"/api/student/attempts/{id}/result"); Assert.DoesNotContain("Changed future question", report); Assert.Contains("Hypertext Transfer Protocol", report);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsync($"/api/student/exams/{examId}/attempts", null)).StatusCode);
        using var newcomer = await factory.User(); var next = (await (await newcomer.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("Changed future question", next["questions"]![0]!["text"]!.GetValue<string>());
    }
    [Fact]
    public async Task DatabaseRejectsFinalizedAnswerAndResultTampering()
    {
        using var instructor = await factory.User("Instructor"); using var student = await factory.User(); var examId = await AttemptTests.Published(instructor);
        var a = (await (await student.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!; var id = a["id"]!.GetValue<Guid>();
        (await student.PostAsync($"/api/student/attempts/{id}/submit", null)).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.StudentAnswers.Add(new StudentAnswer { AttemptId = id, VersionQuestionId = a["questions"]![0]!["id"]!.GetValue<Guid>(), VersionChoiceId = a["questions"]![0]!["choices"]![0]!["id"]!.GetValue<Guid>(), SavedAt = factory.Clock.GetUtcNow() });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var result = (await db.Results.FindAsync(id))!; result.EarnedMarks = result.TotalMarks;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var attempt = (await db.Attempts.FindAsync(id))!; attempt.Status = "Active";
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
