using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace BudgetTracker.Api.Infrastructure;

public class AzureChatService : IAzureChatService
{
    private readonly AzureOpenAIClient _openAiClient;
    private readonly string _deploymentName;

    public AzureChatService(
        IAzureOpenAIClientFactory clientFactory,
        IOptions<AzureAiConfiguration> configuration)
    {
        var config = configuration.Value;
        _deploymentName = config.DeploymentName;
        _openAiClient = clientFactory.CreateClient();
    }

    public async Task<string> CompleteChatAsync(string systemPrompt, string userPrompt)
    {
        var client = _openAiClient.GetChatClient(_deploymentName);
        var response = await client.CompleteChatAsync(
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt));

        return response.Value.Content[0].Text;
    }

    public async Task<string> CompleteChatAsync(IEnumerable<ChatMessage> messages)
    {
        var client = _openAiClient.GetChatClient(_deploymentName);
        var response = await client.CompleteChatAsync(messages);
        return response.Value.Content[0].Text;
    }
}