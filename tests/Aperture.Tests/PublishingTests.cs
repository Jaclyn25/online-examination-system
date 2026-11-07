using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aperture.Api.Data;
using Aperture.Api.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Aperture.Tests;
public class PublishingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task EmptyExamCannotPublish()
    {
        using var c = await factory.User("Instructor");
        var e = (await (await c.PostAsJsonAsync("/api/instructor/exams", new ExamInput("Empty", "", 30, 60))).Content.ReadFromJsonAsync<Exam>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/instructor/exams/{e.Id}/publish", new PublishInput(1))).StatusCode);
    }
    [Fact]
    public async Task PublishedContentIsImmutableAndStudentListHasNoKeys()
    {
        using var c = await factory.User("Instructor"); using var student = await factory.User();
        var e = (await (await c.PostAsJsonAsync("/api/instructor/exams", new ExamInput("Version one", "", 30, 60))).Content.ReadFromJsonAsync<Exam>())!;
        var path = $"/api/instructor/exams/{e.Id}";
        (await c.PostAsJsonAsync(path + "/questions", QuestionTests.Input())).EnsureSuccessStatusCode();
        var published = await (await c.PostAsJsonAsync(path + "/publish", new PublishInput(2))).Content.ReadFromJsonAsync<JsonObject>();
        var versionId = published!["versionId"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(path, new ExamInput("Changed", "", 30, 60, 3))).StatusCode);
        var visible = await student.GetStringAsync("/api/student/exams");
        Assert.Contains("Version one", visible); Assert.DoesNotContain("isCorrect", visible); Assert.DoesNotContain("Hypertext", visible);
        (await c.PutAsJsonAsync(path + "/status", new StatusInput("Draft", 3))).EnsureSuccessStatusCode();
        (await c.PutAsJsonAsync(path, new ExamInput("Version two", "", 40, 80, 4))).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var old = await db.ExamVersions.FindAsync(versionId); Assert.Equal("Version one", old!.Title); Assert.Equal(60, old.PassingPercentage);
        Assert.DoesNotContain("Version one", await student.GetStringAsync("/api/student/exams"));
        old.Title = "Tampered";
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
