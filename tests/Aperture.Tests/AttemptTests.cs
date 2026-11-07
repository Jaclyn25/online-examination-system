using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aperture.Api.Exams;
using Aperture.Api.Attempts;
namespace Aperture.Tests;

public class AttemptTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static async Task<Guid> Published(HttpClient c, bool feedback = true, int passing = 60)
    {
        var e = (await (await c.PostAsJsonAsync("/api/instructor/exams", new ExamInput("Web assessment", "Read each question carefully.", 1, passing))).Content.ReadFromJsonAsync<Exam>())!;
        (await c.PostAsJsonAsync($"/api/instructor/exams/{e.Id}/questions", QuestionTests.Input())).EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync($"/api/instructor/exams/{e.Id}/publish", new PublishInput(2, feedback))).EnsureSuccessStatusCode(); return e.Id;
    }
    [Fact]
    public async Task StartRestoreSaveAndChangeAnswersWithoutKeys()
    {
        using var instructor = await factory.User("Instructor"); using var c = await factory.User();
        var examId = await Published(instructor);
        var a = (await (await c.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        var id = a["id"]!.GetValue<Guid>(); var question = a["questions"]![0]!;
        var qid = question["id"]!.GetValue<Guid>(); var choice = question["choices"]![0]!["id"]!.GetValue<Guid>();
        Assert.DoesNotContain("isCorrect", a.ToJsonString());
        (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{qid}", new AnswerInput(choice, 1))).EnsureSuccessStatusCode();
        var restored = (await (await c.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(id, restored["id"]!.GetValue<Guid>());
        Assert.Equal(choice, restored["questions"]![0]!["selectedChoiceId"]!.GetValue<Guid>());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{qid}", new AnswerInput(null, 1))).StatusCode);
        (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{qid}", new AnswerInput(null, 2))).EnsureSuccessStatusCode();
        var cleared = await c.GetFromJsonAsync<JsonObject>($"/api/student/attempts/{id}"); Assert.Null(cleared!["questions"]![0]!["selectedChoiceId"]);
    }
    [Fact]
    public async Task AttemptIsolationAndForeignChoicesEnforced()
    {
        using var instructor = await factory.User("Instructor"); using var c = await factory.User(); using var other = await factory.User();
        var examId = await Published(instructor);
        var a = (await (await c.PostAsync($"/api/student/exams/{examId}/attempts", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        var id = a["id"]!.GetValue<Guid>(); var qid = a["questions"]![0]!["id"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/student/attempts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{qid}", new AnswerInput(null, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/student/attempts/{id}/answers/{qid}", new AnswerInput(Guid.NewGuid(), 1))).StatusCode);
    }
    [Fact]
    public async Task ConcurrentStartRestoresSingleAttempt()
    {
        using var instructor = await factory.User("Instructor"); using var c = await factory.User();
        var examId = await Published(instructor);
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => c.PostAsync($"/api/student/exams/{examId}/attempts", null)));
        var ids = new List<Guid>();
        foreach (var r in responses) { r.EnsureSuccessStatusCode(); ids.Add((await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>()); }
        Assert.Single(ids.Distinct());
    }
}
