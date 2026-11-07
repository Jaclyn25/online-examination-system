using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aperture.Api.Attempts;
using Aperture.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Aperture.Tests;

public class ScoringTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ExpirationSweepFinalizesDisconnectedAttempt()
    {
        var (c, id, _) = await Start(); using var client = c;
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var worker = new ExpirationWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<ExpirationWorker>.Instance);
        await worker.Sweep();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal("Expired", (await db.Attempts.FindAsync(id))!.Status);
        Assert.Equal(0, (await db.Results.FindAsync(id))!.EarnedMarks);
        await worker.Sweep();
        Assert.Equal(1, await db.Results.CountAsync(r => r.AttemptId == id));
    }
    private async Task<(HttpClient Client, Guid Id, JsonNode Question)> Start(int passing = 60)
    {
        using var instructor = await factory.User("Instructor"); var c = await factory.User();
        var examId = await AttemptTests.Published(instructor, passing: passing);
        var a = (await (await c.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        return (c, a["id"]!.GetValue<Guid>(), a["questions"]![0]!);
    }
    [Theory]
    [InlineData(0, 100, true, 1, 0, 0)]
    [InlineData(1, 0, false, 0, 1, 0)]
    [InlineData(-1, 0, false, 0, 0, 1)]
    public async Task ServerCalculatesMarksAndUnanswered(int selected, int percentage, bool passed, int correct, int incorrect, int unanswered)
    {
        var (c, id, q) = await Start(100); using var client = c;
        if (selected >= 0) (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{q["id"]!.GetValue<Guid>()}", new AnswerInput(q["choices"]![selected]!["id"]!.GetValue<Guid>(), 1))).EnsureSuccessStatusCode();
        var response = await c.PostAsJsonAsync($"/api/student/attempts/{id}/submit", new { earnedMarks = 99999, percentage = 100, passed = true });
        var r = (await response.Content.ReadFromJsonAsync<AttemptResult>())!;
        Assert.Equal(5, r.TotalMarks); Assert.Equal(percentage, r.Percentage); Assert.Equal(passed, r.Passed);
        Assert.Equal(correct, r.CorrectCount); Assert.Equal(incorrect, r.IncorrectCount); Assert.Equal(unanswered, r.UnansweredCount);
    }
    [Fact]
    public async Task AtExactDeadlineAnswersRejectedAndResultFinalized()
    {
        var (c, id, q) = await Start(); using var client = c;
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{q["id"]!.GetValue<Guid>()}", new AnswerInput(q["choices"]![0]!["id"]!.GetValue<Guid>(), 1))).StatusCode);
        var a = (await c.GetFromJsonAsync<JsonObject>($"/api/student/attempts/{id}"))!;
        Assert.Equal("Expired", a["status"]!.GetValue<string>());
        Assert.Equal(a["deadline"]!.GetValue<DateTimeOffset>(), a["completedAt"]!.GetValue<DateTimeOffset>());
        var r = await (await c.PostAsync($"/api/student/attempts/{id}/submit", null)).Content.ReadFromJsonAsync<AttemptResult>(); Assert.Equal(0, r!.EarnedMarks);
    }
    [Fact]
    public async Task ConcurrentFinalizationReturnsOneImmutableResult()
    {
        var (c, id, q) = await Start(); using var client = c;
        (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{q["id"]!.GetValue<Guid>()}", new AnswerInput(q["choices"]![0]!["id"]!.GetValue<Guid>(), 1))).EnsureSuccessStatusCode();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => c.PostAsync($"/api/student/attempts/{id}/submit", null)));
        foreach (var response in responses) { response.EnsureSuccessStatusCode(); Assert.Equal(5, (await response.Content.ReadFromJsonAsync<AttemptResult>())!.EarnedMarks); }
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{q["id"]!.GetValue<Guid>()}", new AnswerInput(null, 2))).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal(1, await db.Results.CountAsync(r => r.AttemptId == id));
    }
    [Fact]
    public async Task ExpiredSubmitAndSaveRaceCannotCreditLateAnswer()
    {
        var (c, id, q) = await Start(); using var client = c;
        factory.Clock.Advance(TimeSpan.FromMinutes(2));
        var save = c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{q["id"]!.GetValue<Guid>()}", new AnswerInput(q["choices"]![0]!["id"]!.GetValue<Guid>(), 1));
        var submit = c.PostAsync($"/api/student/attempts/{id}/submit", null); await Task.WhenAll(save, submit);
        Assert.Equal(HttpStatusCode.Conflict, (await save).StatusCode);
        Assert.Equal(0, (await (await submit).Content.ReadFromJsonAsync<AttemptResult>())!.EarnedMarks);
    }
    [Fact]
    public async Task OtherStudentCannotSubmit()
    {
        var (c, id, _) = await Start(); using var client = c; using var other = await factory.User();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/student/attempts/{id}/submit", null)).StatusCode);
    }
    [Theory]
    [InlineData("66.66", true)]
    [InlineData("66.67", false)]
    public async Task PassingUsesExactWeightedScoreInsteadOfRoundedPercentage(string threshold, bool passed)
    {
        using var instructor = await factory.User("Instructor"); using var c = await factory.User();
        var exam = (await (await instructor.PostAsJsonAsync("/api/instructor/exams", new Aperture.Api.Exams.ExamInput("Weighted boundary", "", 10, decimal.Parse(threshold, System.Globalization.CultureInfo.InvariantCulture)))).Content.ReadFromJsonAsync<Aperture.Api.Exams.Exam>())!;
        var path = $"/api/instructor/exams/{exam.Id}";
        (await instructor.PostAsJsonAsync(path + "/questions", QuestionTests.Input() with { Marks = 2 })).EnsureSuccessStatusCode();
        (await instructor.PostAsJsonAsync(path + "/questions", QuestionTests.Input(2) with { Marks = 1 })).EnsureSuccessStatusCode();
        (await instructor.PostAsJsonAsync(path + "/publish", new Aperture.Api.Exams.PublishInput(3))).EnsureSuccessStatusCode();
        var a = (await (await c.PostAsync($"/api/student/exams/{exam.Id}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        var id = a["id"]!.GetValue<Guid>(); var q = a["questions"]![0]!;
        (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{q["id"]!.GetValue<Guid>()}", new AnswerInput(q["choices"]![0]!["id"]!.GetValue<Guid>(), 1))).EnsureSuccessStatusCode();
        var r = (await (await c.PostAsync($"/api/student/attempts/{id}/submit", null)).Content.ReadFromJsonAsync<AttemptResult>())!;
        Assert.Equal(2, r.EarnedMarks); Assert.Equal(3, r.TotalMarks); Assert.Equal(66.67m, r.Percentage); Assert.Equal(passed, r.Passed);
    }
}
