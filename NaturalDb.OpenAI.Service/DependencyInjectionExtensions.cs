using Microsoft.Extensions.DependencyInjection;

namespace NaturalDb.OpenAI.Service;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddOpenAIService(this IServiceCollection services)
    {
        return services;
    }
}