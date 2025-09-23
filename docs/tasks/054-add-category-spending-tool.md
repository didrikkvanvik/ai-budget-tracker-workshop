# Workshop Step 054: Adding a Second Agent Tool

## Mission 🎯

In this exercise, you'll add a second tool to your agentic system to see how easy it is to extend. You'll implement `GetCategorySpending` which aggregates spending data by category over date ranges. This tool complements `SearchTransactions` by providing quantitative analysis after qualitative discovery.

**Your goal**: Implement GetCategorySpending tool and observe how the agent automatically discovers it and combines it with SearchTransactions for more powerful recommendations.

**Learning Objectives**:
- Understanding how tool extensibility works
- Implementing aggregation tools with parameters
- Observing autonomous tool composition
- Seeing how agents combine qualitative (search) and quantitative (aggregation) analysis

---

## Prerequisites

Before starting, ensure you completed:
- **Workshop Step 053: Agentic Recommendation Tool System** (required)
- Have the agent working with SearchTransactions tool
- Understand the IAgentTool interface and tool registry

---

## Branches

**Starting branch:** `053-agentic-tools`
**Solution branch:** `054-add-category-spending-tool`

---

## Background: Why Add This Tool?

### Current System (SearchTransactions Only)

Your agent can currently:
- ✅ Find specific transactions with natural language
- ✅ Discover patterns like "subscriptions" or "coffee shops"
- ✅ Identify specific merchants

**Limitation:**
- ❌ Cannot quantify total spending in categories
- ❌ Cannot compare time periods (this month vs last month)
- ❌ Cannot calculate averages or totals

### With GetCategorySpending

Your agent will be able to:
- ✅ Search for subscriptions
- ✅ **Then aggregate Entertainment spending to quantify the impact**
- ✅ **Compare this month to last month for trends**
- ✅ Provide evidence-based recommendations with exact dollar amounts

**Example workflow:**
```
1. SearchTransactions("dining out") → Finds 15 restaurant transactions
2. GetCategorySpending("Dining", "thisMonth") → $456 total
3. GetCategorySpending("Dining", "lastMonth") → $312 total
4. Recommendation: "Dining spending increased 46% ($456 vs $312)"
```

---

## Understanding Tool Extensibility

### How the Registry Works

The `ToolRegistry` automatically discovers all tools registered via dependency injection:

```csharp
public ToolRegistry(IEnumerable<IAgentTool> tools)
{
    _tools = tools.ToDictionary(t => t.Name, t => t);
}
```

When you register a new `IAgentTool` in `Program.cs`, it's automatically:
1. **Discovered** by the registry
2. **Converted** to `ChatTool` format for the OpenAI SDK
3. **Available** to the agent without any code changes

### Zero Agent Changes Required

The agent loop doesn't need modification:
- ✅ Agent sees all tools in `ChatCompletionOptions`
- ✅ Agent decides which tools to call
- ✅ Tool execution happens through the registry
- ✅ Results flow back to the agent automatically

---

## Step 54.1: Implement GetCategorySpending Tool

*Create the aggregation tool for category spending analysis.*

Create `src/BudgetTracker.Api/Features/Intelligence/Tools/GetCategorySpendingTool.cs`:

```csharp
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
```

**Key Design Decisions:**
- **Preset date ranges**: Easier for AI to use than custom dates
- **Top merchants**: Provides context about where money is going
- **Transaction count**: Shows frequency of spending
- **Error handling**: Returns structured JSON even on failure
- **DateTime Kind**: Explicitly specifies `DateTimeKind.Utc` when constructing DateTime objects to ensure compatibility with PostgreSQL's `timestamptz` type (which only accepts UTC timestamps)

---

## Step 54.2: Register the Tool

*Add the new tool to dependency injection - that's it!*

Update `src/BudgetTracker.Api/Program.cs`:

```csharp
// Add this line after the existing SearchTransactionsTool registration
builder.Services.AddScoped<IAgentTool, GetCategorySpendingTool>();
```

That's all you need! The registry will automatically discover it.

---

## Step 54.3: Update System Prompt (Optional)

*Let the agent know about the second tool.*

Update the system prompt in `RecommendationAgent.cs` to mention both tools:

```csharp
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
```

---

## Step 54.4: Test Tool Composition

*See how the agent combines both tools autonomously.*

### 54.4.1: Trigger Recommendation Generation

```http
### Trigger recommendation generation
POST http://localhost:5295/api/recommendations/generate
X-API-Key: test-key-user1
```

### 54.4.2: Monitor Agent Behavior

Watch the logs to see tool composition:

```bash
dotnet run --project src/BudgetTracker.Api/
```

**Expected log pattern:**
```
Agent iteration 1/5 for user test-user-1
Executing 1 tool call(s)
SearchTransactions called: query=subscriptions, maxResults=10
Tool SearchTransactions executed in 234ms

Agent iteration 2/5 for user test-user-1
Executing 1 tool call(s)
GetCategorySpending called: category=Entertainment, dateRange=thisMonth
Tool GetCategorySpending executed in 89ms

Agent iteration 3/5 for user test-user-1
Executing 1 tool call(s)
GetCategorySpending called: category=Entertainment, dateRange=lastMonth
Tool GetCategorySpending executed in 76ms

Agent iteration 4/5 for user test-user-1
Agent completed after 4 iterations, 3 tool calls
Generated 5 recommendations for test-user-1
```

