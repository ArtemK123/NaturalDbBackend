using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace WebApi.Controllers;

[ApiController]
[Route("")]
public class NaturalLanguageController : ControllerBase
{
    private readonly IConfiguration configuration;
    private readonly ILogger<NaturalLanguageController> logger;
    private readonly HttpClient httpClient;
    private readonly OpenAiOptions openAiOptions;

    public NaturalLanguageController(IConfiguration configuration, ILogger<NaturalLanguageController> logger, IHttpClientFactory httpClientFactory, IOptions<OpenAiOptions> openAiOptionsProvider)
    {
        this.configuration = configuration;
        this.logger = logger;
        httpClient = httpClientFactory.CreateClient();
        openAiOptions = openAiOptionsProvider.Value;
    }

    [HttpPost("audio-to-text")]
    public async Task<string> AudioToText(IFormFile file, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString();

        logger.LogInformation("[{requestId}]: Starting to covert the audio file '{filename}' to text", requestId, file.FileName);


        using var fileStream = file.OpenReadStream();

        using var whisperRequest = new HttpRequestMessage(HttpMethod.Post, openAiOptions.WhisperEndpoint);
        whisperRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAiOptions.ApiKey);
        whisperRequest.Content = new MultipartFormDataContent
        {
            { new StreamContent(fileStream), "file", file.FileName },
            { new StringContent(openAiOptions.WhisperModel), "model" },
            { new StringContent("en"), "language" }
        };

        var whisperResponse = await httpClient.SendAsync(whisperRequest, cancellationToken);

        if (!whisperResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to call the Whisper model: '${whisperResponse.StatusCode} {whisperResponse.ReasonPhrase}'");
        }

        var recognition = (await whisperResponse.Content.ReadFromJsonAsync<WhisperRecognition>(cancellationToken))!;

        logger.LogInformation("[{requestId}]: Successfully converted audio to text. Converted text: {text}", requestId, recognition.Text);
        return recognition.Text;
    }

    public record WhisperRecognition(string Text);

    [HttpPost("text-to-command")]
    public async Task<string> TextToCommand([FromBody] TextToCommandRequest request)
    {
        var requestId = Guid.NewGuid().ToString();
        logger.LogInformation($"[{requestId}]: Starting to covert the text to command. Text: {request.Text}");
        
        var openAiConfig = configuration.GetSection("OpenAI");

        using var openAiRequest = new HttpRequestMessage(HttpMethod.Post, openAiConfig.GetValue<string>("Endpoint"));
        openAiRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAiConfig.GetValue<string>("ApiKey"));
        openAiRequest.Content = JsonContent.Create(new
        {
            model = openAiConfig.GetValue<string>("Model"),
            prompt = request.Text,
            temperature = openAiConfig.GetValue<double>("Temperature"),
            max_tokens = openAiConfig.GetValue<int>("MaxTokens"),
            top_p = 1.0,
            frequency_penalty = 0.0,
            presence_penalty = 0.0
        });
        
        var openAiResponse = await httpClient.SendAsync(openAiRequest);
        var responseText = await openAiResponse.Content.ReadAsStringAsync();
        var responseModel = JsonSerializer.Deserialize<ResponseModel>(responseText);

        var result = responseModel?.choices[0].text ?? "Sorry, I didn't understand that.";
        
        logger.LogInformation($"[{requestId}]: Converted command: ${result}");
        return result;
    }

    public record TextToCommandRequest(string Text);
    
    record Choice(string text);

    record ResponseModel(Choice[] choices);
}

