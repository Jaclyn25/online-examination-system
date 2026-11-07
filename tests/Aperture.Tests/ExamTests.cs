using System.Net;
using System.Net.Http.Json;
using Aperture.Api.Exams;
namespace Aperture.Tests;

public class ExamTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task InstructorCanCreateAndEditOwnedDraft()
    {
        using var c = await factory.User("Instructor");
        var response = await c.PostAsJsonAsync("/api/instructor/exams", new ExamInput("Algorithms", "Foundations", 30, 60));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var exam = (await response.Content.ReadFromJsonAsync<Exam>())!;
        Assert.Equal("Draft", exam.Status);
        var updated = await c.PutAsJsonAsync($"/api/instructor/exams/{exam.Id}", new ExamInput("Algorithms II", "Updated", 40, 70, exam.Revision));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/instructor/exams/{exam.Id}", new ExamInput("Stale", "", 40, 70, exam.Revision))).StatusCode);
    }
    [Fact]
    public async Task StudentsCannotManageExams()
    {
        using var c = await factory.User();
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/instructor/exams", new ExamInput("Test", "", 30, 50))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/instructor/exams")).StatusCode);
    }
    [Fact]
    public async Task OtherInstructorsCannotAccessExam()
    {
        using var c = await factory.User("Instructor"); using var other = await factory.User("Instructor");
        var exam = (await (await c.PostAsJsonAsync("/api/instructor/exams", new ExamInput("Private", "", 30, 50))).Content.ReadFromJsonAsync<Exam>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/instructor/exams/{exam.Id}")).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<Exam>>("/api/instructor/exams"))!);
    }
    [Theory]
    [InlineData("", 30, 50)]
    [InlineData("Exam", 0, 50)]
    [InlineData("Exam", 241, 50)]
    [InlineData("Exam", 30, 101)]
    public async Task ConfigurationValidated(string title, int duration, int passing)
    {
        using var c = await factory.User("Instructor");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/instructor/exams", new ExamInput(title, "", duration, passing))).StatusCode);
    }
}
