using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("natural-language")]
public class NaturalLanguageController : ControllerBase
{
    [HttpPost("audio-to-text")]
    public string AudioToText(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var body = reader.ReadToEnd();
        Console.WriteLine(body);
        return "Hello World!";
    }

    [HttpPost("text-to-command")]
    public string TextToCommand(string text)
    {
        Console.WriteLine(text);
        return text;
    }
}