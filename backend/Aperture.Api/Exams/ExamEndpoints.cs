using System.Security.Claims;
using Aperture.Api.Data;
using Aperture.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Exams;

public static class ExamEndpoints
{
    public static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public static async Task<Exam> Owned(AppDb db, Guid id, ClaimsPrincipal user, bool locked = false)
    {
        var query = locked ? db.Exams.FromSqlInterpolated($"SELECT * FROM \"Exams\" WHERE \"Id\" = {id} FOR UPDATE") : db.Exams.Where(e => e.Id == id);
        var exam = await query.SingleOrDefaultAsync();
        if (exam is null || exam.InstructorId != UserId(user)) throw new ApiError(404, "Exam not found.");
        return exam;
    }
    public static void Draft(Exam exam, int revision)
    {
        if (exam.Status != "Draft") throw new ApiError(409, "Return this exam to Draft before editing. Published content remains immutable.");
        if (exam.Revision != revision) throw new ApiError(409, "This exam changed. Refresh before editing.");
    }
    private static void Validate(ExamInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 160 || input.Description is null || input.Description.Length > 4000 || input.DurationMinutes is < 1 or > 240 || input.PassingPercentage is < 0 or > 100 || decimal.Round(input.PassingPercentage, 2) != input.PassingPercentage)
            throw new ApiError(400, "Provide a title (1–160 characters), description (up to 4000), duration (1–240 minutes) and passing percentage (0–100, up to two decimals).");
    }
    public static void MapExams(this WebApplication app)
    {
        var group = app.MapGroup("/api/instructor/exams").RequireAuthorization(p => p.RequireRole("Instructor"));
        group.MapGet("/", async (AppDb db, ClaimsPrincipal user) => await db.Exams.AsNoTracking().Where(e => e.InstructorId == UserId(user)).OrderByDescending(e => e.CreatedAt).ToListAsync());
        group.MapGet("/{id:guid}", async (Guid id, AppDb db, ClaimsPrincipal user) => await Owned(db, id, user));
        group.MapPost("/", async (ExamInput input, AppDb db, ClaimsPrincipal user, TimeProvider clock) =>
        {
            Validate(input);
            var exam = new Exam { InstructorId = UserId(user), Title = input.Title.Trim(), Description = input.Description.Trim(), DurationMinutes = input.DurationMinutes, PassingPercentage = input.PassingPercentage, CreatedAt = clock.GetUtcNow() };
            db.Exams.Add(exam); await db.SaveChangesAsync();
            return Results.Created($"/api/instructor/exams/{exam.Id}", exam);
        });
        group.MapPut("/{id:guid}", async (Guid id, ExamInput input, AppDb db, ClaimsPrincipal user) =>
        {
            Validate(input);
            await using var tx = await db.Database.BeginTransactionAsync();
            var exam = await Owned(db, id, user, true); Draft(exam, input.Revision);
            exam.Title = input.Title.Trim(); exam.Description = input.Description.Trim(); exam.DurationMinutes = input.DurationMinutes; exam.PassingPercentage = input.PassingPercentage; exam.Revision++;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(exam);
        });
    }
}
