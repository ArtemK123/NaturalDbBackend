using MediatR;
using WebApi.Services;
using WebApi.UseCases.OpenAi.Models;

namespace WebApi.UseCases.OpenAi;

public record RetrieveMessageListRequest(string ThreadId) : IRequest<MessageListModel>;

public class RetrieveMessageListHandler : IRequestHandler<RetrieveMessageListRequest, MessageListModel>
{
    private readonly ILogger<RetrieveMessageListHandler> logger;
    private readonly OpenAiOptions openAiOptions;
    private readonly IOpenAiMessageSender openAiMessageSender;

        public RetrieveMessageListHandler(ILogger<RetrieveMessageListHandler> logger, OpenAiOptions openAiOptions, IOpenAiMessageSender openAiMessageSender)
    {
        this.logger = logger;
        this.openAiOptions = openAiOptions;
        this.openAiMessageSender = openAiMessageSender;
    }

    public async Task<MessageListModel> Handle(RetrieveMessageListRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving a message list. ThreadId - {threadId}", request.ThreadId);

        var url = $"{openAiOptions.ThreadsEndpoint}/{request.ThreadId}/messages";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);

        var messageList = await openAiMessageSender.Send<MessageListModel>(httpRequest, cancellationToken);

        logger.LogInformation("Retrieved the message list. ThreadId - {threadId}, MessageList - {list}", request.ThreadId, messageList);
        return messageList;
    }
}
