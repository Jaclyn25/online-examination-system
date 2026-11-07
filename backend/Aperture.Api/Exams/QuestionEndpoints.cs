using System.Security.Claims;
using Aperture.Api.Data;
using Aperture.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Exams;
public static class QuestionEndpoints
{
    public static void Validate(QuestionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 3000 || input.Marks is < 1 or > 1000 || input.Choices is null || input.Choices.Count is < 2 or > 8 || input.Choices.Any(c => string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > 1000) || input.Choices.Count(c => c.IsCorrect) != 1 || input.Choices.Select(c => c.Text.Trim().ToUpperInvariant()).Distinct().Count() != input.Choices.Count)
            throw new ApiError(400, "A question needs text, 1–1000 marks and 2–8 distinct choices with exactly one correct answer.");
    }
    private static List<Choice> Choices(QuestionInput input) => input.Choices.Select((c, i) => new Choice { Text = c.Text.Trim(), IsCorrect = c.IsCorrect, Position = i + 1 }).ToList();
    public static void MapQuestions(this WebApplication app)
    {
        var group = app.MapGroup("/api/instructor/exams/{id:guid}/questions").RequireAuthorization(p => p.RequireRole("Instructor"));
        group.MapGet("/", async (Guid id, AppDb db, ClaimsPrincipal user) =>
        {
            await ExamEndpoints.Owned(db, id, user);
            return await db.Questions.AsNoTracking().Include(q => q.Choices.OrderBy(c => c.Position)).Where(q => q.ExamId == id).OrderBy(q => q.Position).ToListAsync();
        });
        group.MapPost("/", async (Guid id, QuestionInput input, AppDb db, ClaimsPrincipal user) =>
        {
            Validate(input);
            await using var tx = await db.Database.BeginTransactionAsync();
            var exam = await ExamEndpoints.Owned(db, id, user, true); ExamEndpoints.Draft(exam, input.Revision);
            var count = await db.Questions.CountAsync(q => q.ExamId == id);
            if (count >= 100) throw new ApiError(400, "An exam supports up to 100 questions.");
            var position = (await db.Questions.Where(q => q.ExamId == id).MaxAsync(q => (int?)q.Position) ?? 0) + 1;
            var question = new Question { ExamId = id, Text = input.Text.Trim(), Marks = input.Marks, Position = position, Choices = Choices(input) };
            db.Questions.Add(question); exam.Revision++;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { question, exam.Revision });
        });
        group.MapPut("/{questionId:guid}", async (Guid id, Guid questionId, QuestionInput input, AppDb db, ClaimsPrincipal user) =>
        {
            Validate(input);
            await using var tx = await db.Database.BeginTransactionAsync();
            var exam = await ExamEndpoints.Owned(db, id, user, true); ExamEndpoints.Draft(exam, input.Revision);
            var question = await db.Questions.Include(q => q.Choices).SingleOrDefaultAsync(q => q.Id == questionId && q.ExamId == id) ?? throw new ApiError(404, "Question not found.");
            db.Choices.RemoveRange(question.Choices);
            // Delete old choices before inserting replacements at the same positions.
            await db.SaveChangesAsync();
            question.Text = input.Text.Trim(); question.Marks = input.Marks; question.Choices = Choices(input); exam.Revision++;
            db.Choices.AddRange(question.Choices);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { question, exam.Revision });
        });
        group.MapDelete("/{questionId:guid}", async (Guid id, Guid questionId, int revision, AppDb db, ClaimsPrincipal user) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var exam = await ExamEndpoints.Owned(db, id, user, true); ExamEndpoints.Draft(exam, revision);
            var question = await db.Questions.SingleOrDefaultAsync(q => q.Id == questionId && q.ExamId == id) ?? throw new ApiError(404, "Question not found.");
            db.Questions.Remove(question); exam.Revision++;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { exam.Revision });
        });
        group.MapPut("/order", async (Guid id, ReorderInput input, AppDb db, ClaimsPrincipal user) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var exam = await ExamEndpoints.Owned(db, id, user, true); ExamEndpoints.Draft(exam, input.Revision);
            var questions = await db.Questions.Where(q => q.ExamId == id).ToListAsync();
            if (input.QuestionIds is null || input.QuestionIds.Count != questions.Count || !input.QuestionIds.ToHashSet().SetEquals(questions.Select(q => q.Id)) || input.QuestionIds.Distinct().Count() != questions.Count) throw new ApiError(400, "Provide every question exactly once.");
            foreach (var q in questions) q.Position += 1000;
            await db.SaveChangesAsync();
            foreach (var q in questions) q.Position = input.QuestionIds.IndexOf(q.Id) + 1;
            exam.Revision++; await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { exam.Revision });
        });
    }
}
