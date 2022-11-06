using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace WebApi.Controllers;

[ApiController]
[Route("")]
public class NaturalLanguageController : ControllerBase
{
    private readonly IConfiguration configuration;
    private readonly ILogger<NaturalLanguageController> logger;
    private readonly HttpClient httpClient;

    public NaturalLanguageController(IConfiguration configuration, ILogger<NaturalLanguageController> logger, IHttpClientFactory httpClientFactory)
    {
        this.configuration = configuration;
        this.logger = logger;
        httpClient = httpClientFactory.CreateClient();
    }

    [HttpPost("audio-to-text")]
    public async Task<string> AudioToText(IFormFile file)
    {
        var requestId = Guid.NewGuid().ToString();
        
        logger.LogInformation($"[{requestId}]: Starting to covert the audio file '{file.FileName}' to text");
        
        if (file.ContentType != "audio/wav")
        {
            logger.LogError($"[{requestId}]: The file '{file.FileName}' is not a WAV file");
            return "The file is not a WAV file";
        }
        
        var tempFileName = Path.GetTempPath() + Guid.NewGuid() + ".wav";
        await using (var fileStream = new FileStream(tempFileName, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }
        
        var speechConfigSectionPath = "Azure:CognitiveServices:Speech";
        var speechApiKey = configuration.GetSection(speechConfigSectionPath).GetValue<string>("ApiKey");
        var speechRegion = configuration.GetSection(speechConfigSectionPath).GetValue<string>("Region");

        var speechConfig = SpeechConfig.FromSubscription(speechApiKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";
        
        using var audioInputStream = AudioInputStream.CreatePushStream();
        using var audioConfig = AudioConfig.FromWavFileInput(tempFileName);
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
     
        var result = await recognizer.RecognizeOnceAsync();

        logger.LogInformation($"[{requestId}]: Converted text: {result.Text}");
        return result.Text;
    }

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

