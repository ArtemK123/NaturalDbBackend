namespace WebApi;

public class OpenAiOptions
{
    public const string SectionPath = "OpenAi";

    public string ApiKey { get; set; } = null!;

    public string OpenAiBetaHeaderName { get; set; } = null!;

    public string OpenAiBetaHeaderValue { get; set; } = null!;

    public string ThreadsEndpoint { get; set; } = null!;

    public string TextToSqlAssistantId { get; set; } = null!;

    public string WhisperEndpoint { get; set; } = null!;

    public string WhisperModel { get; set; } = null!;
}
