# Workshop Step 053: Agentic Recommendation System with Tool Calling

## Mission 🎯

In this step, you'll transform the recommendation engine from a batch-analysis system into an autonomous AI agent that uses function calling to explore transaction data dynamically. Instead of dumping all pattern data into a single AI prompt, the agent will decide which tools to use, execute queries, and reason through findings to generate more targeted recommendations.

**Your goal**: Implement an agentic recommendation system that uses function calling with one tool (SearchTransactions), enabling multi-turn reasoning and autonomous decision-making. In the next workshop step, you'll add a second tool to see how easy the system is to extend.

**Learning Objectives**:
- Understanding AI function calling and tool execution patterns
- Building autonomous agentic systems with multi-turn reasoning
- Implementing tool executors and registries
- Integrating function calling with the current OpenAI .NET SDK
- Creating explainable AI recommendations with tool call chains
- Designing agent loops with iteration limits and completion detection

---

## Prerequisites

Before starting, ensure you completed:
- Recommendation Agent System (052-recommendation-agent.md)

---

## Branches

**Starting branch:** `052-recommendation-agents`
**Solution branch:** `053-agentic-tools`

---

## Background: Batch Analysis vs Agentic Approach

### Current System (Simple AI Recommendations)

The current recommendation system (from Step 052) uses a simple, single-pass approach:

1. **Basic Statistics**: Gathers high-level stats via `GetBasicStatsAsync()` (total income, expenses, top categories)
2. **Single Prompt**: Sends summary statistics to AI in one large prompt
3. **Single AI Call**: Gets back 3-5 general recommendations in one shot via `GenerateSimpleRecommendationsAsync()`
4. **No Exploration**: AI cannot query specific transactions or dig deeper into patterns

**Limitations:**
- ❌ AI only sees summary statistics, not actual transactions
- ❌ No targeted investigation of specific spending patterns
- ❌ Cannot adapt analysis based on discoveries
- ❌ Limited explainability (can't see AI's reasoning process)
- ❌ Recommendations are general, not evidence-based

### New System (Agentic with Tool Calling)

The agentic system uses function calling for dynamic exploration:

1. **Initial Assessment**: Agent gets high-level context about the user
2. **Tool Discovery**: Agent decides which tools to call based on what it wants to investigate
3. **Multi-Turn Execution**: Agent calls tools over multiple iterations
4. **Adaptive Analysis**: Agent adjusts investigation based on tool results
5. **Recommendation Generation**: Agent synthesizes findings into specific recommendations

**Benefits:**
- ✅ Only queries data that's needed (efficient)
- ✅ Targeted investigations (e.g., "search for subscriptions")
- ✅ Autonomous decision-making (agent chooses tools)
- ✅ Explainable (can trace tool call chain)
- ✅ Extensible (easy to add new tools)

---

## Step 53.1: Define Tool Architecture

*Create the foundational abstractions for the tool system.*

The tool architecture defines how tools are discovered, described, and executed by the agent. We'll create interfaces that make it easy to add new tools in the future.

Create `src/BudgetTracker.Api/Features/Intelligence/Tools/IAgentTool.cs`:

```csharp
using System.Text.Json;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    BinaryData ParametersSchema { get; }
    Task<string> ExecuteAsync(string userId, JsonElement arguments);
}

public class ToolExecutionResult
{
    public required string ToolName { get; init; }
    public required string Result { get; init; }
    public required TimeSpan ExecutionTime { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}
```

**Key Design:**
- `IAgentTool` interface defines the contract for all tools
- `ParametersSchema` returns JSON schema as `BinaryData` (SDK requirement)
- `ExecuteAsync` returns JSON string results
- `ToolExecutionResult` tracks execution metadata for logging

---

## Step 53.2: Implement SearchTransactions Tool

*Create the search tool that leverages existing semantic search.*

This tool enables the agent to discover transactions using natural language queries. It wraps the existing semantic search functionality in a tool interface.

Create `src/BudgetTracker.Api/Features/Intelligence/Tools/SearchTransactionsTool.cs`:

