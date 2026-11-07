using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aperture.Api.Attempts;
namespace Aperture.Tests;
public class ReportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FeedbackRequiresFinalizationAndPublishedPermission(bool feedback)
    {
        using var instructor = await factory.User("Instructor"); using var c = await factory.User(); using var other = await factory.User(); using var outsider = await factory.User("Instructor");
        var examId = await AttemptTests.Published(instructor, feedback);
        var a = (await (await c.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        var id = a["id"]!.GetValue<Guid>(); var path = $"/api/student/attempts/{id}/result";
        Assert.Equal(HttpStatusCode.Conflict, (await c.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        (await c.PostAsync($"/api/student/attempts/{id}/submit", null)).EnsureSuccessStatusCode();
        var result = (await c.GetFromJsonAsync<JsonObject>(path))!;
        if (feedback) Assert.Contains("correctChoiceId", result.ToJsonString()); else { Assert.Null(result["feedback"]); Assert.DoesNotContain("correctChoiceId", result.ToJsonString()); }
        Assert.Empty((await other.GetFromJsonAsync<JsonArray>("/api/student/history"))!);
        Assert.Single((await c.GetFromJsonAsync<JsonArray>("/api/student/history"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/instructor/attempts/{id}/result")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/instructor/exams/{examId}/report")).StatusCode);
        Assert.Contains("correctChoiceId", await instructor.GetStringAsync($"/api/instructor/attempts/{id}/result"));
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/instructor/reports")).StatusCode);
    }
    [Fact]
    public async Task InstructorReportsReconcileAndRespectOwnership()
    {
        using var instructor = await factory.User("Instructor"); using var c = await factory.User(); using var other = await factory.User("Instructor");
        var examId = await AttemptTests.Published(instructor);
        var a = (await (await c.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        var id = a["id"]!.GetValue<Guid>(); var q = a["questions"]![0]!;
        (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{q["id"]!.GetValue<Guid>()}", new AnswerInput(q["choices"]![0]!["id"]!.GetValue<Guid>(), 1))).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/student/attempts/{id}/submit", null)).EnsureSuccessStatusCode();
        var report = (await instructor.GetFromJsonAsync<JsonObject>("/api/instructor/reports"))!;
        Assert.Equal(1, report["totalAttempts"]!.GetValue<int>()); Assert.Equal(1, report["completedAttempts"]!.GetValue<int>());
        Assert.Equal(100, report["averagePercentage"]!.GetValue<decimal>()); Assert.Equal(100, report["passRate"]!.GetValue<decimal>());
        Assert.Equal(1, report["distribution"]!.AsArray().Sum(x => x!["count"]!.GetValue<int>()));
        Assert.Equal(0, (await other.GetFromJsonAsync<JsonObject>("/api/instructor/reports"))!["totalAttempts"]!.GetValue<int>());
    }
}
