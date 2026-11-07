using System.Security.Claims;
using Aperture.Api.Data;
using Aperture.Api.Exams;
using Aperture.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Attempts;

public class AttemptService(AppDb db, TimeProvider clock)
{
    public async Task<Attempt> Locked(Guid id) => await db.Attempts.FromSqlInterpolated($"SELECT * FROM \"Attempts\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync() ?? throw new ApiError(404, "Attempt not found.");
    public async Task<Attempt> Owned(Guid id, ClaimsPrincipal user)
    {
        var a = await Locked(id);
        if (a.StudentId != ExamEndpoints.UserId(user)) throw new ApiError(404, "Attempt not found.");
        return a;
    }
    // Caller holds the attempt row lock and commits the result with the attempt state.
    public async Task<AttemptResult?> Finalize(Attempt a, bool submit = false)
    {
        if (a.Status != "Active") return await db.Results.SingleAsync(r => r.AttemptId == a.Id);
        var now = clock.GetUtcNow();
        if (!submit && now < a.Deadline) return null;
        var version = await db.ExamVersions.AsNoTracking().Include(v => v.Questions).ThenInclude(q => q.Choices).SingleAsync(v => v.Id == a.ExamVersionId);
        var answers = await db.StudentAnswers.AsNoTracking().Where(x => x.AttemptId == a.Id && x.VersionChoiceId != null).ToDictionaryAsync(x => x.VersionQuestionId, x => x.VersionChoiceId);
        var result = new AttemptResult { AttemptId = a.Id, TotalMarks = version.Questions.Sum(q => q.Marks) };
        foreach (var q in version.Questions)
        {
            if (!answers.TryGetValue(q.Id, out var choice)) result.UnansweredCount++;
            else if (q.Choices.Single(c => c.IsCorrect).Id == choice) { result.CorrectCount++; result.EarnedMarks += q.Marks; }
            else result.IncorrectCount++;
        }
        result.Percentage = decimal.Round(result.EarnedMarks * 100m / result.TotalMarks, 2, MidpointRounding.AwayFromZero);
        result.Passed = result.EarnedMarks * 100m >= version.PassingPercentage * result.TotalMarks;
        a.Status = now >= a.Deadline ? "Expired" : "Submitted";
        a.CompletedAt = now >= a.Deadline ? a.Deadline : now;
        a.Revision++; db.Results.Add(result); await db.SaveChangesAsync(); return result;
    }
    public async Task<object> View(Attempt a)
    {
        var version = await db.ExamVersions.AsNoTracking().Include(v => v.Questions).ThenInclude(q => q.Choices).SingleAsync(v => v.Id == a.ExamVersionId);
        var answers = await db.StudentAnswers.AsNoTracking().Where(x => x.AttemptId == a.Id).ToListAsync();
        return new
        {
            a.Id,
            a.ExamId,
            a.ExamVersionId,
            a.Status,
            a.Revision,
            a.StartedAt,
            a.Deadline,
            a.CompletedAt,
            serverNow = clock.GetUtcNow(),
            version.Title,
            version.Description,
            version.DurationMinutes,
            version.PassingPercentage,
            questions = version.Questions.OrderBy(q => q.Position).Select(q => new { q.Id, q.Text, q.Marks, q.Position, choices = q.Choices.OrderBy(c => c.Position).Select(c => new { c.Id, c.Text, c.Position }), selectedChoiceId = answers.SingleOrDefault(x => x.VersionQuestionId == q.Id)?.VersionChoiceId })
        };
    }
}
