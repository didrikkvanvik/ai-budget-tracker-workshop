using Azure.AI.OpenAI;

namespace BudgetTracker.Api.Infrastructure;

public interface IAzureOpenAIClientFactory
{
    AzureOpenAIClient CreateClient();
}