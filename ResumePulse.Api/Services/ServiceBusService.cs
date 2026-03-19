using Azure.Messaging.ServiceBus;
using System.Text.Json;

namespace ResumePulse.Api.Services;

public interface IServiceBusService
{
    Task SendResumeAnalysisMessageAsync(object message);
}

public class ServiceBusService : IServiceBusService
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusService(IConfiguration configuration)
    {
        var connectionString = configuration["ServiceBusConnectionString"]
                            ?? configuration["ServiceBus:ConnectionString"];
        var queueName = configuration["ServiceBus:QueueName"] ?? "resume-analysis-queue";

        _client = new ServiceBusClient(connectionString);
        _sender = _client.CreateSender(queueName);
    }

    public async Task SendResumeAnalysisMessageAsync(object message)
    {
        var json = JsonSerializer.Serialize(message);
        var sbMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json"
        };
        await _sender.SendMessageAsync(sbMessage);
    }
}
