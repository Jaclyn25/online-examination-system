using System.Security.Claims;
using Aperture.Api.Data;
using Aperture.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Exams;

public static class PublishingEndpoints
{
    public static void MapPublishing(this WebApplication app)
    {
        var group = app.MapGroup("/api/instructor/exams/{id:guid}").RequireAuthorization(p => p.RequireRole("Instructor"));
        group.MapPost("/publish", async (Guid id, PublishInput input, AppDb db, ClaimsPrincipal user, TimeProvider clock) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var exam = await ExamEndpoints.Owned(db, id, user, true); ExamEndpoints.Draft(exam, input.Revision);
            var questions = await db.Questions.Include(q => q.Choices).Where(q => q.ExamId == id).OrderBy(q => q.Position).ToListAsync();
            if (questions.Count is < 1 or > 100) throw new ApiError(400, "Publish an exam with 1–100 valid questions.");
            foreach (var q in questions) QuestionEndpoints.Validate(new(q.Text, q.Marks, q.Choices.Select(c => new ChoiceInput(c.Text, c.IsCorrect)).ToList(), input.Revision));
            var version = new ExamVersion
            {
                ExamId = id,
                Number = (await db.ExamVersions.Where(v => v.ExamId == id).MaxAsync(v => (int?)v.Number) ?? 0) + 1,
                Title = exam.Title,
                Description = exam.Description,
                DurationMinutes = exam.DurationMinutes,
                PassingPercentage = exam.PassingPercentage,
                ShowFeedback = input.ShowFeedback,
                PublishedAt = clock.GetUtcNow(),
                Questions = questions.Select((q, i) => new VersionQuestion
                {
                    Text = q.Text,
                    Marks = q.Marks,
                    Position = i + 1,
                    Choices = q.Choices.OrderBy(c => c.Position).Select(c => new VersionChoice { Text = c.Text, Position = c.Position, IsCorrect = c.IsCorrect }).ToList()
                }).ToList()
            };
            db.ExamVersions.Add(version); exam.Status = "Published"; exam.Revision++;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { exam.Id, exam.Status, exam.Revision, versionId = version.Id, versionNumber = version.Number });
        });
        group.MapPut("/status", async (Guid id, StatusInput input, AppDb db, ClaimsPrincipal user) =>
        {
            if (input.Status is not ("Draft" or "Closed")) throw new ApiError(400, "Choose Draft or Closed. Use Publish to publish a validated version.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var exam = await ExamEndpoints.Owned(db, id, user, true);
            if (exam.Revision != input.Revision) throw new ApiError(409, "Exam changed. Refresh before updating.");
            exam.Status = input.Status; exam.Revision++;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(exam);
        });
        app.MapGet("/api/student/exams", async (AppDb db) =>
            await db.ExamVersions.AsNoTracking().Where(v => db.Exams.Any(e => e.Id == v.ExamId && e.Status == "Published") && !db.ExamVersions.Any(newer => newer.ExamId == v.ExamId && newer.Number > v.Number))
                .OrderByDescending(v => v.PublishedAt).Select(v => new { v.ExamId, versionId = v.Id, v.Number, v.Title, v.Description, v.DurationMinutes, v.PassingPercentage, questionCount = v.Questions.Count, totalMarks = v.Questions.Sum(q => q.Marks), v.ShowFeedback }).ToListAsync())
            .RequireAuthorization(p => p.RequireRole("Student"));
    }
}