### 54.4.3: Verify Enhanced Recommendations

Get the generated recommendations:

```http
### Get recommendations
GET http://localhost:5295/api/recommendations
X-API-Key: test-key-user1
```

**Expected improvements:**
- ✅ Recommendations include exact dollar amounts
- ✅ Month-over-month comparisons with percentages
- ✅ Specific merchants mentioned (from search) with totals (from aggregation)
- ✅ Example: "Entertainment spending: $87 this month (+38% from last month's $63). Found subscriptions to Netflix ($15), Hulu ($8), Disney+ ($10), HBO Max ($16)."

---

## Step 54.5: Testing Scenarios

*Observe different tool composition patterns.*

### Scenario 1: Subscription Audit

**Expected agent behavior:**
```
1. SearchTransactions("subscriptions") → Netflix, Spotify, Gym
2. GetCategorySpending("Entertainment", "thisMonth") → $87 total
3. SearchTransactions("streaming services") → Netflix, Hulu, Disney+, HBO Max
4. Generate: "You have 4 streaming services totaling $49/month (56% of Entertainment budget)"
```

### Scenario 2: Category Trend Analysis

**Expected agent behavior:**
```
1. SearchTransactions("dining restaurants") → Finds frequent dining
2. GetCategorySpending("Dining", "thisMonth") → $456
3. GetCategorySpending("Dining", "lastMonth") → $312
4. SearchTransactions("food delivery") → DoorDash, Uber Eats
5. Generate: "Dining up 46% ($456 vs $312). Food delivery accounts for $134 (29%)"
```

### Scenario 3: Multi-Category Analysis

**Expected agent behavior:**
```
1. SearchTransactions("coffee") → Starbucks, Dunkin
2. GetCategorySpending("Dining", "thisMonth") → Includes coffee in total
3. SearchTransactions("groceries") → Grocery stores
4. GetCategorySpending("Groceries", "thisMonth") → Compare prepared vs grocery food
5. Generate insights about food spending optimization
```

---

## Understanding What Happened

### Tool Discovery

The agent automatically discovered your new tool because:
1. You registered `IAgentTool` implementation in DI
2. `ToolRegistry` received it via `IEnumerable<IAgentTool>`
3. Registry converted it to `ChatTool` format
4. OpenAI SDK received the tool definition
5. Agent saw it as an available capability

### Tool Composition

The agent decided to:
1. **Search first** (qualitative): Find specific patterns
2. **Aggregate second** (quantitative): Measure their impact
3. **Compare periods**: Identify trends
4. **Generate recommendations**: With concrete evidence

**No code told it to do this** - the agent chose this strategy autonomously!

---

## Challenge: Extend Further (Optional)

Want to see how truly extensible this is? Try adding a third tool:

### Challenge Tool: GetTopMerchants

```csharp
public class GetTopMerchantsTool : IAgentTool
{
    public string Name => "GetTopMerchants";

    public string Description =>
        "Get top merchants by spending across all categories or within a specific category. " +
        "Returns merchant name, total amount spent, and transaction count.";

    // Implement ParametersSchema, ExecuteAsync, etc.
}
```

Register it in `Program.cs`:
```csharp
builder.Services.AddScoped<IAgentTool, GetTopMerchantsTool>();
```

The agent will automatically discover and use it! No other changes needed.

---

## Summary ✅

You've successfully extended your agentic system with a second tool!

### What You Learned

✅ **Tool Extensibility**: Adding tools requires minimal code
✅ **Automatic Discovery**: Registry finds tools via DI automatically
✅ **Zero Agent Changes**: Agent loop doesn't need modification
✅ **Tool Composition**: Agent combines tools intelligently
✅ **Qualitative + Quantitative**: Search discovers, aggregation quantifies

### Key Observations

**Tool Composition Patterns:**
- 🔍 **Explore then Quantify**: Search → Aggregate
- 📊 **Compare Periods**: thisMonth → lastMonth
- 🎯 **Drill Down**: Category total → Top merchants
- 💡 **Synthesize**: Multiple tool results → Actionable recommendation

**Agent Autonomy:**
- Agent decides which tools to use
- Agent determines the order of tool calls
- Agent composes multiple tool results
- Agent generates evidence-based recommendations

### What Makes This Powerful

**For Developers:**
- Adding tools is trivial (one file + one line of registration)
- No agent loop modifications needed
- Tools are self-contained and testable
- Easy to add specialized analysis capabilities

**For Users:**
- More specific recommendations with exact amounts
- Trend analysis with month-over-month comparisons
- Evidence-based advice ("You spent $X on Y")
- Actionable insights with concrete impact

The agentic architecture you built enables unlimited extensibility - you can keep adding tools and the agent will discover and use them intelligently! 🚀

---

## Next Steps

**Potential Additional Tools:**
- `DetectAnomalies` - Statistical outlier detection
- `PredictCashflow` - Future balance forecasting
- `FindRecurring` - Advanced subscription detection
- `CompareBudget` - Actual vs planned spending
- `AnalyzeMerchantFrequency` - Shopping habit patterns

Each tool you add makes the agent more capable, and the agent figures out how to combine them!