```csharp
using System.Text.Json;
using BudgetTracker.Api.Features.Intelligence.Search;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public class SearchTransactionsTool : IAgentTool
{
    private readonly ISemanticSearchService _searchService;
    private readonly ILogger<SearchTransactionsTool> _logger;

    public SearchTransactionsTool(
        ISemanticSearchService searchService,
        ILogger<SearchTransactionsTool> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public string Name => "SearchTransactions";

    public string Description =>
        "Search transactions using semantic search. Use this to find specific patterns, merchants, " +
        "or transaction types. Examples: 'subscriptions', 'coffee shops', 'shopping', " +
        "'dining'. Returns up to maxResults transactions with descriptions and amounts.";

    public BinaryData ParametersSchema => BinaryData.FromObjectAsJson(new
    {
        type = "object",
        properties = new
        {
            query = new
            {
                type = "string",
                description = "Natural language search query describing what transactions to find"
            },
            maxResults = new
            {
                type = "integer",
                description = "Maximum number of results to return (default: 10, max: 20)",
                @default = 10
            }
        },
        required = new[] { "query" }
    },
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public async Task<string> ExecuteAsync(string userId, JsonElement arguments)
    {
        try
        {
            var query = arguments.GetProperty("query").GetString()
                ?? throw new ArgumentException("Query is required");

            var maxResults = arguments.TryGetProperty("maxResults", out var maxResultsEl)
                ? maxResultsEl.GetInt32()
                : 10;

            maxResults = Math.Min(maxResults, 20);

            _logger.LogInformation("SearchTransactions called: query={Query}, maxResults={MaxResults}",
                query, maxResults);

            var results = await _searchService.FindRelevantTransactionsAsync(query, userId, maxResults);

            if (!results.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = 0,
                    message = "No transactions found matching the query.",
                    transactions = Array.Empty<object>()
                });
            }

            var transactions = results.Select(t => new
            {
                id = t.Id,
                date = t.Date.ToString("yyyy-MM-dd"),
                description = t.Description,
                amount = t.Amount,
                category = t.Category,
                account = t.Account
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = transactions.Count,
                query,
                transactions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SearchTransactions tool");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
```

**Key Points:**
- Uses existing `ISemanticSearchService` for semantic search
- Returns structured JSON results
- Handles errors gracefully
- Limits results to prevent overwhelming the agent

---

## Step 53.3: Create Tool Registry

*Build a registry that manages all available tools.*

The tool registry provides a centralized way to access tools and convert them to the format required by the OpenAI SDK.

Create `src/BudgetTracker.Api/Features/Intelligence/Tools/ToolRegistry.cs`:

```csharp
using OpenAI.Chat;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public interface IToolRegistry
{
    IReadOnlyList<IAgentTool> GetAllTools();
    IAgentTool? GetTool(string toolName);
    List<ChatTool> ToChatTools();
}

public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools;

    public ToolRegistry(IEnumerable<IAgentTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, t => t);
    }

    public IReadOnlyList<IAgentTool> GetAllTools()
    {
        return _tools.Values.ToList();
    }

    public IAgentTool? GetTool(string toolName)
    {
        return _tools.TryGetValue(toolName, out var tool) ? tool : null;
    }

    public List<ChatTool> ToChatTools()
    {
        return _tools.Values.Select(tool =>
            ChatTool.CreateFunctionTool(
                functionName: tool.Name,
                functionDescription: tool.Description,
                functionParameters: tool.ParametersSchema
            )
        ).ToList();
    }
}
```

**Key Design:**
- Automatically discovers all registered `IAgentTool` implementations via DI
- Converts tools to SDK's `ChatTool` format
- Provides lookup by tool name for execution

---

## Step 53.4: Simplify Chat Service for Tool Support

*Modify the existing chat service to optionally support tools.*

Instead of adding a separate method, we'll modify the existing `CompleteChatAsync` method to optionally accept tools via `ChatCompletionOptions`, and return `ChatCompletion` instead of just the text.

**Update `src/BudgetTracker.Api/Infrastructure/IAzureChatService.cs`:**

```csharp
using OpenAI.Chat;

namespace BudgetTracker.Api.Infrastructure;

public interface IAzureChatService
{
    Task<string> CompleteChatAsync(string systemPrompt, string userPrompt);

    // Modified to return ChatCompletion and accept optional tools
    Task<ChatCompletion> CompleteChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null);
}
```

**Update `src/BudgetTracker.Api/Infrastructure/AzureChatService.cs`:**

