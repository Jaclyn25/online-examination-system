using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aperture.Api.Exams;
namespace Aperture.Tests;

public class QuestionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static QuestionInput Input(int revision = 1) => new("What does HTTP stand for?", 5, [new("Hypertext Transfer Protocol", true), new("Home Tool Transfer Protocol", false)], revision);
    [Fact]
    public async Task CreateEditReorderDelete()
    {
        using var c = await factory.User("Instructor");
        var exam = (await (await c.PostAsJsonAsync("/api/instructor/exams", new ExamInput("Web fundamentals", "", 20, 60))).Content.ReadFromJsonAsync<Exam>())!;
        var path = $"/api/instructor/exams/{exam.Id}/questions";
        var first = await (await c.PostAsJsonAsync(path, Input())).Content.ReadFromJsonAsync<JsonObject>();
        var firstId = first!["question"]!["id"]!.GetValue<Guid>();
        var second = await (await c.PostAsJsonAsync(path, Input(2))).Content.ReadFromJsonAsync<JsonObject>();
        var secondId = second!["question"]!["id"]!.GetValue<Guid>();
        (await c.PutAsJsonAsync(path + "/order", new ReorderInput([secondId, firstId], 3))).EnsureSuccessStatusCode();
        var ordered = (await c.GetFromJsonAsync<List<Question>>(path))!;
        Assert.Equal(secondId, ordered[0].Id);
        (await c.PutAsJsonAsync(path + $"/{firstId}", Input(4) with { Marks = 10 })).EnsureSuccessStatusCode();
        Assert.Equal(10, (await c.GetFromJsonAsync<List<Question>>(path))!.Single(q => q.Id == firstId).Marks);
        (await c.DeleteAsync(path + $"/{firstId}?revision=5")).EnsureSuccessStatusCode();
        Assert.Single((await c.GetFromJsonAsync<List<Question>>(path))!);
    }
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 2)]
    public async Task InvalidMarksOrAnswerKeysRejected(int marks, int correctCount)
    {
        using var c = await factory.User("Instructor");
        var exam = (await (await c.PostAsJsonAsync("/api/instructor/exams", new ExamInput("Validation", "", 20, 60))).Content.ReadFromJsonAsync<Exam>())!;
        var input = new QuestionInput("Question", marks, [new("A", correctCount > 0), new("B", correctCount > 1)], 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/instructor/exams/{exam.Id}/questions", input)).StatusCode);
    }
    [Fact]
    public async Task StudentCannotReadAnswerKeys()
    {
        using var c = await factory.User();
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/instructor/exams/{Guid.NewGuid()}/questions")).StatusCode);
    }
}
