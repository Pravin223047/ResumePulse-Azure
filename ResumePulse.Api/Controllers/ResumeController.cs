using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using ResumePulse.Api.Models;
using ResumePulse.Api.Services;
using System.Text.Json;

namespace ResumePulse.Api.Controllers;

[ApiController]
[Route("api/resume")]
public class ResumeController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly IServiceBusService _serviceBusService;
    private readonly IConfiguration _configuration;

    public ResumeController(
        IBlobStorageService blobStorageService,
        IServiceBusService serviceBusService,
        IConfiguration configuration)
    {
        _blobStorageService = blobStorageService;
        _serviceBusService = serviceBusService;
        _configuration = configuration;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] ResumeUploadRequest request)
    {
        if (request.ResumeFile == null || request.ResumeFile.Length == 0)
            return BadRequest("Resume file is required.");

        var jobId = Guid.NewGuid().ToString();

        // Uses existing IBlobStorageService signature: (IFormFile, string)
        var blobUrl = await _blobStorageService.UploadResumeAsync(request.ResumeFile, jobId);

        // Uses existing IServiceBusService signature: SendResumeAnalysisMessageAsync
        await _serviceBusService.SendResumeAnalysisMessageAsync(new
        {
            JobId = jobId,
            BlobUrl = blobUrl,
            JobDescription = request.JobDescription,
            ApplicantEmail = request.ApplicantEmail,
            SubmittedAt = DateTime.UtcNow
        });

        return Ok(new ResumeUploadResponse
        {
            JobId = jobId,
            Status = "Queued",
            Message = "Resume uploaded and queued for analysis.",
            BlobUrl = blobUrl
        });
    }

    [HttpGet("report/{jobId}")]
    public async Task<IActionResult> GetReport(string jobId)
    {
        try
        {
            var connectionString = _configuration["StorageConnectionString"]
                ?? _configuration["AzureStorage:ConnectionString"]!;
            var reportsContainer = _configuration["AzureStorage:ReportsContainer"] ?? "reports";

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(reportsContainer);
            var blobClient = containerClient.GetBlobClient($"{jobId}/report.json");

            var exists = await blobClient.ExistsAsync();
            if (!exists)
                return NotFound(new { Status = "Processing" });

            var download = await blobClient.DownloadContentAsync();
            var reportJson = download.Value.Content.ToString();
            var report = JsonSerializer.Deserialize<JsonElement>(reportJson);

            return Ok(report);
        }
        catch
        {
            return NotFound(new { Status = "Processing" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
}
