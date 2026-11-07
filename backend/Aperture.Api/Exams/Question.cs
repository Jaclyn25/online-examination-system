namespace Aperture.Api.Exams;

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public string Text { get; set; } = "";
    public int Marks { get; set; }
    public int Position { get; set; }
    public List<Choice> Choices { get; set; } = [];
}
public class Choice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public string Text { get; set; } = "";
    public int Position { get; set; }
    public bool IsCorrect { get; set; }
}
public record ChoiceInput(string Text, bool IsCorrect);
public record QuestionInput(string Text, int Marks, List<ChoiceInput> Choices, int Revision);
public record ReorderInput(List<Guid> QuestionIds, int Revision);