```csharp
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace BudgetTracker.Api.Infrastructure;

public class AzureChatService : IAzureChatService
{
    private readonly AzureOpenAIClient _openAiClient;
    private readonly string _deploymentName;
    private readonly ILogger<AzureChatService> _logger;

    public AzureChatService(
        IAzureOpenAIClientFactory clientFactory,
        IOptions<AzureAiConfiguration> configuration,
        ILogger<AzureChatService> logger)
    {
        var config = configuration.Value;
        _deploymentName = config.DeploymentName;
        _openAiClient = clientFactory.CreateClient();
        _logger = logger;
    }

    public async Task<string> CompleteChatAsync(string systemPrompt, string userPrompt)
    {
        var client = _openAiClient.GetChatClient(_deploymentName);
        var response = await client.CompleteChatAsync(
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt));

        return response.Value.Content[0].Text;
    }

    // Modified to return ChatCompletion and accept optional tools
    public async Task<ChatCompletion> CompleteChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null)
    {
        try
        {
            var client = _openAiClient.GetChatClient(_deploymentName);

            if (options?.Tools.Count > 0)
            {
                _logger.LogDebug("Calling chat completion with {ToolCount} tools",
                    options.Tools.Count);
            }

            var response = await client.CompleteChatAsync(messages, options);

            if (options?.Tools.Count > 0)
            {
                _logger.LogDebug("Chat completion finished: {FinishReason}, {ToolCallCount} tool calls",
                    response.Value.FinishReason,
                    response.Value.ToolCalls.Count);
            }

            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing chat");
            throw;
        }
    }
}
```

**Key Points:**
- One flexible method instead of two separate methods
- Returns `ChatCompletion` (can access ToolCalls and Content)
- Optional `ChatCompletionOptions` for tool support
- Conditional logging when tools are present

---

## Step 53.5: Evolve RecommendationAgent with Agent Logic

*Add agent capabilities directly to the existing RecommendationAgent.*

Instead of creating a separate agent class, we'll incorporate the agentic workflow directly into `RecommendationAgent.GenerateRecommendationsAsync()`. This evolves the existing feature rather than creating new abstractions.

**Update `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationAgent.cs`:**

Add the tool registry dependency:

```csharp
private readonly BudgetTrackerContext _context;
private readonly IAzureChatService _chatService;
private readonly IToolRegistry _toolRegistry;  // Add this
private readonly ILogger<RecommendationAgent> _logger;

public RecommendationAgent(
    BudgetTrackerContext context,
    IAzureChatService chatService,
    IToolRegistry toolRegistry,  // Add this
    ILogger<RecommendationAgent> logger)
{
    _context = context;
    _chatService = chatService;
    _toolRegistry = toolRegistry;  // Add this
    _logger = logger;
}
```

Update the `GenerateRecommendationsAsync` method:

```csharp
public async Task GenerateRecommendationsAsync(string userId)
{
        // ...

        // Run agentic recommendation generation
        var recommendations = await GenerateAgenticRecommendationsAsync(userId, maxIterations: 5);

        if (!recommendations.Any())
        {
            _logger.LogInformation("Agent generated no recommendations for {UserId}", userId);
            return;
        }

        // ...
}
```

Add the agentic generation method:

```csharp
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
```

**Why Use ChatFinishReason?**

Using `ChatFinishReason` provides explicit control flow based on the model's intent:
- `Stop`: Natural completion - the model finished its response, parse recommendations
- `ToolCalls`: Model wants to call tools - execute them and continue the loop
- `Length`: Max tokens reached - handle gracefully (could truncate mid-response)
- `ContentFilter`: Content was filtered - abort safely to avoid incomplete data

This is more robust than just checking `ToolCalls.Count > 0` because:
1. **Handles edge cases**: Properly manages truncation and content filtering scenarios
2. **Makes intent explicit**: The model tells us exactly why it stopped
3. **Better logging**: Clear visibility into what happened at each iteration
4. **Production-ready**: Handles all possible completion states gracefully

Add the tool execution method:

```csharp
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
```

Add helper methods for prompts and parsing:

```csharp
private static string CreateSystemPrompt()
{
    return """
        You are an autonomous financial analysis agent with access to transaction data tools.

        Your goal is to investigate spending patterns and generate 3-5 highly specific, actionable recommendations.

        AVAILABLE TOOLS:
        - SearchTransactions: Find transactions using natural language queries

        ANALYSIS STRATEGY:
        1. Start with exploratory searches to discover patterns
        2. Look for recurring charges, subscriptions, and spending categories
        3. Identify behavioral patterns and opportunities
        4. Focus on the most impactful findings

        RECOMMENDATION CRITERIA:
        - SPECIFIC: Include exact merchants, dates, and patterns found
        - ACTIONABLE: Clear next steps the user can take
        - IMPACTFUL: Focus on changes that make a real difference
        - EVIDENCE-BASED: Reference the specific transactions you found

        When you've completed your analysis (after 2-4 tool calls), respond with JSON in this format:
        {
          "recommendations": [
            {
              "title": "Brief, attention-grabbing title",
              "message": "Specific recommendation with evidence from your searches",
              "type": "SpendingAlert|SavingsOpportunity|BehavioralInsight|BudgetWarning",
              "priority": "Low|Medium|High|Critical"
            }
          ]
        }

        Think step-by-step. Use the search tool to explore before making recommendations.
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

private List<GeneratedRecommendation> ParseRecommendations(string content)
{
    try
    {
        var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);
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
                        Type = Enum.TryParse<RecommendationType>(type.GetString(), out var t)
                            ? t : RecommendationType.BehavioralInsight,
                        Priority = Enum.TryParse<RecommendationPriority>(priority.GetString(), out var p)
                            ? p : RecommendationPriority.Medium
                    });
                }
            }
        }

        return recommendations.Take(5).ToList();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to parse recommendations");
        return new List<GeneratedRecommendation>();
    }
}
```

