using System.Text.Json.Serialization;
using WebApi.UseCases.OpenAi.Constants;

namespace WebApi.UseCases.OpenAi.Models;

public record RunModel(
    string Id,
    string Object,
    int CreatedAt,
    string AssistantId,
    string ThreadId,
    string Status,
    int? StartedAt,
    int? ExpiresAt,
    int? CancelledAt,
    int? FailedAt,
    int? CompletedAt,
    string Model);