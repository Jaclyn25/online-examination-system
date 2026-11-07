using System.Security.Claims;
using Aperture.Api.Data;
using Aperture.Api.Exams;
using Aperture.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Attempts;
public class AttemptService(AppDb db, TimeProvider clock)
{
    public async Task<Attempt> Owned(Guid id, ClaimsPrincipal user)
    {
        var a = await db.Attempts.FromSqlInterpolated($"SELECT * FROM \"Attempts\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (a is null || a.StudentId != ExamEndpoints.UserId(user)) throw new ApiError(404, "Attempt not found.");
        return a;
    }
    public async Task<object> View(Attempt a)
    {
        var version = await db.ExamVersions.AsNoTracking().Include(v => v.Questions).ThenInclude(q => q.Choices).SingleAsync(v => v.Id == a.ExamVersionId);
        var answers = await db.StudentAnswers.AsNoTracking().Where(x => x.AttemptId == a.Id).ToListAsync();
        return new { a.Id, a.ExamId, a.ExamVersionId, a.Status, a.Revision, a.StartedAt, a.Deadline, a.CompletedAt, serverNow = clock.GetUtcNow(), version.Title, version.Description, version.DurationMinutes, version.PassingPercentage,
            questions = version.Questions.OrderBy(q => q.Position).Select(q => new { q.Id, q.Text, q.Marks, q.Position, choices = q.Choices.OrderBy(c => c.Position).Select(c => new { c.Id, c.Text, c.Position }), selectedChoiceId = answers.SingleOrDefault(x => x.VersionQuestionId == q.Id)?.VersionChoiceId }) };
    }
}
