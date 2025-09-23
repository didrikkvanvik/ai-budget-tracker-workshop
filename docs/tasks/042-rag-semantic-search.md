# Workshop Step 042: RAG-Enhanced Transaction Categorization with Embeddings

## Mission 🎯

In this step, you'll enhance the existing AI transaction categorization system by integrating semantic search capabilities using RAG (Retrieval-Augmented Generation). Instead of using only recent transactions for context, the system will find semantically similar transactions using vector embeddings to provide better categorization patterns.

**Your goal**: Transform the basic transaction enhancer to use semantic similarity for finding relevant historical context, improving categorization accuracy through intelligent pattern matching.

**Learning Objectives**:
- Integrating semantic search into existing AI workflows
- Using RAG patterns to improve categorization accuracy
- Combining vector similarity with traditional filtering
- Enhancing AI prompts with semantically relevant context
- Building robust fallback mechanisms for production systems

---

## Prerequisites

Before starting, ensure you completed:
- [041-rag.md](041-rag.md) - Basic semantic search infrastructure

You should have:
- Working semantic search infrastructure with embeddings
- Basic TransactionEnhancer functionality
- Azure OpenAI integration configured

---

## Branches

**Starting branch:** `041-rag`
**Solution branch:** `042-rag-semantic-search`

---

## Step 42.1: Add Embedding Service Dependency

*Inject the embedding service into the TransactionEnhancer for semantic operations.*

The current TransactionEnhancer only uses recent transactions for context. We need to add semantic search capabilities to find more relevant historical transactions.

Update the constructor in `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/TransactionEnhancer.cs`:

```csharp
using System.Diagnostics;
using System.Text.Json;
using BudgetTracker.Api.Features.Intelligence.Search;  // Add this import
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public class TransactionEnhancer : ITransactionEnhancer
{
    private readonly IAzureChatService _chatService;
    private readonly IAzureEmbeddingService _embeddingService;  // Add embedding service
    private readonly ILogger<TransactionEnhancer> _logger;
    private readonly BudgetTrackerContext _context;

    // RAG Configuration Constants
    private const int DefaultContextLimit = 25;
    private const int ContextWindowDays = 365;

    public TransactionEnhancer(
        IAzureChatService chatService,
        IAzureEmbeddingService embeddingService,  // Add parameter
        ILogger<TransactionEnhancer> logger,
        BudgetTrackerContext context)
    {
        _chatService = chatService;
        _embeddingService = embeddingService;  // Assign field
        _logger = logger;
        _context = context;
    }

    // ... rest of the class remains the same for now
}
```

## Step 42.2: Update Method Signature

*Modify the enhancement method to require session hash for better filtering.*

The current method has an optional session hash parameter, but for semantic search we need it to be required to properly exclude the current import session.

Update the `EnhanceDescriptionsAsync` method signature:

```csharp
public async Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
    List<string> descriptions,
    string account,
    string userId,
    string currentImportSessionHash)  // Remove nullable, make required
{
    if (!descriptions.Any())
        return new List<EnhancedTransactionDescription>();

    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Get semantically similar transactions for context
        var contextTransactions = await GetSemanticContextTransactionsAsync(descriptions, userId, account,
            DefaultContextLimit, currentImportSessionHash);

        _logger.LogInformation("Retrieved {ContextCount} context transactions for account {Account}",
            contextTransactions.Count, account);

        // Always create enhanced system prompt with available context
        var systemPrompt = CreateEnhancedSystemPrompt(contextTransactions);
        var userPrompt = CreateUserPrompt(descriptions);

        var content = await _chatService.CompleteChatAsync(systemPrompt, userPrompt);
        var results = ParseEnhancedDescriptions(content, descriptions);

        _logger.LogInformation("AI processing completed in {ProcessingTime}ms", stopwatch.ElapsedMilliseconds);

        return results;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to enhance transaction descriptions");
        return descriptions.Select(d => new EnhancedTransactionDescription
        {
            OriginalDescription = d,
            EnhancedDescription = d,
            ConfidenceScore = 0.0
        }).ToList();
    }
}
```

## Step 42.3: Replace Context Retrieval with Semantic Search

*Replace the basic recent transaction retrieval with semantic similarity search.*

Remove the old `GetRecentTransactionsAsync` method and replace it with a new semantic search method.

