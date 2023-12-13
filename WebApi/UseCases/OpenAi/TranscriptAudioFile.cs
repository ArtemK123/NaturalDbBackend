using MediatR;
using WebApi.Services;

namespace WebApi.UseCases.OpenAi.TranslateAudioToText;

public record TranscriptAudioFileRequest(Stream FileStream, string FileName) : IRequest<TranscriptAudioFileResponse>;

public record TranscriptAudioFileResponse(string Text);

public class TranscriptAudioFileHandler : IRequestHandler<TranscriptAudioFileRequest, TranscriptAudioFileResponse>
{
    private readonly ILogger<TranscriptAudioFileHandler> logger;
    private readonly OpenAiOptions openAiOptions;
    private readonly IOpenAiMessageSender openAiMessageSender;

    public TranscriptAudioFileHandler(ILogger<TranscriptAudioFileHandler> logger, OpenAiOptions openAiOptions, IOpenAiMessageSender openAiMessageSender)
    {
        this.logger = logger;
        this.openAiOptions = openAiOptions;
        this.openAiMessageSender = openAiMessageSender;
    }

    public async Task<TranscriptAudioFileResponse> Handle(TranscriptAudioFileRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Transcripting audio file. FileName - {fileName}", request.FileName);

        using var whisperRequest = new HttpRequestMessage(HttpMethod.Post, openAiOptions.WhisperEndpoint);
        whisperRequest.Content = new MultipartFormDataContent
        {
            { new StreamContent(request.FileStream), "file", request.FileName },
            { new StringContent(openAiOptions.WhisperModel), "model" },
            { new StringContent("en"), "language" }
        };

        var recognition = await openAiMessageSender.Send<WhisperRecognition>(whisperRequest, cancellationToken);

        var result = new TranscriptAudioFileResponse(recognition.Text);

        logger.LogInformation("Transripted audio file. FileName - {fileName}, ConvertionResult - {result}", request.FileName, result);
        return result;
    }

    public record WhisperRecognition(string Text);
}
