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
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
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
