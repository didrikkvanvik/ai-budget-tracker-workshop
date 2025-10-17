using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Infrastructure.Extensions;
using BudgetTracker.Api.Features.Intelligence.Tools;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

internal class GeneratedRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public RecommendationType Type { get; set; }
    public RecommendationPriority Priority { get; set; }
}

public class RecommendationAgent : IRecommendationRepository
{
    private readonly BudgetTrackerContext _context;
    private readonly IAzureChatService _chatService;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<RecommendationAgent> _logger;

    public RecommendationAgent(
        BudgetTrackerContext context,
        IAzureChatService chatService,
        IToolRegistry toolRegistry,
        ILogger<RecommendationAgent> logger)
    {
        _context = context;
        _chatService = chatService;
        _toolRegistry = toolRegistry;
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

            // Skip if no new transactions since last generation (within 2 minute for dev testing)
            if (lastGenerated.HasValue && lastImported.HasValue &&
                lastGenerated > lastImported.Value.AddMinutes(-2))
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

            // 3. Run agentic recommendation generation
            var recommendations = await GenerateAgenticRecommendationsAsync(userId, maxIterations: 5);

            if (!recommendations.Any())
            {
                _logger.LogInformation("Agent generated no recommendations for {UserId}", userId);
                return;
            }

            // 4. Store recommendations
            await StoreRecommendationsAsync(userId, recommendations);

            _logger.LogInformation("Generated {Count} recommendations for user {UserId}",
                recommendations.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate recommendations for user {UserId}", userId);
        }
    }

    private async Task<List<GeneratedRecommendation>> GenerateAgenticRecommendationsAsync(
        string userId,
        int maxIterations)
    {
        // Initialize conversation
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(CreateSystemPrompt()),
            new UserChatMessage(CreateInitialUserPrompt())
        };

        // Prepare tools
        var tools = _toolRegistry.ToChatTools();
        var options = new ChatCompletionOptions();
        foreach (var tool in tools)
        {
            options.Tools.Add(tool);
        }

        _logger.LogInformation("Agent started for user {UserId}", userId);

        // Multi-turn agent loop
        var iteration = 0;
        while (iteration < maxIterations)
        {
            iteration++;
            _logger.LogInformation("Agent iteration {Iteration}/{Max} for user {UserId}",
                iteration, maxIterations, userId);

            var completion = await _chatService.CompleteChatAsync(messages, options);

            // Add assistant's response
            messages.Add(new AssistantChatMessage(completion));

            // Use FinishReason for explicit control flow
            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    // Model naturally completed - extract recommendations
                    _logger.LogInformation("Agent completed after {Iterations} iterations", iteration);
                    return ExtractRecommendations(completion);

                case ChatFinishReason.ToolCalls:
                    // Model wants to call tools
                    if (completion.ToolCalls.Count > 0)
                    {
                        await ExecuteToolCallsAsync(userId, messages, completion.ToolCalls);
                    }
                    else
                    {
                        _logger.LogWarning("FinishReason is ToolCalls but no tool calls present");
                        return new List<GeneratedRecommendation>();
                    }
                    break;

                case ChatFinishReason.Length:
                    // Max tokens reached - log and continue
                    _logger.LogWarning("Max tokens reached at iteration {Iteration} - continuing", iteration);
                    break;

                case ChatFinishReason.ContentFilter:
                    _logger.LogWarning("Content filtered at iteration {Iteration}", iteration);
                    return new List<GeneratedRecommendation>();

                default:
                    _logger.LogWarning("Unexpected finish reason: {FinishReason}", completion.FinishReason);
                    return new List<GeneratedRecommendation>();
            }
        }

        _logger.LogWarning("Agent reached max iterations ({MaxIterations}) without completion",
            maxIterations);
        return new List<GeneratedRecommendation>();
    }

    private async Task ExecuteToolCallsAsync(
        string userId,
        List<ChatMessage> messages,
        IReadOnlyList<ChatToolCall> toolCalls)
    {
        _logger.LogInformation("Executing {Count} tool call(s)", toolCalls.Count);

        foreach (var toolCall in toolCalls)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var tool = _toolRegistry.GetTool(toolCall.FunctionName);
                if (tool == null)
                {
                    _logger.LogWarning("Tool not found: {ToolName}", toolCall.FunctionName);
                    messages.Add(new ToolChatMessage(
                        toolCall.Id,
                        JsonSerializer.Serialize(new { error = "Tool not found" })));
                    continue;
                }

                var arguments = JsonDocument.Parse(toolCall.FunctionArguments).RootElement;
                var result = await tool.ExecuteAsync(userId, arguments);

                stopwatch.Stop();
                messages.Add(new ToolChatMessage(toolCall.Id, result));

                _logger.LogInformation("Tool {ToolName} executed in {Duration}ms",
                    tool.Name, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.FunctionName);

                messages.Add(new ToolChatMessage(
                    toolCall.Id,
                    JsonSerializer.Serialize(new { error = ex.Message })));
            }
        }
    }

    private static string CreateSystemPrompt()
    {
        return """
            You are an autonomous financial analysis agent with access to transaction data tools.

            Your goal is to investigate spending patterns and generate 3-5 highly specific, actionable recommendations.

            AVAILABLE TOOLS:
            - SearchTransactions: Find transactions using natural language queries (qualitative discovery)
            - GetCategorySpending: Aggregate total spending by category and time period (quantitative analysis)

            ANALYSIS STRATEGY:
            1. Start with SearchTransactions to discover patterns and categories of interest
            2. Use GetCategorySpending to quantify the spending you found
            3. Compare time periods (thisMonth vs lastMonth) to identify trends
            4. Focus on the most impactful opportunities with concrete dollar amounts

            RECOMMENDATION CRITERIA:
            - SPECIFIC: Include exact amounts, percentages, and merchants
            - ACTIONABLE: Clear next steps the user can take
            - IMPACTFUL: Focus on changes that make a real difference
            - EVIDENCE-BASED: Reference both the transactions found and the total amounts spent

            When you've completed your analysis (after 3-5 tool calls), respond with JSON in this format:
            {
              "recommendations": [
                {
                  "title": "Brief, attention-grabbing title",
                  "message": "Specific recommendation with evidence from your tool calls",
                  "type": "SpendingAlert|SavingsOpportunity|BehavioralInsight|BudgetWarning",
                  "priority": "Low|Medium|High|Critical"
                }
              ]
            }

            Think step-by-step. Search first, then aggregate to quantify what you find.
            """;
    }

    private static string CreateInitialUserPrompt()
    {
        return """
            Analyze this user's transaction data to generate proactive financial recommendations.

            Use the SearchTransactions tool to investigate:
            1. Recurring charges and subscriptions
            2. Frequent spending patterns
            3. Unusual or concerning transactions
            4. Optimization opportunities

            Make 2-4 targeted searches, then provide 3-5 specific recommendations based on what you find.
            """;
    }

    private List<GeneratedRecommendation> ExtractRecommendations(ChatCompletion completion)
    {
        if (completion.Content == null || completion.Content.Count == 0)
        {
            _logger.LogWarning("No content in final message");
            return new List<GeneratedRecommendation>();
        }

        var content = completion.Content[0].Text;

        try
        {
            return ParseRecommendations(content.ExtractJsonFromCodeBlock());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse recommendations from agent output");
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
