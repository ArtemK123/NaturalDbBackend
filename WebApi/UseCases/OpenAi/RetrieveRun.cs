using MediatR;
using WebApi.Services;
using WebApi.UseCases.OpenAi.Models;

namespace WebApi.UseCases.OpenAi;

public record RetrieveRunRequest(string ThreadId, string RunId) : IRequest<RunModel>;

public sealed class RetrieveRunHandler : IRequestHandler<RetrieveRunRequest, RunModel>
{
    private readonly ILogger<RetrieveRunHandler> logger;
    private readonly OpenAiOptions openAiOptions;
    private readonly IOpenAiMessageSender openAiMessageSender;

    public RetrieveRunHandler(ILogger<RetrieveRunHandler> logger, OpenAiOptions openAiOptions, IOpenAiMessageSender openAiMessageSender)
    {
        this.logger = logger;
        this.openAiOptions = openAiOptions;
        this.openAiMessageSender = openAiMessageSender;
    }

    public async Task<RunModel> Handle(RetrieveRunRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving a run. ThreadId - {threadId}, RunId - {runId}", request.ThreadId, request.RunId);

        var url = $"{openAiOptions.ThreadsEndpoint}/{request.ThreadId}/runs/{request.RunId}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);

        var run = await openAiMessageSender.Send<RunModel>(httpRequest, cancellationToken);

        logger.LogInformation("Retrieved the run. ThreadId - {threadId}, Run - {run}", request.ThreadId, run);
        return run;
    }
}