Replace the existing context retrieval method:

```csharp
private async Task<List<Transaction>> GetSemanticContextTransactionsAsync(
    List<string> descriptions,
    string userId,
    string account,
    int limit,
    string excludeImportSessionHash)
{
    try
    {
        // Combine all descriptions into a single query for embedding
        var combinedQuery = string.Join(" ", descriptions.Take(5)); // Limit to avoid token overflow

        // Generate embedding for the combined descriptions
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(combinedQuery);
        var vectorString = queryEmbedding.ToString();

        var cutoffDate = DateTime.UtcNow.AddDays(-ContextWindowDays);

        // Build the base query conditions
        var conditions = new List<string>
        {
            "\"Embedding\" IS NOT NULL",
            "\"UserId\" = {0}",
            "\"Account\" = {1}",
            "\"Date\" >= {2}",
            "\"Category\" IS NOT NULL AND \"Category\" != ''",
            "\"ImportSessionHash\" != {3}",
        };

        var parameters = new List<object> { userId, account, cutoffDate, excludeImportSessionHash, vectorString, limit };

        var whereClause = string.Join(" AND ", conditions);

        // Use semantic similarity with cosine distance, but also factor in recency
        var similarTransactions = await _context.Transactions
            .FromSqlRaw($@"
                SELECT *
                FROM ""Transactions""
                WHERE {whereClause}
                 AND cosine_distance(""Embedding"",
                  {{4}}::vector) < 0.6
                ORDER BY cosine_distance(""Embedding"", {{4}}::vector) ASC,
                         ""Date"" DESC
                LIMIT {{5}}",
                parameters.ToArray())
            .ToListAsync();

        _logger.LogInformation("Found {Count} semantically similar context transactions for enhancement",
            similarTransactions.Count);

        return similarTransactions;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to get semantic context, falling back to recent transactions");

        // Fallback to empty list - better to proceed without context than fail
        return new List<Transaction>();
    }
}
```

## Step 42.4: Update System Prompt for Semantic Context

*Enhance the AI system prompt to leverage semantically similar transactions.*

Update the `CreateEnhancedSystemPrompt` method to reflect that we're using semantic similarity:

```csharp
private string CreateEnhancedSystemPrompt(List<Transaction> contextTransactions)
{
    var basePrompt = """
                     You are a transaction categorization assistant. Your job is to clean up messy bank transaction descriptions and make them more readable and meaningful for users.

                     Guidelines:
                     1. Transform cryptic merchant codes and bank jargon into clear, readable descriptions
                     2. Remove unnecessary reference numbers, codes, and technical identifiers
                     3. Identify the actual merchant or service provider
                     4. Suggest appropriate spending categories when possible
                     5. Maintain accuracy - don't invent information not present in the original
                     """;

    if (contextTransactions.Any())
    {
        var contextSection = "\n\nSIMILAR TRANSACTIONS for this account:\n";
        contextSection += string.Join("\n", contextTransactions.Select(t =>
            $"- \"{t.Description}\" → Amount: {t.Amount:C} → Category: \"{t.Category}\"").Distinct());

        contextSection +=
            "\n\nThese transactions were selected based on semantic similarity to the new transactions being processed.";
        contextSection +=
            "\nUse these patterns to inform your categorization decisions, paying special attention to:";
        contextSection += "\n- Similar merchant names or transaction types";
        contextSection += "\n- Comparable amount ranges for similar categories";
        contextSection += "\n- Established categorization patterns for this user";

        basePrompt += contextSection;
    }

    basePrompt += """

                  Examples:
                  - "AMZN MKTP US*123456789" → "Amazon Marketplace Purchase"
                  - "STARBUCKS COFFEE #1234" → "Starbucks Coffee"
                  - "SHELL OIL #4567" → "Shell Gas Station"
                  - "DD VODAFONE PORTU 222111000 PT00110011" → "Vodafone Portugal - Direct Debit"
                  - "COMPRA 0000 TEMU.COM DUBLIN" → "Temu Online Purchase"
                  - "TRF MB WAY P/ Manuel Silva" → "MB WAY Transfer to Manuel Silva"

                  Respond with a JSON array where each object has:
                  - "originalDescription": the input description
                  - "enhancedDescription": the cleaned description
                  - "suggestedCategory": optional category (e.g., "Groceries", "Entertainment", "Transportation", "Utilities", "Shopping", "Food & Drink", "Gas & Fuel", "Transfer")
                  - "confidenceScore": number between 0-1 indicating confidence in the enhancement

                  Be conservative with confidence scores - only use high scores (>0.8) when you're very certain about the merchant identification.
                  """;

    return basePrompt;
}
```

