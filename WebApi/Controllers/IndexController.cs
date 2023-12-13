using MediatR;
using Microsoft.AspNetCore.Mvc;
using WebApi.UseCases.BigQuery;
using WebApi.UseCases.OpenAi;
using WebApi.UseCases.OpenAi.Models;
using WebApi.UseCases.OpenAi.TranslateAudioToText;

namespace WebApi.Controllers;

[ApiController]
[Route("")]
public class NaturalLanguageController : ControllerBase
{
    private readonly IMediator mediator;

    public NaturalLanguageController(IMediator mediator)
    {
        this.mediator = mediator;
    }

    [HttpPost("audio-to-text")]
    public async Task<TranscriptAudioFileResponse> AudioToText(IFormFile file, CancellationToken cancellationToken)
    {
        using var fileStream = file.OpenReadStream();
        var result = await mediator.Send(new TranscriptAudioFileRequest(fileStream, file.FileName), cancellationToken);
        return result;
    }

    [HttpPost("text-to-sql")]
    public async Task<ConvertTextToSqlResponse> TextToSql([FromBody] ConvertTextToSqlRequest request, CancellationToken cancellationToken)
    {
        var response = await mediator.Send(request, cancellationToken);
        return response;
    }

    [HttpPost("get-thread-messages/{threadId}")]
    public async Task<MessageListModel> CheckTread([FromRoute] string threadId, CancellationToken cancellationToken)
    {
        var messageList = await mediator.Send(new RetrieveMessageListRequest(threadId), cancellationToken);
        return messageList;
    }

    [HttpPost("execute-in-big-query")]
    public async Task<ExecuteSqlInBigQueryResponse> ExecuteBigQuery([FromBody]ExecuteSqlInBigQueryRequest request, CancellationToken cancellationToken)
    {
        var response = await mediator.Send(request, cancellationToken);
        return response;
    }
}

