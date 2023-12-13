using System.Dynamic;
using MediatR;
using WebApi.Services;

namespace WebApi.UseCases.OpenAi;

public record CreateThreadRequest : IRequest<CreateThreadResponse>;

public record CreateThreadResponse(string ThreadId);

public sealed class CreateThreadHandler : IRequestHandler<CreateThreadRequest, CreateThreadResponse>
{
    private readonly ILogger<CreateThreadHandler> logger;
    private readonly OpenAiOptions openAiOptions;
    private readonly IOpenAiMessageSender openAiMessageSender;

    public CreateThreadHandler(ILogger<CreateThreadHandler> logger, OpenAiOptions openAiOptions, IOpenAiMessageSender openAiMessageSender)
    {
        this.logger = logger;
        this.openAiOptions = openAiOptions;
        this.openAiMessageSender = openAiMessageSender;
    }

    public async Task<CreateThreadResponse> Handle(CreateThreadRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating OpenAi thread");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, openAiOptions.ThreadsEndpoint);
        httpRequest.Content = JsonContent.Create(new object());

        var creationModel = await openAiMessageSender.Send<ThreadCreationModel>(httpRequest, cancellationToken);

        var response = new CreateThreadResponse(ThreadId: creationModel.Id);
        logger.LogInformation("Created OpenAi thread. ThreadId - {threadId}", creationModel.Id);

        return response;
    }

    internal sealed record ThreadCreationModel(string Id, string Object, int CreatedAt, ExpandoObject Metadata);
}
