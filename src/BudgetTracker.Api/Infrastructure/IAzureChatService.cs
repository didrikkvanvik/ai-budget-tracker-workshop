using OpenAI.Chat;

namespace BudgetTracker.Api.Infrastructure;

public interface IAzureChatService
{
    Task<string> CompleteChatAsync(string systemPrompt, string userPrompt);
    Task<string> CompleteChatAsync(IEnumerable<ChatMessage> messages);
}