namespace WebApi.UseCases.OpenAi.Models;

public record MessageListModel(IReadOnlyCollection<MessageModel> Data, string FirstId, string LastId, bool HasMore);
