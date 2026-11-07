using Aperture.Api.Attempts;
using Aperture.Api.Exams;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Data;
public static class DemoSeed
{
    public static async Task Run(IServiceProvider services)
    {
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        if (await db.Exams.AnyAsync()) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var instructor = await users.FindByEmailAsync("instructor@aperture.test"); if (instructor is null) return;
        await using var tx = await db.Database.BeginTransactionAsync();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var students = new List<AppUser>();
        foreach (var name in new[] { "Avery Chen", "Samira Hassan", "Leo Martins" })
        {
            var email = name.ToLowerInvariant().Replace(' ', '.') + "@fictional.aperture.test";
            var student = new AppUser { Email = email, UserName = email, DisplayName = name };
            // Historical fictional learners have no password and cannot sign in.
            if (!(await users.CreateAsync(student)).Succeeded) throw new InvalidOperationException("Fictional seed user creation failed.");
            await users.AddToRoleAsync(student, "Student"); students.Add(student);
        }
        var sets = new[]
        {
            ("Web Platform Foundations", "Demonstrate your understanding of HTTP, browser security and accessible interfaces.", new[] {
                ("Which HTTP method retrieves a resource without changing it?", "GET", "POST", "DELETE", "PATCH"),
                ("Which response status indicates authentication is required?", "401", "200", "301", "500"),
                ("What does HTTPS protect in transit?", "Confidentiality and integrity", "Only font rendering", "Database normalization", "Local file permissions"),
                ("Which element groups related radio choices accessibly?", "fieldset", "canvas", "span", "iframe"),
                ("Which cookie flag prevents JavaScript from reading a session cookie?", "HttpOnly", "Path", "Max-Age", "Domain"),
                ("What should a client do after a transient network failure while saving?", "Retry safely and show save status", "Assume the answer is saved", "Calculate the final score", "Change the deadline") }),
            ("Data & Query Essentials", "A practical checkpoint on relational data, transactions and dependable querying.", new[] {
                ("Which constraint ensures every row has a unique identity?", "PRIMARY KEY", "ORDER BY", "GROUP BY", "OFFSET"),
                ("Which SQL clause filters rows before grouping?", "WHERE", "HAVING", "LIMIT", "UNION"),
                ("What does an atomic transaction guarantee?", "All changes commit together or roll back", "Queries always run instantly", "Indexes are unnecessary", "Every row is public"),
                ("Which data type is suitable for exact decimal values?", "numeric", "real", "double precision", "text arrays"),
                ("What does a foreign key protect?", "Referential integrity", "Network bandwidth", "Browser history", "CSS specificity") }),
            ("Secure Systems Practice", "Prepare an assessment on application security and authorization.", new[] {
                ("Where must authorization be enforced?", "On the server", "Only in navigation menus", "Only in CSS", "Only in client storage"),
                ("How should user passwords be stored?", "With a strong salted password hash", "In plain text", "In frontend source", "In query strings"),
                ("Which defense helps prevent cross-site request forgery?", "Validated antiforgery tokens", "Longer page titles", "Public database ports", "Client-side scoring") })
        };
        for (var index = 0; index < sets.Length; index++)
        {
            var (title, description, items) = sets[index];
            var exam = new Exam { InstructorId = instructor.Id, Title = title, Description = description, DurationMinutes = 30, PassingPercentage = 60, CreatedAt = now.AddDays(-5 + index), Status = index == 2 ? "Draft" : "Published", Revision = items.Length + 2 };
            db.Exams.Add(exam);
            var questions = items.Select((item, i) => new Question { ExamId = exam.Id, Text = item.Item1, Marks = 5, Position = i + 1, Choices = new[] { item.Item2, item.Item3, item.Item4, item.Item5 }.Select((text, j) => new Choice { Text = text, Position = j + 1, IsCorrect = j == 0 }).ToList() }).ToList();
            db.Questions.AddRange(questions);
            if (index == 2) continue;
            var version = new ExamVersion { ExamId = exam.Id, Number = 1, Title = title, Description = description, DurationMinutes = 30, PassingPercentage = 60, PublishedAt = now.AddDays(-3 + index), ShowFeedback = true,
                Questions = questions.Select(q => new VersionQuestion { Text = q.Text, Marks = q.Marks, Position = q.Position, Choices = q.Choices.Select(c => new VersionChoice { Text = c.Text, Position = c.Position, IsCorrect = c.IsCorrect }).ToList() }).ToList() };
            db.ExamVersions.Add(version); await db.SaveChangesAsync();
            for (var s = 0; s < students.Count; s++)
            {
                var a = new Attempt { ExamId = exam.Id, ExamVersionId = version.Id, StudentId = students[s].Id, StartedAt = now.AddMinutes(-20 + s), Deadline = now.AddMinutes(10 + s) };
                db.Attempts.Add(a);
                foreach (var q in version.Questions)
                    if (!(s == 2 && q.Position == items.Length)) db.StudentAnswers.Add(new StudentAnswer { AttemptId = a.Id, VersionQuestionId = q.Id, VersionChoiceId = q.Choices.Single(c => c.Position == (q.Position <= items.Length - s * 2 ? 1 : 2)).Id, SavedAt = now.AddMinutes(-5) });
                await db.SaveChangesAsync();
                await scope.ServiceProvider.GetRequiredService<AttemptService>().Finalize(a, true);
            }
        }
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