## Step 42.5: Remove Unused Methods

*Clean up the codebase by removing methods that are no longer needed.*

Remove the old `CreateSystemPrompt()` and `GetRecentTransactionsAsync()` methods since they're replaced by semantic versions:

```csharp
// Remove these methods entirely:
// - private static string CreateSystemPrompt()
// - private async Task<List<Transaction>> GetRecentTransactionsAsync(...)

// Keep only:
// - CreateEnhancedSystemPrompt (updated in previous step)
// - GetSemanticContextTransactionsAsync (added in step 42.3)
```


## Step 42.6: Update Interface

*Ensure the interface matches the new method signature.*

Check and update `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/ITransactionEnhancer.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public interface ITransactionEnhancer
{
    Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
        List<string> descriptions,
        string account,
        string userId,
        string currentImportSessionHash);  // Ensure this parameter is required, not nullable
}
```

## Step 42.8: Update Import Logic

*Ensure all calls to the TransactionEnhancer provide the required session hash.*

Update any places that call the TransactionEnhancer to provide the session hash. In `src/BudgetTracker.Api/Features/Transactions/Import/ImportApi.cs`, ensure the enhancement call includes the session hash:

```csharp
// In the import logic, make sure you pass the session hash:
var enhancedDescriptions = await enhancer.EnhanceDescriptionsAsync(
    descriptions,
    account,
    userId,
    sessionHash);  // Make sure this is passed and not null
```

### 42.9: Verify Improved Categorization

Compare the categorization results with and without semantic context:

**Expected Improvements:**
- ✅ **Coffee transactions** should be consistently categorized as "Food & Drink" based on previous coffee purchases
- ✅ **Amazon transactions** should be categorized consistently with previous Amazon purchases
- ✅ **Similar merchants** should get similar categories based on historical patterns
- ✅ **Amount patterns** should influence categorization (small coffee vs large electronics purchases)

## Summary ✅

You've successfully enhanced the transaction categorization system with semantic search capabilities:

✅ **Semantic Context Retrieval**: RAG-enhanced context finding using vector similarity
✅ **Improved Categorization**: AI decisions based on semantically similar historical transactions
✅ **Robust Fallback**: Graceful handling when semantic search fails
✅ **Enhanced Prompting**: AI prompts enriched with relevant historical patterns
✅ **Production Ready**: Proper error handling and logging for production deployment

**Key Features Implemented**:
- **Semantic Similarity**: Find relevant transactions using vector embeddings, not just recency
- **Combined Ranking**: Order results by both semantic similarity and recency
- **Smart Context**: Only use transactions with existing categories as learning examples
- **Fallback Mechanisms**: Continue operation even if embedding service fails
- **Configurable Thresholds**: Tunable similarity thresholds and context limits

**Technical Achievements**:
- **RAG Integration**: Successful integration of Retrieval-Augmented Generation patterns
- **Vector Query Optimization**: Efficient PostgreSQL queries with pgvector operations
- **Context Filtering**: Smart exclusion of current import session and uncategorized transactions
- **Performance Monitoring**: Detailed logging for debugging and optimization
- **Error Resilience**: Robust error handling that doesn't break the import flow

**What Users Get**:
- **Smarter Categorization**: AI learns from semantically similar past transactions
- **Consistent Patterns**: Similar merchants get consistent category assignments
- **Better Accuracy**: Historical context improves categorization decisions
- **Faster Processing**: Pre-filtered context reduces AI processing overhead
- **Reliable Operation**: System continues working even with partial service failures

**Before vs After**:
- **Before**: Used only recent transactions for context, regardless of relevance
- **After**: Uses semantically similar transactions that match the patterns being processed
- **Result**: More accurate categorization with better pattern recognition

The enhanced system now provides intelligent, context-aware transaction categorization that learns from semantically relevant historical data! 🎉