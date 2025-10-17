using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace BudgetTracker.Api.Infrastructure;

public class AzureChatService : IAzureChatService
{
    private readonly AzureOpenAIClient _openAiClient;
    private readonly string _deploymentName;
    private readonly ILogger<AzureChatService> _logger;

    public AzureChatService(
        IAzureOpenAIClientFactory clientFactory,
        IOptions<AzureAiConfiguration> configuration,
        ILogger<AzureChatService> logger)
    {
        var config = configuration.Value;
        _deploymentName = config.DeploymentName;
        _openAiClient = clientFactory.CreateClient();
        _logger = logger;
    }

    public async Task<string> CompleteChatAsync(string systemPrompt, string userPrompt)
    {
        var client = _openAiClient.GetChatClient(_deploymentName);
        var response = await client.CompleteChatAsync(
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt));

        return response.Value.Content[0].Text;
    }

    // Modified to return ChatCompletion and accept optional tools
    public async Task<ChatCompletion> CompleteChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null)
    {
        try
        {
            var client = _openAiClient.GetChatClient(_deploymentName);

            if (options?.Tools.Count > 0)
            {
                _logger.LogDebug("Calling chat completion with {ToolCount} tools",
                    options.Tools.Count);
            }

            var response = await client.CompleteChatAsync(messages, options);

            if (options?.Tools.Count > 0)
            {
                _logger.LogDebug("Chat completion finished: {FinishReason}, {ToolCallCount} tool calls",
                    response.Value.FinishReason,
                    response.Value.ToolCalls.Count);
            }

            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing chat");
            throw;
        }
    }
}