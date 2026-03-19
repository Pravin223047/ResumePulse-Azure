using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ResumePulse.Api.Services;

public interface IBlobStorageService
{
    Task<string> UploadResumeAsync(IFormFile file, string jobId);
    Task<bool> DeleteBlobAsync(string containerName, string blobName);
}

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _resumesContainer;

    public BlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnectionString"]
                            ?? configuration["AzureStorage:ConnectionString"];

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                "StorageConnectionString is missing. Check Key Vault or appsettings.");

        _resumesContainer = configuration["AzureStorage:ResumesContainer"] ?? "resumes";
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<string> UploadResumeAsync(IFormFile file, string jobId)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_resumesContainer);
        await containerClient.CreateIfNotExistsAsync();

        var extension = Path.GetExtension(file.FileName);
        var blobName = $"{jobId}/resume{extension}";
        var blobClient = containerClient.GetBlobClient(blobName);

        var blobHttpHeaders = new BlobHttpHeaders
        {
            ContentType = file.ContentType
        };

        using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = blobHttpHeaders
        });

        return blobClient.Uri.ToString();
    }

    public async Task<bool> DeleteBlobAsync(string containerName, string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        return await blobClient.DeleteIfExistsAsync();
    }
}
