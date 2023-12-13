using MediatR;
using WebApi.Services;
using WebApi.UseCases.OpenAi.Models;

namespace WebApi.UseCases.OpenAi;

public record RunAssistantRequest(string ThreadId, string AssistantId) : IRequest<RunAssistantResponse>;

public record RunAssistantResponse(RunModel StartedRun);

public sealed class RunAssistantHandler : IRequestHandler<RunAssistantRequest, RunAssistantResponse>
{
    private readonly ILogger<RunAssistantHandler> logger;
    private readonly OpenAiOptions openAiOptions;
    private readonly IOpenAiMessageSender openAiMessageSender;

    public RunAssistantHandler(ILogger<RunAssistantHandler> logger, OpenAiOptions openAiOptions, IOpenAiMessageSender openAiMessageSender)
    {
        this.logger = logger;
        this.openAiOptions = openAiOptions;
        this.openAiMessageSender = openAiMessageSender;
    }

    public async Task<RunAssistantResponse> Handle(RunAssistantRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running OpenAi assistant. ThreadId - {threadId}, AssistantId - {assistantId}", request.ThreadId, request.AssistantId);

        var url = $"{openAiOptions.ThreadsEndpoint}/{request.ThreadId}/runs";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                assistant_id = request.AssistantId,
            })
        };

        var threadRun = await openAiMessageSender.Send<RunModel>(httpRequest, cancellationToken);

        logger.LogInformation(
            "Started the run of OpenAi assistant. ThreadId - {threadId}, AssistantId - {assistantId}, RunId - {runId}",
            request.ThreadId,
            request.AssistantId,
            threadRun.Id);

        var response = new RunAssistantResponse(threadRun);
        return response;
    }
}