Update `StoreRecommendationsAsync`:

```csharp
private async Task StoreRecommendationsAsync(
    string userId,
    List<GeneratedRecommendation> aiRecommendations)
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
}
```

**Key Points:**
- Agent logic embedded directly in RecommendationAgent
- No separate AgentContext classes
- Uses local variables for conversation state
- Simpler, more maintainable code
- Same multi-turn agent loop behavior

---

## Step 53.6: Register Services

*Configure dependency injection for the tool system.*

Update `src/BudgetTracker.Api/Program.cs`:

```csharp
// Add tool registration
builder.Services.AddScoped<IAgentTool, SearchTransactionsTool>();
builder.Services.AddScoped<IToolRegistry, ToolRegistry>();

// The existing RecommendationAgent registration remains
// It will now receive IToolRegistry through DI
```

**Key Points:**
- Only register tools and tool registry
- No separate agent interface needed
- RecommendationAgent already registered as `IRecommendationRepository`
- Tools are auto-discovered via `IEnumerable<IAgentTool>`

---

## Step 53.7: Test the Agentic System

*Verify the agent's tool-calling capabilities.*

### 53.7.1: Test Manual Trigger

Trigger recommendation generation manually:

```http
### Manually trigger recommendation generation
POST http://localhost:5295/api/recommendations/generate
X-API-Key: test-key-user1
```

### 53.7.2: Monitor Agent Behavior

Watch the logs while the agent runs:

```bash
dotnet run --project src/BudgetTracker.Api/
```

Look for:
```
Agent started for user test-user-1
Agent iteration 1/5 for user test-user-1
Executing 1 tool call(s)
SearchTransactions called: query=subscriptions, maxResults=10
Tool SearchTransactions executed in 234ms
Agent iteration 2/5 for user test-user-1
Executing 1 tool call(s)
SearchTransactions called: query=recurring monthly charges, maxResults=10
Tool SearchTransactions executed in 198ms
Agent iteration 3/5 for user test-user-1
Agent completed after 3 iterations
Generated 4 recommendations for test-user-1
```

### 53.7.3: Verify Recommendations

Get the generated recommendations:

```http
### Get recommendations
GET http://localhost:5295/api/recommendations
X-API-Key: test-key-user1
```

**Expected improvements:**
- Recommendations mention specific merchants found in searches
- References actual transaction patterns
- More targeted and evidence-based
- Example: "Found 3 streaming subscriptions: Netflix, Hulu, Disney+ totaling $42/month"

### 53.7.4: Test Different Scenarios

Import different transaction patterns and see how the agent explores:

**Scenario 1: Subscription-heavy spending**
- Agent searches for "subscriptions"
- Finds recurring charges
- Recommends consolidation

**Scenario 2: Category-focused spending**
- Agent searches for "dining expenses"
- Searches for "coffee purchases"
- Recommends reducing specific patterns

---

## Summary ✅

You've successfully built an autonomous AI agent with tool-calling capabilities!

### What You Built

✅ **Tool Architecture**: Extensible system for defining and executing agent tools
✅ **SearchTransactions Tool**: Natural language transaction discovery
✅ **Tool Registry**: Automatic tool discovery and SDK integration
✅ **Function Calling**: Proper OpenAI .NET SDK implementation with flexible chat service
✅ **RecommendationAgent**: Evolved with agent logic directly embedded (no separate agent classes)
✅ **Multi-turn Agent Loop**: Autonomous reasoning with iteration control and tool execution

### What's Next?

In **Workshop Step 054**, you'll add a second tool (`GetCategorySpending`) to see how easy it is to extend the agent. You'll learn that:
- Adding tools requires zero changes to the agent loop
- The agent automatically discovers new tools
- Tool composition happens naturally (search + aggregate)

The agentic foundation is complete - now you can easily add more capabilities! 🚀
