namespace ResumePulse.Api.Models;

public class ResumeUploadRequest
{
    public IFormFile? ResumeFile { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
}

public class ResumeUploadResponse
{
    public string JobId { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}