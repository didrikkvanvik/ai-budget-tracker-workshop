using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public class TransactionEnhancer : ITransactionEnhancer
{
    private readonly IAzureChatService _chatService;
    private readonly ILogger<TransactionEnhancer> _logger;
    public TransactionEnhancer(
        IAzureChatService chatService,
        ILogger<TransactionEnhancer> logger)
    {
        _chatService = chatService;
        _logger = logger;
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
            var systemPrompt = CreateSystemPrompt();
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

    private static string CreateSystemPrompt()
    {
        return """
            You are a transaction description enhancement assistant. Your job is to clean up messy bank transaction descriptions and make them more readable and meaningful for users.

            Guidelines:
            1. Transform cryptic merchant codes and bank jargon into clear, readable descriptions
            2. Remove unnecessary reference numbers, codes, and technical identifiers
            3. Identify the actual merchant or service provider
            4. Maintain accuracy - don't invent information not present in the original
            5. Focus solely on improving description clarity, not on categorization

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
            - "confidenceScore": number between 0-1 indicating confidence in the enhancement

            Be conservative with confidence scores - only use high scores (>0.8) when you're very certain about the merchant identification.
            """;
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
                ExtractJsonFromCodeBlock(content), new JsonSerializerOptions
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

    private static string ExtractJsonFromCodeBlock(string input)
    {
        var match = Regex.Match(input, @"```json\s*([\s\S]*?)\s*```");

        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        throw new FormatException("Could not extract JSON from the input string");
    }
}