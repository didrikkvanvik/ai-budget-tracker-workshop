using OpenAI.Chat;

namespace BudgetTracker.Api.Infrastructure;

public interface IAzureChatService
{
    Task<string> CompleteChatAsync(string systemPrompt, string userPrompt);

    // Modified to return ChatCompletion and accept optional tools
    Task<ChatCompletion> CompleteChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null);
}