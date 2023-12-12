using System.Dynamic;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Cloud.BigQuery.V2;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace WebApi.Controllers;

[ApiController]
[Route("")]
// TODO: Extract logic to MediatR handlers
// TODO: Create an unified why to log request ids
public class NaturalLanguageController : ControllerBase
{
    private const string BearerSchemaName = "Bearer";
    private const int RunStatusCheckDelayInMs = 1000;
    private static readonly JsonSerializerOptions OpenAiJsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly IConfiguration configuration;
    private readonly ILogger<NaturalLanguageController> logger;
    private readonly HttpClient httpClient;
    private readonly OpenAiOptions openAiOptions;

    public NaturalLanguageController(IConfiguration configuration, ILogger<NaturalLanguageController> logger, IHttpClientFactory httpClientFactory, IOptions<OpenAiOptions> openAiOptionsProvider)
    {
        this.configuration = configuration;
        this.logger = logger;
        httpClient = httpClientFactory.CreateClient();
        openAiOptions = openAiOptionsProvider.Value;
    }

    [HttpPost("audio-to-text")]
    public async Task<string> AudioToText(IFormFile file, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting to covert the audio file '{filename}' to text", file.FileName);

        using var fileStream = file.OpenReadStream();

        using var whisperRequest = new HttpRequestMessage(HttpMethod.Post, openAiOptions.WhisperEndpoint);
        whisperRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerSchemaName, openAiOptions.ApiKey);
        whisperRequest.Content = new MultipartFormDataContent
        {
            { new StreamContent(fileStream), "file", file.FileName },
            { new StringContent(openAiOptions.WhisperModel), "model" },
            { new StringContent("en"), "language" }
        };

        var whisperResponse = await httpClient.SendAsync(whisperRequest, cancellationToken);

        if (!whisperResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to call the Whisper model: '${whisperResponse.StatusCode} {whisperResponse.ReasonPhrase}'");
        }

        var recognition = (await whisperResponse.Content.ReadFromJsonAsync<WhisperRecognition>(cancellationToken))!;

