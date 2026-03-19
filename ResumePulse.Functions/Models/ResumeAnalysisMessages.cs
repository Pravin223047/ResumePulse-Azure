namespace ResumePulse.Functions.Models;

public class ResumeAnalysisMessage
{
    public string JobId { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public class ResumeAnalysisReport
{
    public string JobId { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public int OverallScore { get; set; }
    public string ScoreSummary { get; set; } = string.Empty;
    public List<string> MatchedSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public string Recommendation { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
