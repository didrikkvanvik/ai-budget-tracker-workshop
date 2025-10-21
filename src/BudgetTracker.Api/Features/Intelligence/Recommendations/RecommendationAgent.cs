using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

internal class GeneratedRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public RecommendationType Type { get; set; }
    public RecommendationPriority Priority { get; set; }
}

internal class BasicStats
{
    public int TransactionCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public List<string> TopCategories { get; set; } = new();
}

public class RecommendationAgent : IRecommendationRepository
{
    private readonly BudgetTrackerContext _context;
    private readonly IAzureChatService _chatService;
    private readonly ILogger<RecommendationAgent> _logger;

    public RecommendationAgent(
        BudgetTrackerContext context,
        IAzureChatService chatService,
        ILogger<RecommendationAgent> logger)
    {
        _context = context;
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<List<Recommendation>> GetActiveRecommendationsAsync(string userId)
    {
        return await _context.Recommendations
            .Where(r => r.UserId == userId &&
                       r.Status == RecommendationStatus.Active &&
                       r.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.GeneratedAt)
            .Take(5)
            .ToListAsync();
    }

    public async Task GenerateRecommendationsAsync(string userId)
    {
        try
        {
            // 1. Check if we need to regenerate
            var lastGenerated = await _context.Recommendations
                .Where(r => r.UserId == userId)
                .MaxAsync(r => (DateTime?)r.GeneratedAt);

            var lastImported = await _context.Transactions
                .Where(t => t.UserId == userId)
                .MaxAsync(t => (DateTime?)t.ImportedAt);

            // Skip if no new transactions since last generation (within 1 minute for dev testing)
            if (lastGenerated.HasValue && lastImported.HasValue &&
                lastGenerated > lastImported.Value.AddMinutes(-1)) // DEMO
            {
                _logger.LogInformation("Skipping generation - no new data for user {UserId}", userId);
                return;
            }

            // 2. Check minimum transaction count
            var transactionCount = await _context.Transactions
                .Where(t => t.UserId == userId)
                .CountAsync();

            if (transactionCount < 5)
            {
                _logger.LogInformation("Insufficient transaction data for user {UserId}", userId);
                return;
            }

            // 3. Get basic statistics
            var basicStats = await GetBasicStatsAsync(userId);

            // 4. Generate recommendations with AI
            var recommendations = await GenerateSimpleRecommendationsAsync(basicStats);

            // 5. Store recommendations
            await StoreRecommendationsAsync(userId, recommendations);

            _logger.LogInformation("Generated {Count} recommendations for user {UserId}",
                recommendations.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate recommendations for user {UserId}", userId);
        }
    }

    private async Task<BasicStats> GetBasicStatsAsync(string userId)
    {
        var transactions = await _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Date)
            .Take(1000)
            .ToListAsync();

        if (!transactions.Any())
        {
            return new BasicStats();
        }

        return new BasicStats
        {
            TransactionCount = transactions.Count,
            TotalIncome = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount),
            TotalExpenses = Math.Abs(transactions.Where(t => t.Amount < 0).Sum(t => t.Amount)),
            DateRange = $"{transactions.Min(t => t.Date):yyyy-MM-dd} to {transactions.Max(t => t.Date):yyyy-MM-dd}",
            TopCategories = transactions
                .Where(t => t.Amount < 0 && !string.IsNullOrEmpty(t.Category))
                .GroupBy(t => t.Category)
                .OrderByDescending(g => Math.Abs(g.Sum(t => t.Amount)))
                .Take(5)
                .Select(g => g.Key!)
                .ToList()
        };
    }

    private async Task<List<GeneratedRecommendation>> GenerateSimpleRecommendationsAsync(BasicStats stats)
    {
        var systemPrompt = """
            You are a financial assistant providing general recommendations based on high-level transaction statistics.

            Generate 3-5 actionable financial recommendations in JSON format:
            {
              "recommendations": [
                {
                  "title": "Brief, attention-grabbing title",
                  "message": "Actionable recommendation based on the statistics provided",
                  "type": "SpendingAlert|SavingsOpportunity|BehavioralInsight|BudgetWarning",
                  "priority": "Low|Medium|High|Critical"
                }
              ]
            }

            Make recommendations:
            - GENERAL: Based on overall spending patterns
            - ACTIONABLE: Clear next steps users can take
            - RELEVANT: Focus on income/expense balance and top spending categories
            """;

        var userPrompt = $"""
            Based on these high-level statistics, provide 3-5 financial recommendations:

            - Total Income: ${stats.TotalIncome:F2}
            - Total Expenses: ${stats.TotalExpenses:F2}
            - Net: ${stats.TotalIncome - stats.TotalExpenses:F2}
            - Transaction Count: {stats.TransactionCount}
            - Date Range: {stats.DateRange}
            - Top Spending Categories: {string.Join(", ", stats.TopCategories)}

            Provide helpful, general financial advice based on these statistics.
            """;

        try
        {
            var response = await _chatService.CompleteChatAsync(systemPrompt, userPrompt);
            return ParseRecommendations(response.ExtractJsonFromCodeBlock());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI recommendations");
            return new List<GeneratedRecommendation>();
        }
    }

    private List<GeneratedRecommendation> ParseRecommendations(string response)
    {
        try
        {
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response);
            var recommendations = new List<GeneratedRecommendation>();

            if (jsonResponse.TryGetProperty("recommendations", out var recsArray))
            {
                foreach (var rec in recsArray.EnumerateArray())
                {
                    if (rec.TryGetProperty("title", out var title) &&
                        rec.TryGetProperty("message", out var message) &&
                        rec.TryGetProperty("type", out var type) &&
                        rec.TryGetProperty("priority", out var priority))
                    {
                        recommendations.Add(new GeneratedRecommendation
                        {
                            Title = title.GetString() ?? "",
                            Message = message.GetString() ?? "",
                            Type = Enum.TryParse<RecommendationType>(type.GetString(), out var t) ? t : RecommendationType.BehavioralInsight,
                            Priority = Enum.TryParse<RecommendationPriority>(priority.GetString(), out var p) ? p : RecommendationPriority.Medium
                        });
                    }
                }
            }

            return recommendations.Take(5).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response: {Response}", response);
            return new List<GeneratedRecommendation>();
        }
    }

    private async Task StoreRecommendationsAsync(string userId, List<GeneratedRecommendation> aiRecommendations)
    {
        if (!aiRecommendations.Any()) return;

        // Expire old active recommendations
        var oldRecommendations = await _context.Recommendations
            .Where(r => r.UserId == userId && r.Status == RecommendationStatus.Active)
            .ToListAsync();

        foreach (var old in oldRecommendations)
        {
            old.Status = RecommendationStatus.Expired;
        }

        // Add new recommendations
        var newRecommendations = aiRecommendations.Select(ai => new Recommendation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = ai.Title,
            Message = ai.Message,
            Type = ai.Type,
            Priority = ai.Priority,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = RecommendationStatus.Active
        }).ToList();

        await _context.Recommendations.AddRangeAsync(newRecommendations);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Stored {Count} recommendations for user {UserId}", newRecommendations.Count, userId);
    }
}