        logger.LogInformation("Successfully converted audio to text. Converted text: {text}", recognition.Text);
        return recognition.Text;
    }

    [HttpPost("text-to-sql")]
    public async Task<string> TextToSql([FromBody] TextToCommandRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting to convert the text to command. Text: {request.Text}");
        
        var threadCreationResult = await CreateThread(cancellationToken);
        var threadId = threadCreationResult.Id;

        await AddMessageToThread(threadId, request.Text, cancellationToken);
        var initalRunModel = await RunAssistant(threadId, cancellationToken);
        var runId = initalRunModel.Id;

        bool isRunCompleted = false;
        while (!isRunCompleted) 
        {
            var run = await RetrieveRun(threadId, runId, cancellationToken);
            if (run.Status is ThreadRunStatuses.Completed) {
                isRunCompleted = true;
            }
            else if (run.Status is ThreadRunStatuses.Queued or ThreadRunStatuses.InProgress) {
                await Task.Delay(TimeSpan.FromMilliseconds(RunStatusCheckDelayInMs), cancellationToken);
            }
            else {
                throw new InvalidOperationException($"Received an unsupported run status - {run.Status}");
            }
        }

        var messageList = await RetrieveMessageList(threadId, cancellationToken);

        var response = messageList.Data.Last(message => message.Role == MessageRoles.Assistant).Content.Last().Text.Value;
        return response;
    }

    [HttpPost("get-thread-messages/{threadId}")]
    public async Task<MessageList> CheckTread([FromRoute] string threadId, CancellationToken cancellationToken)
    {
        var messageList = await RetrieveMessageList(threadId, cancellationToken);
        return messageList;
    }

    private async Task<MessageList> RetrieveMessageList(string threadId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving a message list. ThreadId - {threadId}", threadId);

        var url = $"{openAiOptions.ThreadsEndpoint}/{threadId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(openAiOptions.OpenAiBetaHeaderName, openAiOptions.OpenAiBetaHeaderValue);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerSchemaName, openAiOptions.ApiKey);

        var response = await httpClient.SendAsync(request, cancellationToken);
        ValidateOpenAiResponse(response);

        var responseText = await response.Content.ReadAsStringAsync();
        var messageList = JsonSerializer.Deserialize<MessageList>(responseText, OpenAiJsonSerializerOptions)!;

        logger.LogInformation("Retrieved the message list. ThreadId - {threadId}, MessageList - {list}", threadId, messageList);

        return messageList;
    }

    public record MessageList(IReadOnlyCollection<Message> Data, string FirstId, string LastId, bool HasMore);

    public record Message(string Id, string Role, IReadOnlyCollection<MessagePayload> Content);

    public static class MessageRoles
    {
        public const string User = "user";
    
        public const string Assistant = "assistant";
    }

    public record MessagePayload(string Type, MessagePayloadText Text);

    public record MessagePayloadText(string Value);


    private async Task<ThreadRun> RetrieveRun(string threadId, string runId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving a run. ThreadId - {threadId}, RunId - {runId}", threadId, runId);

        var url = $"{openAiOptions.ThreadsEndpoint}/{threadId}/runs/{runId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(openAiOptions.OpenAiBetaHeaderName, openAiOptions.OpenAiBetaHeaderValue);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerSchemaName, openAiOptions.ApiKey);

        var response = await httpClient.SendAsync(request, cancellationToken);
        ValidateOpenAiResponse(response);

        var responseText = await response.Content.ReadAsStringAsync();
        var run = JsonSerializer.Deserialize<ThreadRun>(responseText, OpenAiJsonSerializerOptions)!;

        logger.LogInformation("Retrieved the run. ThreadId - {threadId}, Run - {run}", threadId, run);

        return run;
    }

    private async Task<ThreadRun> RunAssistant(string threadId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running the text-to-sql assistant. ThreadId - {threadId}, AssistantId - {assistantId}", threadId, openAiOptions.TextToSqlAssistantId);

        var url = $"{openAiOptions.ThreadsEndpoint}/{threadId}/runs";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add(openAiOptions.OpenAiBetaHeaderName, openAiOptions.OpenAiBetaHeaderValue);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerSchemaName, openAiOptions.ApiKey);
        request.Content = JsonContent.Create(new
        {
            assistant_id = openAiOptions.TextToSqlAssistantId,
        });

        var response = await httpClient.SendAsync(request, cancellationToken);
        ValidateOpenAiResponse(response);

        var responseText = await response.Content.ReadAsStringAsync();
        var responseBody = JsonSerializer.Deserialize<ThreadRun>(responseText, OpenAiJsonSerializerOptions)!;

        logger.LogInformation(
            "Successfully started the run of text-to-sql assistant. ThreadId - {threadId}, AssistantId - {assistantId}, RunId - {runId}",
            threadId,
            openAiOptions.TextToSqlAssistantId,
            responseBody.Id);

        return responseBody;
    }

    private record ThreadRun(
        string Id,
        string Object,
        int CreatedAt,
        string AssistantId,
        string ThreadId,
        string Status,
        int? StartedAt,
        int? ExpiresAt,
        int? CancelledAt,
        int? FailedAt,
        int? CompletedAt,
        string Model);

    public static class ThreadRunStatuses {
        public const string Queued = "queued";
        public const string InProgress = "in_progress";
        public const string Completed = "completed";
    }

    private async Task<ThreadCreationResult> CreateThread(CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating a new OpenAi thread");

        using var request = new HttpRequestMessage(HttpMethod.Post, openAiOptions.ThreadsEndpoint);
        request.Headers.Add(openAiOptions.OpenAiBetaHeaderName, openAiOptions.OpenAiBetaHeaderValue);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerSchemaName, openAiOptions.ApiKey);
        request.Content = JsonContent.Create(new object());

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException($"OpenAi call failed. Call type - {nameof(AddMessageToThread)}, response - {response.StatusCode} {response.ReasonPhrase}");
        }

        var responseText = await response.Content.ReadAsStringAsync();
        var responseBody = JsonSerializer.Deserialize<ThreadCreationResult>(responseText, OpenAiJsonSerializerOptions)!;

        ValidateOpenAiResponse(response);

        logger.LogInformation("Successfully created the new OpenAi thread. ThreadId - {threadId}", responseBody.Id);

        return responseBody;
    }

    private async Task AddMessageToThread(string threadId, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Adding a message to the thread. ThreadId - {threadId}", threadId);

        var url = $"{openAiOptions.ThreadsEndpoint}/{threadId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add(openAiOptions.OpenAiBetaHeaderName, openAiOptions.OpenAiBetaHeaderValue);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerSchemaName, openAiOptions.ApiKey);
        request.Content = JsonContent.Create(new
        {
            role = "user",
            content = message 
        });

        var response = await httpClient.SendAsync(request, cancellationToken);
        ValidateOpenAiResponse(response);

        logger.LogInformation("Successfully added a message to the thread. ThreadId - {threadId}", threadId);
    }

    private void ValidateOpenAiResponse(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException($"OpenAi call failed. Response - {response.StatusCode} {response.ReasonPhrase}");
        }
    }

    private record ThreadCreationResult(string Id, string Object, int CreatedAt, ExpandoObject Metadata);

    [HttpPost("execute-in-big-query")]
    public async Task<string> ExecuteBigQuery([FromBody]ExecuteBigQueryRequest request)
    {
        var projectId = "naturaldb-research";

        BigQueryClient client = await BigQueryClient.CreateAsync(projectId);

        BigQueryResults results = await client.ExecuteQueryAsync(request.Sql, null);

        StringBuilder csvResult = new();
        var separator = ',';

        if (results.Any())
        {
            var firstResult = results.First();
            var fieldNames = firstResult.Schema.Fields.Select(f => f.Name);
            var header = string.Join(separator, fieldNames);
            csvResult.AppendLine(header);
        }

        foreach (BigQueryRow row in results)
        {
            var line = string.Join(separator, row.RawRow.F.Select(field => field.V));
            csvResult.AppendLine(line);
        }

        return csvResult.ToString();
    }

    public record ExecuteBigQueryRequest(string Sql);

    public record WhisperRecognition(string Text);

    public record TextToCommandRequest(string Text);
}

