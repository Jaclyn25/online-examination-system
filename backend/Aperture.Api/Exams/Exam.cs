namespace Aperture.Api.Exams;

public class Exam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InstructorId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int DurationMinutes { get; set; }
    public decimal PassingPercentage { get; set; }
    public string Status { get; set; } = "Draft";
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
}
public record ExamInput(string Title, string Description, int DurationMinutes, decimal PassingPercentage, int Revision = 0);
