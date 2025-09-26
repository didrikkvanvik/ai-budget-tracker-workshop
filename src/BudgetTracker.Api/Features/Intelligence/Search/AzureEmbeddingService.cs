using Azure.AI.OpenAI;
using BudgetTracker.Api.Infrastructure;
using OpenAI.Embeddings;
using Pgvector;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public class AzureEmbeddingService : IAzureEmbeddingService
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly ILogger<AzureEmbeddingService> _logger;

    // Use text-embedding-3-small for cost efficiency (1536 dimensions)
    private const string EmbeddingModel = "text-embedding-3-small";
    private const int MaxBatchSize = 100; // Azure OpenAI batch limit

    public AzureEmbeddingService(
        IAzureOpenAIClientFactory clientFactory,
        ILogger<AzureEmbeddingService> logger)
    {
        _logger = logger;
        _openAIClient = clientFactory.CreateClient();
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        try
        {
            var client = _openAIClient.GetEmbeddingClient(EmbeddingModel);
            var response = await client.GenerateEmbeddingAsync(text);

            var embeddingValues = response.Value.ToFloats().ToArray();
            return new Vector(embeddingValues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text: {Text}", text[..Math.Min(text.Length, 50)]);
            throw;
        }
    }

    public async Task<Vector> GenerateTransactionEmbeddingAsync(string description, string? category = null)
    {
        // Combine description and category for richer semantic representation
        var text = string.IsNullOrEmpty(category)
            ? description
            : $"{description} [{category}]";

        return await GenerateEmbeddingAsync(text);
    }
}