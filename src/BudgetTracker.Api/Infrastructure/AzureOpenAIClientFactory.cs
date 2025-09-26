using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Infrastructure;

public class AzureOpenAIClientFactory : IAzureOpenAIClientFactory
{
    private readonly AzureAiConfiguration _configuration;

    public AzureOpenAIClientFactory(IOptions<AzureAiConfiguration> configuration)
    {
        _configuration = configuration.Value;
    }

    public AzureOpenAIClient CreateClient()
    {
        if (string.IsNullOrEmpty(_configuration.Endpoint) || string.IsNullOrEmpty(_configuration.ApiKey))
        {
            throw new InvalidOperationException(
                "Azure AI configuration is missing. Please configure Endpoint and ApiKey.");
        }

        var options = new AzureOpenAIClientOptions();

        foreach (var header in _configuration.Headers)
        {
            options.AddPolicy(new CustomHeaderPolicy(header.Key, header.Value), PipelinePosition.PerCall);
        }

        return new AzureOpenAIClient(
            new Uri(_configuration.Endpoint),
            new Azure.AzureKeyCredential(_configuration.ApiKey),
            options);
    }
}