using System.Security.Claims;
using Aperture.Api.Data;
using Aperture.Api.Exams;
using Aperture.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Attempts;
public static class AttemptEndpoints
{
    public static void MapAttempts(this WebApplication app)
    {
        var group = app.MapGroup("/api/student").RequireAuthorization(p => p.RequireRole("Student"));
        group.MapPost("/exams/{id:guid}/attempts", async (Guid id, AppDb db, AttemptService service, ClaimsPrincipal user, TimeProvider clock) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var exam = await db.Exams.FromSqlInterpolated($"SELECT * FROM \"Exams\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync() ?? throw new ApiError(404, "Exam not found.");
            var userId = ExamEndpoints.UserId(user);
            var existing = await db.Attempts.SingleOrDefaultAsync(a => a.ExamId == id && a.StudentId == userId);
            if (existing is not null)
            {
                // A fresh tracked value under the attempt lock prevents a mixed restore snapshot.
                db.Entry(existing).State = EntityState.Detached;
                existing = await service.Owned(existing.Id, user);
                if (existing.Status != "Active") throw new ApiError(409, "You already completed this exam. Open your history to view the result.");
                await service.Finalize(existing);
                var restored = await service.View(existing); await tx.CommitAsync(); return Results.Ok(restored);
            }
            if (exam.Status != "Published") throw new ApiError(409, "This exam is not available.");
            var version = await db.ExamVersions.Where(v => v.ExamId == id).OrderByDescending(v => v.Number).FirstAsync();
            var now = clock.GetUtcNow();
            var a = new Attempt { ExamId = id, ExamVersionId = version.Id, StudentId = userId, StartedAt = now, Deadline = now.AddMinutes(version.DurationMinutes) };
            db.Attempts.Add(a); await db.SaveChangesAsync(); var view = await service.View(a); await tx.CommitAsync(); return Results.Ok(view);
        });
        group.MapGet("/attempts/{id:guid}", async (Guid id, AppDb db, AttemptService service, ClaimsPrincipal user) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var a = await service.Owned(id, user); await service.Finalize(a); var view = await service.View(a); await tx.CommitAsync(); return Results.Ok(view);
        });
        group.MapPut("/attempts/{id:guid}/answers/{questionId:guid}", async (Guid id, Guid questionId, AnswerInput input, AppDb db, AttemptService service, ClaimsPrincipal user, TimeProvider clock) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var a = await service.Owned(id, user);
            await service.Finalize(a);
            if (a.Status != "Active") { await tx.CommitAsync(); return Results.Problem(statusCode: 409, title: "This attempt no longer accepts answers."); }
            if (a.Revision != input.Revision) throw new ApiError(409, "Answers changed in another session. Refresh to restore saved answers.");
            if (!await db.VersionQuestions.AnyAsync(q => q.Id == questionId && q.ExamVersionId == a.ExamVersionId) || (input.ChoiceId.HasValue && !await db.VersionChoices.AnyAsync(c => c.Id == input.ChoiceId && c.VersionQuestionId == questionId))) throw new ApiError(400, "Choose an answer belonging to this question and attempt.");
            var savedAt = clock.GetUtcNow();
            if (savedAt >= a.Deadline) { await service.Finalize(a); await tx.CommitAsync(); return Results.Problem(statusCode: 409, title: "The deadline has passed."); }
            var answer = await db.StudentAnswers.FindAsync(id, questionId);
            if (answer is null) { answer = new StudentAnswer { AttemptId = id, VersionQuestionId = questionId }; db.StudentAnswers.Add(answer); }
            answer.VersionChoiceId = input.ChoiceId; answer.SavedAt = savedAt; a.Revision++;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { a.Revision, answer.SavedAt });
        });
        group.MapPost("/attempts/{id:guid}/submit", async (Guid id, AppDb db, AttemptService service, ClaimsPrincipal user) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var a = await service.Owned(id, user); var result = await service.Finalize(a, true);
            await tx.CommitAsync(); return Results.Ok(result);
        });
    }
}
