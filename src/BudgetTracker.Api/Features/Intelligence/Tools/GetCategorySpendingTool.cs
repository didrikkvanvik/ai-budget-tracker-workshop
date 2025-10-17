using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public class GetCategorySpendingTool : IAgentTool
{
    private readonly BudgetTrackerContext _context;
    private readonly ILogger<GetCategorySpendingTool> _logger;

    public GetCategorySpendingTool(
        BudgetTrackerContext context,
        ILogger<GetCategorySpendingTool> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string Name => "GetCategorySpending";

    public string Description =>
        "Get total spending for a specific category over a date range. Use this to quantify spending patterns " +
        "and compare time periods. Returns total amount, transaction count, and top merchants. " +
        "Useful for understanding spending magnitude after finding patterns with SearchTransactions. " +
        "Date ranges: 'last7days', 'last30days', 'last90days', 'thisMonth', 'lastMonth'.";

    public BinaryData ParametersSchema => BinaryData.FromObjectAsJson(new
    {
        type = "object",
        properties = new
        {
            category = new
            {
                type = "string",
                description = "Category name to analyze (e.g., 'Dining', 'Entertainment', 'Shopping', 'Transportation')"
            },
            dateRange = new
            {
                type = "string",
                description = "Preset date range: 'last7days', 'last30days', 'last90days', 'thisMonth', 'lastMonth'",
                @enum = new[] { "last7days", "last30days", "last90days", "thisMonth", "lastMonth" },
                @default = "last30days"
            }
        },
        required = new[] { "category" }
    },
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public async Task<string> ExecuteAsync(string userId, JsonElement arguments)
    {
        try
        {
            var category = arguments.GetProperty("category").GetString()
                ?? throw new ArgumentException("Category is required");

            var dateRange = arguments.TryGetProperty("dateRange", out var rangeEl)
                ? rangeEl.GetString()
                : "last30days";

            var (startDate, endDate) = ParseDateRange(dateRange ?? "last30days");

            _logger.LogInformation(
                "GetCategorySpending called: category={Category}, dateRange={DateRange}",
                category, dateRange);

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId &&
                           t.Category == category &&
                           t.Date >= startDate &&
                           t.Date <= endDate &&
                           t.Amount < 0)
                .ToListAsync();

            if (!transactions.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    category,
                    dateRange,
                    startDate = startDate.ToString("yyyy-MM-dd"),
                    endDate = endDate.ToString("yyyy-MM-dd"),
                    totalSpending = 0m,
                    transactionCount = 0,
                    message = "No transactions found in this category and date range."
                });
            }

            var totalSpending = Math.Abs(transactions.Sum(t => t.Amount));
            var transactionCount = transactions.Count;
            var averageTransaction = totalSpending / transactionCount;

            var topMerchants = transactions
                .GroupBy(t => t.Description)
                .Select(g => new
                {
                    merchant = g.Key,
                    amount = Math.Abs(g.Sum(t => t.Amount)),
                    count = g.Count()
                })
                .OrderByDescending(x => x.amount)
                .Take(3)
                .ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                category,
                dateRange,
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
                daySpan = (endDate - startDate).Days,
                totalSpending = Math.Round(totalSpending, 2),
                transactionCount,
                averageTransaction = Math.Round(averageTransaction, 2),
                topMerchants
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing GetCategorySpending tool");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static (DateTime startDate, DateTime endDate) ParseDateRange(string dateRange)
    {
        var now = DateTime.UtcNow.Date;

        return dateRange switch
        {
            "last7days" => (now.AddDays(-7), now),
            "last30days" => (now.AddDays(-30), now),
            "last90days" => (now.AddDays(-90), now),
            "thisMonth" => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), now),
            "lastMonth" => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1),
                           new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-1)),
            _ => (now.AddDays(-30), now)
        };
    }
}
