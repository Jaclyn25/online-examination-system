using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Data;
public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
}
public class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Aperture.Api.Exams.Exam> Exams => Set<Aperture.Api.Exams.Exam>();
    public DbSet<Aperture.Api.Exams.Question> Questions => Set<Aperture.Api.Exams.Question>();
    public DbSet<Aperture.Api.Exams.Choice> Choices => Set<Aperture.Api.Exams.Choice>();
    public DbSet<Aperture.Api.Exams.ExamVersion> ExamVersions => Set<Aperture.Api.Exams.ExamVersion>();
    public DbSet<Aperture.Api.Exams.VersionQuestion> VersionQuestions => Set<Aperture.Api.Exams.VersionQuestion>();
    public DbSet<Aperture.Api.Exams.VersionChoice> VersionChoices => Set<Aperture.Api.Exams.VersionChoice>();
    public DbSet<Aperture.Api.Attempts.Attempt> Attempts => Set<Aperture.Api.Attempts.Attempt>();
    public DbSet<Aperture.Api.Attempts.StudentAnswer> StudentAnswers => Set<Aperture.Api.Attempts.StudentAnswer>();
    public DbSet<Aperture.Api.Attempts.AttemptResult> Results => Set<Aperture.Api.Attempts.AttemptResult>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Aperture.Api.Attempts.AttemptResult>(e =>
        {
            e.HasKey(x => x.AttemptId);
            e.HasOne<Aperture.Api.Attempts.Attempt>().WithOne().HasForeignKey<Aperture.Api.Attempts.AttemptResult>(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Percentage).HasPrecision(5, 2);
            e.ToTable("Results", t => { t.HasCheckConstraint("CK_Result_Marks", "\"TotalMarks\" > 0 AND \"EarnedMarks\" BETWEEN 0 AND \"TotalMarks\""); t.HasCheckConstraint("CK_Result_Percentage", "\"Percentage\" BETWEEN 0 AND 100"); });
        });
        b.Entity<Aperture.Api.Attempts.Attempt>(e =>
        {
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Aperture.Api.Exams.Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Aperture.Api.Exams.ExamVersion>().WithMany().HasForeignKey(x => x.ExamVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ExamId, x.StudentId }).IsUnique();
            e.HasIndex(x => new { x.Status, x.Deadline });
            e.ToTable("Attempts", t => { t.HasCheckConstraint("CK_Attempt_Status", "\"Status\" IN ('Active','Submitted','Expired')"); t.HasCheckConstraint("CK_Attempt_Deadline", "\"Deadline\" > \"StartedAt\""); });
        });
        b.Entity<Aperture.Api.Attempts.StudentAnswer>(e =>
        {
            e.HasKey(x => new { x.AttemptId, x.VersionQuestionId });
            e.HasOne<Aperture.Api.Attempts.Attempt>().WithMany(a => a.Answers).HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Aperture.Api.Exams.VersionQuestion>().WithMany().HasForeignKey(x => x.VersionQuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Aperture.Api.Exams.VersionChoice>().WithMany().HasForeignKey(x => x.VersionChoiceId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Aperture.Api.Exams.ExamVersion>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(160); e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.PassingPercentage).HasPrecision(5, 2);
            e.HasIndex(x => new { x.ExamId, x.Number }).IsUnique();
            e.HasOne<Aperture.Api.Exams.Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("ExamVersions", t => { t.HasCheckConstraint("CK_Version_Duration", "\"DurationMinutes\" BETWEEN 1 AND 240"); t.HasCheckConstraint("CK_Version_Pass", "\"PassingPercentage\" BETWEEN 0 AND 100"); });
        });
        b.Entity<Aperture.Api.Exams.VersionQuestion>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(3000);
            e.HasOne<Aperture.Api.Exams.ExamVersion>().WithMany(v => v.Questions).HasForeignKey(x => x.ExamVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ExamVersionId, x.Position }).IsUnique();
            e.ToTable("VersionQuestions", t => t.HasCheckConstraint("CK_VersionQuestion_Marks", "\"Marks\" BETWEEN 1 AND 1000"));
        });
        b.Entity<Aperture.Api.Exams.VersionChoice>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(1000);
            e.HasOne<Aperture.Api.Exams.VersionQuestion>().WithMany(q => q.Choices).HasForeignKey(x => x.VersionQuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.VersionQuestionId, x.Position }).IsUnique();
            e.HasIndex(x => x.VersionQuestionId).IsUnique().HasFilter("\"IsCorrect\" = true");
        });
        b.Entity<Aperture.Api.Exams.Question>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(3000);
            e.HasOne<Aperture.Api.Exams.Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ExamId, x.Position }).IsUnique();
            e.ToTable("Questions", t => { t.HasCheckConstraint("CK_Question_Marks", "\"Marks\" BETWEEN 1 AND 1000"); t.HasCheckConstraint("CK_Question_Position", "\"Position\" > 0"); });
        });
        b.Entity<Aperture.Api.Exams.Choice>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(1000);
            e.HasOne<Aperture.Api.Exams.Question>().WithMany(q => q.Choices).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.QuestionId, x.Position }).IsUnique();
            e.HasIndex(x => x.QuestionId).IsUnique().HasFilter("\"IsCorrect\" = true");
        });
        b.Entity<Aperture.Api.Exams.Exam>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.PassingPercentage).HasPrecision(5, 2);
            e.HasIndex(x => new { x.InstructorId, x.Status });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("Exams", t =>
            {
                t.HasCheckConstraint("CK_Exam_Duration", "\"DurationMinutes\" BETWEEN 1 AND 240");
                t.HasCheckConstraint("CK_Exam_Pass", "\"PassingPercentage\" BETWEEN 0 AND 100");
                t.HasCheckConstraint("CK_Exam_Status", "\"Status\" IN ('Draft','Published','Closed')");
            });
        });
    }
}
