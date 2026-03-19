using System.Text.Json;
using Azure.Messaging.EventGrid;
using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ResumePulse.Functions.Models;
using ResumePulse.Functions.Services;
using Azure.Storage.Blobs;

namespace ResumePulse.Functions.Functions;

public class ResumeAnalysisFunction
{
    private readonly ILogger<ResumeAnalysisFunction> _logger;

    public ResumeAnalysisFunction(ILogger<ResumeAnalysisFunction> logger)
    {
        _logger = logger;
    }

    [Function("ResumeAnalysisFunction")]
    public async Task Run(
        [ServiceBusTrigger("resume-analysis-queue", Connection = "ServiceBusConnectionString")] string messageBody)
    {
        _logger.LogInformation("Resume analysis triggered. Message: {Message}", messageBody);

        var message = JsonSerializer.Deserialize<ResumeAnalysisMessage>(messageBody);
        if (message == null)
        {
            _logger.LogError("Failed to deserialize message.");
            return;
        }

        _logger.LogInformation("Processing JobId: {JobId}", message.JobId);

        try
        {
            // 1. Download resume from Blob
            var storageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString")!;
            var resumesContainer = Environment.GetEnvironmentVariable("ResumesContainer") ?? "resumes";
            var reportsContainer = Environment.GetEnvironmentVariable("ReportsContainer") ?? "reports";

            var blobServiceClient = new BlobServiceClient(storageConnectionString);
            var resumeContainerClient = blobServiceClient.GetBlobContainerClient(resumesContainer);
            var resumeBlobClient = resumeContainerClient.GetBlobClient($"{message.JobId}/resume.pdf");

            var resumeDownload = await resumeBlobClient.DownloadContentAsync();
            var resumeText = resumeDownload.Value.Content.ToString();

            // 2. Analyze with Gemini
            var geminiApiKey = Environment.GetEnvironmentVariable("GeminiApiKey")!;
            var geminiService = new GeminiService(geminiApiKey);
            var report = await geminiService.AnalyzeResumeAsync(
                resumeText,
                message.JobDescription,
                message.JobId,
                message.ApplicantEmail);

            _logger.LogInformation("Analysis complete. Score: {Score}", report.OverallScore);

            // 3. Save report to Blob
            var reportContainerClient = blobServiceClient.GetBlobContainerClient(reportsContainer);
            var reportBlobClient = reportContainerClient.GetBlobClient($"{message.JobId}/report.json");
            var reportJson = JsonSerializer.Serialize(report);
            var reportBytes = System.Text.Encoding.UTF8.GetBytes(reportJson);
            using var reportStream = new MemoryStream(reportBytes);
            await reportBlobClient.UploadAsync(reportStream, overwrite: true);

            _logger.LogInformation("Report saved to Blob. JobId: {JobId}", message.JobId);

            // 4. Publish Event to Event Grid
            var topicEndpoint = Environment.GetEnvironmentVariable("EventGridTopicEndpoint")!;
            var topicKey = Environment.GetEnvironmentVariable("EventGridTopicKey")!;

            var eventGridClient = new EventGridPublisherClient(
                new Uri(topicEndpoint),
                new AzureKeyCredential(topicKey));

            var eventData = new
            {
                JobId = message.JobId,
                ApplicantEmail = message.ApplicantEmail,
                OverallScore = report.OverallScore,
                Recommendation = report.Recommendation,
                ScoreSummary = report.ScoreSummary,
                MatchedSkills = report.MatchedSkills,
                MissingSkills = report.MissingSkills,
                ReportUrl = $"https://YOUR_STORAGE_ACCOUNT_NAME.blob.core.windows.net/reports/{message.JobId}/report.json"
            };

            var events = new List<EventGridEvent>
            {
                new EventGridEvent(
                    subject: $"resume/analysis/{message.JobId}",
                    eventType: "ResumePulse.ResumeAnalysisCompleted",
                    dataVersion: "1.0",
                    data: BinaryData.FromObjectAsJson(eventData))
            };

            await eventGridClient.SendEventsAsync(events);
            _logger.LogInformation("Event published to Event Grid. JobId: {JobId}", message.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error processing resume analysis for JobId: {JobId}", message.JobId);
            _logger.LogError("Result: Error processing resume analysis for JobId: {JobId}\nType:\nException: {Exception}", message.JobId, ex);
            throw;
        }
    }
}
