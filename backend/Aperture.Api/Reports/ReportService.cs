using Aperture.Api.Attempts;
using Aperture.Api.Data;
using Aperture.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Reports;

public class ReportService(AppDb db, AttemptService attempts, TimeProvider clock)
{
    public async Task FinalizeDue(string userId, bool instructor)
    {
        var ids = await db.Attempts.AsNoTracking().Where(a => a.Status == "Active" && a.Deadline <= clock.GetUtcNow() &&
            (instructor ? db.Exams.Any(e => e.Id == a.ExamId && e.InstructorId == userId) : a.StudentId == userId)).Select(a => a.Id).ToListAsync();
        foreach (var id in ids)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await attempts.Finalize(await attempts.Locked(id)); await tx.CommitAsync(); db.ChangeTracker.Clear();
        }
    }
    public async Task<object> Result(Attempt a, bool instructor = false)
    {
        if (a.Status == "Active") throw new ApiError(409, "Results are available after submission or expiration.");
        var version = await db.ExamVersions.AsNoTracking().Include(v => v.Questions).ThenInclude(q => q.Choices).SingleAsync(v => v.Id == a.ExamVersionId);
        var result = await db.Results.AsNoTracking().SingleAsync(r => r.AttemptId == a.Id);
        var answers = await db.StudentAnswers.AsNoTracking().Where(x => x.AttemptId == a.Id).ToDictionaryAsync(x => x.VersionQuestionId, x => x.VersionChoiceId);
        return new
        {
            attemptId = a.Id,
            a.ExamId,
            version.Title,
            version.Description,
            versionNumber = version.Number,
            version.PassingPercentage,
            a.Status,
            a.StartedAt,
            a.Deadline,
            a.CompletedAt,
            result,
            feedback = instructor || version.ShowFeedback ? version.Questions.OrderBy(q => q.Position).Select(q => new { q.Id, q.Text, q.Marks, q.Position, selectedChoiceId = answers.GetValueOrDefault(q.Id), correctChoiceId = q.Choices.Single(c => c.IsCorrect).Id, choices = q.Choices.OrderBy(c => c.Position).Select(c => new { c.Id, c.Text }) }) : null
        };
    }
    public async Task<object> Instructor(string userId, Guid? examId = null)
    {
        await FinalizeDue(userId, true);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var exams = await db.Exams.AsNoTracking().Where(e => e.InstructorId == userId && (!examId.HasValue || e.Id == examId)).OrderByDescending(e => e.CreatedAt).ToListAsync();
        if (examId.HasValue && exams.Count == 0) throw new ApiError(404, "Exam not found.");
        var rows = await (from a in db.Attempts.AsNoTracking()
                          join e in db.Exams on a.ExamId equals e.Id
                          join v in db.ExamVersions on a.ExamVersionId equals v.Id
                          join u in db.Users on a.StudentId equals u.Id
                          join r in db.Results on a.Id equals r.AttemptId into resultGroup
                          from r in resultGroup.DefaultIfEmpty()
                          where e.InstructorId == userId && (!examId.HasValue || e.Id == examId)
                          orderby a.StartedAt descending
                          select new { id = a.Id, a.ExamId, title = v.Title, studentName = u.DisplayName, versionNumber = v.Number, a.Status, a.StartedAt, a.CompletedAt, result = r }).ToListAsync();
        var completed = rows.Where(x => x.result is not null).ToList();
        var distribution = Enumerable.Range(0, 5).Select(i => new { label = i == 4 ? "80–100" : $"{i * 20}–{i * 20 + 19}", count = completed.Count(x => x.result!.Percentage >= i * 20 && (i == 4 || x.result.Percentage < (i + 1) * 20)) }).ToList();
        var report = new
        {
            totalExams = exams.Count,
            publishedExams = exams.Count(e => e.Status == "Published"),
            totalAttempts = rows.Count,
            completedAttempts = completed.Count,
            averagePercentage = completed.Count == 0 ? 0 : decimal.Round(completed.Average(x => x.result!.Percentage), 2),
            passRate = completed.Count == 0 ? 0 : decimal.Round(completed.Count(x => x.result!.Passed) * 100m / completed.Count, 2),
            distribution,
            exams = exams.Select(e => { var all = rows.Where(x => x.ExamId == e.Id).ToList(); var done = all.Where(x => x.result is not null).ToList(); return new { e.Id, e.Title, e.Status, attempts = all.Count, completed = done.Count, averagePercentage = done.Count == 0 ? 0 : decimal.Round(done.Average(x => x.result!.Percentage), 2), passRate = done.Count == 0 ? 0 : decimal.Round(done.Count(x => x.result!.Passed) * 100m / done.Count, 2) }; }),
            attempts = examId.HasValue ? rows : null
        };
        await tx.CommitAsync(); return report;
    }
}
