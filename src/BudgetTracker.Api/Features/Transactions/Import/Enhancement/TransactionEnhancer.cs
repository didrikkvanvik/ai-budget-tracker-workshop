using System.Diagnostics;
using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public class TransactionEnhancer : ITransactionEnhancer
{
    private readonly IAzureChatService _chatService;
    private readonly ILogger<TransactionEnhancer> _logger;
    private readonly BudgetTrackerContext _context; // Add database context

    // RAG Configuration Constants
    private const int DefaultContextLimit = 25; // Number of context transactions to retrieve
    private const int ContextWindowDays = 365;  // Time window for context retrieval

    public TransactionEnhancer(
        IAzureChatService chatService,
        ILogger<TransactionEnhancer> logger,
        BudgetTrackerContext context) // Inject database context
    {
        _chatService = chatService;
        _logger = logger;
        _context = context;
    }

    public async Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
        List<string> descriptions,
        string account,
        string userId,
        string? currentImportSessionHash = null)
    {
        if (!descriptions.Any())
            return new List<EnhancedTransactionDescription>();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Always retrieve recent transactions for context (empty list if none exist)
            // Exclude current import session to avoid using uncategorized transactions as context
            var contextTransactions = await GetRecentTransactionsAsync(userId, account, DefaultContextLimit, currentImportSessionHash);

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

    private async Task<List<Transaction>> GetRecentTransactionsAsync(
        string userId,
        string account,
        int limit,
        string? excludeImportSessionHash = null)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-ContextWindowDays);

        var query = _context.Transactions
            .Where(t => t.UserId == userId && t.Account == account && t.Date >= cutoffDate)
            .Where(t => !string.IsNullOrEmpty(t.Category)); // Only include categorized transactions

        // Exclude current import session to avoid using uncategorized transactions as context
        if (!string.IsNullOrEmpty(excludeImportSessionHash))
        {
            query = query.Where(t => t.ImportSessionHash != excludeImportSessionHash);
        }

        return await query
            .OrderByDescending(t => t.Date)
            .Take(limit)
            .ToListAsync();
    }

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
            var contextSection = "\n\nHISTORICAL CONTEXT for this account:\n";
            contextSection += string.Join("\n", contextTransactions.Select(t =>
                $"- \"{t.Description}\" → Amount: {t.Amount:C} → Category: \"{t.Category}\""));

            contextSection += "\n\nUse these patterns to inform your categorization decisions for new transactions.";

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

    private static string CreateUserPrompt(List<string> descriptions)
    {
        var descriptionsJson = JsonSerializer.Serialize(descriptions);
        return $"Please enhance these transaction descriptions:\n{descriptionsJson}";
    }

    private List<EnhancedTransactionDescription> ParseEnhancedDescriptions(string content,
        List<string> originalDescriptions)
    {
        try
        {
            var enhancedDescriptions = JsonSerializer.Deserialize<List<EnhancedTransactionDescription>>(
                content.ExtractJsonFromCodeBlock(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (enhancedDescriptions?.Count == originalDescriptions.Count)
            {
                return enhancedDescriptions;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON: {Content}", content);
        }

        _logger.LogWarning("AI response format was invalid, returning original descriptions");
        return originalDescriptions.Select(d => new EnhancedTransactionDescription
        {
            OriginalDescription = d,
            EnhancedDescription = d,
            ConfidenceScore = 0.0
        }).ToList();
    }

}