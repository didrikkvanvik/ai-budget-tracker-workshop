using Pgvector;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public interface IAzureEmbeddingService
{
    /// <summary>
    /// Generate embedding for a single text input
    /// </summary>
    Task<Vector> GenerateEmbeddingAsync(string text);

    /// <summary>
    /// Generate embedding specifically for transaction description and category
    /// </summary>
    Task<Vector> GenerateTransactionEmbeddingAsync(string description, string? category = null);
}