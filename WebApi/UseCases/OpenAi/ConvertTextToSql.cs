using MediatR;
using WebApi.UseCases.OpenAi.Constants;
using WebApi.UseCases.OpenAi.Models;

namespace WebApi.UseCases.OpenAi;

public record ConvertTextToSqlRequest(string Text) : IRequest<ConvertTextToSqlResponse>;

public record ConvertTextToSqlResponse(string Sql);

public sealed class ConvertTextToSqlHandler : IRequestHandler<ConvertTextToSqlRequest, ConvertTextToSqlResponse>
{
    private readonly ILogger<ConvertTextToSqlHandler> logger;
    private readonly IMediator mediator;
    private readonly OpenAiOptions openAiOptions;

    public ConvertTextToSqlHandler(ILogger<ConvertTextToSqlHandler> logger, IMediator mediator, OpenAiOptions openAiOptions)
    {
        this.logger = logger;
        this.mediator = mediator;
        this.openAiOptions = openAiOptions;
    }

    public async Task<ConvertTextToSqlResponse> Handle(ConvertTextToSqlRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Converting text to sql with OpenAi assistant. Text - {text}, Timeout - {timeout}ms",
            request.Text,
            openAiOptions.TextToSqlTimeoutInSeconds);

        var response = await ConvertTextToSql(request, cancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(openAiOptions.TextToSqlTimeoutInSeconds), cancellationToken);

        logger.LogInformation("Converted text to sql with OpenAi assistant. Text - {text}, Sql - {sql}", request.Text, response.Sql);
        return response;
    }

    private async Task<ConvertTextToSqlResponse> ConvertTextToSql(ConvertTextToSqlRequest request, CancellationToken cancellationToken)
    {
        var createThreadResponse = await mediator.Send(new CreateThreadRequest(), cancellationToken); 
        var threadId = createThreadResponse.ThreadId;

        await mediator.Send(new AddMessageToThreadRequest(threadId, Message: request.Text), cancellationToken);
        
        var runAssistantResponse = await mediator.Send(
            new RunAssistantRequest(threadId, AssistantId: openAiOptions.TextToSqlAssistantId),
            cancellationToken);
        var runId = runAssistantResponse.StartedRun.Id;

        await WaitUntilRunCompletes(threadId: threadId, runId: runId, cancellationToken);

        var messageList = await mediator.Send(new RetrieveMessageListRequest(threadId), cancellationToken);

        var convertedSql = GetContentOfLastMessageFromAssistant(messageList);
        var formattedSql = FormatSql(convertedSql);

        var response = new ConvertTextToSqlResponse(Sql: formattedSql);
        return response;
    }

    private async Task WaitUntilRunCompletes(string threadId, string runId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var currentRunState = await mediator.Send(new RetrieveRunRequest(ThreadId: threadId, RunId: runId), cancellationToken);

            if (currentRunState.Status == RunStatuses.Completed)
            {
                return;
            }

            logger.LogInformation(
                "Run is not completed yet. Waiting {timeout}ms before next check. Current run status - {status}, ThreadId - {threadId} RunId - {runId}",
                openAiOptions.TextToSqlStatusCheckIntervalInMs,
                currentRunState.Status,
                threadId,
                runId);

            await Task.Delay(TimeSpan.FromMilliseconds(openAiOptions.TextToSqlStatusCheckIntervalInMs), cancellationToken);
        }
    }

    private string GetContentOfLastMessageFromAssistant(MessageListModel messageList)
    {
        var lastAssistantMessage = messageList.Data.Last(message => message.Role == MessageRoles.Assistant);
        var lastMessageContent = lastAssistantMessage.Content.Last();
        var result = lastMessageContent.Text.Value;

        return result;
    }

    private string FormatSql(string rawSql)
    {
        var lineBreakSymbols = new [] { '\n', '\r' };
        var formatted = lineBreakSymbols.Aggregate(rawSql, (sql, symbol) => sql.Replace(symbol, ' '));
        return formatted;
    }
}