using System.Security.Claims;
using Aperture.Api.Attempts;
using Aperture.Api.Data;
using Aperture.Api.Exams;
using Aperture.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Reports;

public static class ReportEndpoints
{
    public static void MapReports(this WebApplication app)
    {
        var student = app.MapGroup("/api/student").RequireAuthorization(p => p.RequireRole("Student"));
        student.MapGet("/history", async (AppDb db, ReportService reports, ClaimsPrincipal user) =>
        {
            var userId = ExamEndpoints.UserId(user); await reports.FinalizeDue(userId, false);
            return await (from a in db.Attempts.AsNoTracking()
                          join v in db.ExamVersions on a.ExamVersionId equals v.Id
                          join r in db.Results on a.Id equals r.AttemptId into results
                          from r in results.DefaultIfEmpty()
                          where a.StudentId == userId
                          orderby a.StartedAt descending
                          select new { a.Id, a.ExamId, v.Title, versionNumber = v.Number, a.Status, a.StartedAt, a.Deadline, a.CompletedAt, result = r }).ToListAsync();
        });
        student.MapGet("/attempts/{id:guid}/result", async (Guid id, AppDb db, AttemptService attempts, ReportService reports, ClaimsPrincipal user) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var a = await attempts.Owned(id, user); await attempts.Finalize(a); var result = await reports.Result(a);
            await tx.CommitAsync(); return Results.Ok(result);
        });
        var instructor = app.MapGroup("/api/instructor").RequireAuthorization(p => p.RequireRole("Instructor"));
        instructor.MapGet("/reports", async (ReportService reports, ClaimsPrincipal user) => await reports.Instructor(ExamEndpoints.UserId(user)));
        instructor.MapGet("/exams/{id:guid}/report", async (Guid id, ReportService reports, ClaimsPrincipal user) => await reports.Instructor(ExamEndpoints.UserId(user), id));
        instructor.MapGet("/attempts/{id:guid}/result", async (Guid id, AppDb db, AttemptService attempts, ReportService reports, ClaimsPrincipal user) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var a = await attempts.Locked(id);
            if (!await db.Exams.AnyAsync(e => e.Id == a.ExamId && e.InstructorId == ExamEndpoints.UserId(user))) throw new ApiError(404, "Attempt not found.");
            await attempts.Finalize(a); var result = await reports.Result(a, true); await tx.CommitAsync(); return Results.Ok(result);
        });
    }
}
