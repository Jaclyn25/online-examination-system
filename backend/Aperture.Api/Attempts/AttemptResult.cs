namespace Aperture.Api.Attempts;

public class AttemptResult
{
    public Guid AttemptId { get; set; }
    public int EarnedMarks { get; set; }
    public int TotalMarks { get; set; }
    public decimal Percentage { get; set; }
    public bool Passed { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int UnansweredCount { get; set; }
}
