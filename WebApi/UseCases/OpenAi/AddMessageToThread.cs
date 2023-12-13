using MediatR;
using WebApi.Services;

namespace WebApi.UseCases.OpenAi;

public record AddMessageToThreadRequest(string ThreadId, string Message) : IRequest;

public sealed class AddMessageToThreadHandler : IRequestHandler<AddMessageToThreadRequest>
{
    private readonly ILogger<AddMessageToThreadHandler> logger;
    private readonly OpenAiOptions openAiOptions;
    private readonly IOpenAiMessageSender openAiMessageSender;

    public AddMessageToThreadHandler(ILogger<AddMessageToThreadHandler> logger, OpenAiOptions openAiOptions, IOpenAiMessageSender openAiMessageSender)
    {
        this.logger = logger;
        this.openAiOptions = openAiOptions;
        this.openAiMessageSender = openAiMessageSender;
    }

    public async Task Handle(AddMessageToThreadRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Adding a message to the OpenAi thread. ThreadId - {threadId}, Message - {message}", request.ThreadId, request.Message);

        var url = $"{openAiOptions.ThreadsEndpoint}/{request.ThreadId}/messages";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                role = "user",
                content = request.Message 
            })
        };

        await openAiMessageSender.Send(httpRequest, cancellationToken);
        logger.LogInformation("Added a message to the OpenAi thread. ThreadId - {threadId}, Message - {message}", request.ThreadId, request.Message);
    }
}
