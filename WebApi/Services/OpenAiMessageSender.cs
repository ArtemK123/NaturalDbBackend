using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace WebApi.Services;

public interface IOpenAiMessageSender
{
	public Task Send(HttpRequestMessage request, CancellationToken cancellationToken);

	public Task<TResponse> Send<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken);
}

internal sealed class OpenAiMessageSender : IOpenAiMessageSender
{
    private const string BearerSchemaName = "Bearer";

    private readonly HttpClient httpClient;
	private readonly OpenAiOptions openAiOptions;

    public OpenAiMessageSender(IOptions<OpenAiOptions> openAiOptionsProvider, IHttpClientFactory httpClientFactory)
    {
        openAiOptions = openAiOptionsProvider.Value;
		httpClient = httpClientFactory.CreateClient();
    }

    public async Task Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await SendInternal(request, cancellationToken);
    }

    public async Task<TResponse> Send<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await SendInternal(request, cancellationToken);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        options.Converters.Add(new JsonStringEnumConverter());
		var result =  await response.Content.ReadFromJsonAsync<TResponse>(options, cancellationToken);
		return result!;
	}

	private async Task<HttpResponseMessage> SendInternal(HttpRequestMessage request, CancellationToken cancellationToken)
	{
        request.Headers.Add(openAiOptions.OpenAiBetaHeaderName, openAiOptions.OpenAiBetaHeaderValue);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerSchemaName, openAiOptions.ApiKey);

		var response = await httpClient.SendAsync(request, cancellationToken);
        ValidateOpenAiResponse(response);

		return response;
	}

	private void ValidateOpenAiResponse(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
		{
            throw new InvalidOperationException($"OpenAi call failed. Response - {response.StatusCode} {response.ReasonPhrase}");
        }
    }
}
