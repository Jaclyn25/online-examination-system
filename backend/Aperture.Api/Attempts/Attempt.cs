namespace Aperture.Api.Attempts;

public class Attempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public Guid ExamVersionId { get; set; }
    public string StudentId { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string Status { get; set; } = "Active";
    public int Revision { get; set; } = 1;
    public List<StudentAnswer> Answers { get; set; } = [];
}
public class StudentAnswer
{
    public Guid AttemptId { get; set; }
    public Guid VersionQuestionId { get; set; }
    public Guid? VersionChoiceId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public record AnswerInput(Guid? ChoiceId, int Revision);
