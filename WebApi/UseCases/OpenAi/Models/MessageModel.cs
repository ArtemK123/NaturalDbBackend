namespace WebApi.UseCases.OpenAi.Models;

public record MessageModel(string Id, string Role, IReadOnlyCollection<MessagePayload> Content);

public record MessagePayload(string Type, MessagePayloadText Text);

public record MessagePayloadText(string Value);