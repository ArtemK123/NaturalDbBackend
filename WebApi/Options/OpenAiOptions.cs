namespace WebApi;

public class OpenAiOptions
{
    public const string SectionPath = "OpenAi";

    public string ApiKey { get; set; } = null!;

    public string WhisperEndpoint { get; set; } = null!;

    public string WhisperModel { get; set; } = null!;
}
