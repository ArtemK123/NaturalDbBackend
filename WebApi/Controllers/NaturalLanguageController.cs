using Microsoft.AspNetCore.Mvc;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace WebApi.Controllers;

[ApiController]
[Route("natural-language")]
public class NaturalLanguageController : ControllerBase
{
    private readonly IConfiguration configuration;
    private readonly ILogger<NaturalLanguageController> logger;

    public NaturalLanguageController(IConfiguration configuration, ILogger<NaturalLanguageController> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
    }

    [HttpPost("audio-to-text")]
    public async Task<string> AudioToText(IFormFile file)
    {
        var requestId = Guid.NewGuid().ToString();
        
        logger.LogInformation($"[{requestId}]: Starting to covert the audio file '{file.FileName}' to text");
        
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
    public string TextToCommand(string text)
    {
        Console.WriteLine(text);
        return text;
    }
}