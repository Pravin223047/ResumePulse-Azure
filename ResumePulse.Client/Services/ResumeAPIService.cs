using System.Net.Http.Json;
using System.Text.Json;

namespace ResumePulse.Client.Services;

public class ResumeUploadResponse
{
    public string JobId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public string BlobUrl { get; set; } = "";
}

public class AnalysisReport
{
    public string JobId { get; set; } = "";
    public string ApplicantEmail { get; set; } = "";
    public int OverallScore { get; set; }
    public string ScoreSummary { get; set; } = "";
    public List<string> MatchedSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public string Recommendation { get; set; } = "";
}

public class ResumeApiService
{
    private readonly HttpClient _httpClient;

    public ResumeApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ResumeUploadResponse?> UploadResumeAsync(
        Stream fileStream,
        string fileName,
        string jobDescription,
        string applicantEmail)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "resumeFile", fileName);
        content.Add(new StringContent(jobDescription), "jobDescription");
        content.Add(new StringContent(applicantEmail), "applicantEmail");

        var response = await _httpClient.PostAsync("api/resume/upload", content);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<ResumeUploadResponse>();

        return null;
    }

    public async Task<AnalysisReport?> PollForReportAsync(string jobId, int maxAttempts = 15)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(5000); // poll every 5 seconds

            var response = await _httpClient.GetAsync($"api/resume/report/{jobId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<AnalysisReport>(json, options);
            }
        }
        return null;
    }
}
