namespace Aperture.Api.Exams;
public class ExamVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int DurationMinutes { get; set; }
    public decimal PassingPercentage { get; set; }
    public bool ShowFeedback { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public List<VersionQuestion> Questions { get; set; } = [];
}
public class VersionQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamVersionId { get; set; }
    public string Text { get; set; } = "";
    public int Marks { get; set; }
    public int Position { get; set; }
    public List<VersionChoice> Choices { get; set; } = [];
}
public class VersionChoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VersionQuestionId { get; set; }
    public string Text { get; set; } = "";
    public int Position { get; set; }
    public bool IsCorrect { get; set; }
}
public record PublishInput(int Revision, bool ShowFeedback = true);
public record StatusInput(string Status, int Revision);
