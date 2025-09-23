# Workshop Step 052: Recommendation Agent

## Mission 🎯

In this step, you'll implement the **foundation** for an autonomous, AI-powered recommendation system that proactively analyzes user spending patterns and provides intelligent financial advice. This system will work in the background to generate recommendations automatically.

**Important**: This is **Step 1** in building an agentic recommendation system. This step creates the infrastructure and basic AI integration. In **Workshop Step 053**, you'll enhance it with tool-calling capabilities for sophisticated, evidence-based analysis.

**Your goal**: Build the foundational recommendation system including autonomous background processing, database schema, API endpoints, and a simple AI-powered recommendation generator. You'll create a working system that you'll enhance with agentic capabilities in the next step.

**Learning Objectives**:
- Implementing autonomous background services for continuous analysis
- Designing background processing architectures for scheduled execution
- Creating proactive recommendation systems with priority scoring
- Integrating basic AI-powered analysis into backend services
- Building recommendation infrastructure (database, API, frontend)
- Understanding the progression from simple AI to agentic systems

---

## Prerequisites

Before starting, ensure you completed:
- Analytics insights system (051-analytics-insights.md)

---

## Branches

**Starting branch:** `051-analytics-insights`
**Solution branch:** `052-recommendation-agents`

---

## Background: Why Start Simple?

This workshop implements a **simple, working recommendation system** as a foundation for agentic AI:

**What You'll Build in This Step:**
- ✅ Autonomous background processing (runs without user intervention)
- ✅ Basic AI-powered recommendations (high-level financial advice)
- ✅ Complete infrastructure (database, API endpoints, frontend integration)
- ✅ Scheduled generation with smart caching

**What It Does:**
Analyzes high-level transaction statistics (total income, expenses, top categories) and generates general financial recommendations using AI.

**What It Doesn't Do (Yet):**
Sophisticated, targeted analysis of specific spending patterns. The AI receives only summary statistics, not detailed transaction data.

**Why Start Simple?**
This approach teaches the evolution from basic AI integration to autonomous agentic systems:
- **Step 052 (This Step)**: Simple AI call with basic context → General recommendations
- **Step 053 (Next Step)**: Agentic AI with tool-calling → Evidence-based, targeted recommendations

---

## Step 52.1: Create Recommendation Data Model

*Define the core recommendation entity and supporting data structures.*

The recommendation system requires a robust data model that can store various types of recommendations with different priorities and track their lifecycle. This foundation will support the autonomous agent's decision-making process.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/Recommendation.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BudgetTracker.Api.Auth;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public class Recommendation
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public RecommendationType Type { get; set; }

    [Required]
    public RecommendationPriority Priority { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime GeneratedAt { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime ExpiresAt { get; set; }

    [Required]
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Active;
}

public enum RecommendationType
{
    SpendingAlert,
    SavingsOpportunity,
    BehavioralInsight,
    BudgetWarning
}

public enum RecommendationPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum RecommendationStatus
{
    Active,
    Expired
}

public class RecommendationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public RecommendationType Type { get; set; }
    public RecommendationPriority Priority { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal static class RecommendationExtensions
{
    public static RecommendationDto MapToDto(this Recommendation recommendation)
    {
        return new RecommendationDto
        {
            Id = recommendation.Id,
            Title = recommendation.Title,
            Message = recommendation.Message,
            Type = recommendation.Type,
            Priority = recommendation.Priority,
            GeneratedAt = recommendation.GeneratedAt,
            ExpiresAt = recommendation.ExpiresAt
        };
    }
}
```

## Step 52.2: Create Recommendation Repository Interface

*Define the contract for recommendation data access and business logic.*

The repository interface provides a clean abstraction for recommendation operations, enabling proper dependency injection and testability.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/IRecommendationRepository.cs`:

```csharp
namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public interface IRecommendationRepository
{
    Task<List<Recommendation>> GetActiveRecommendationsAsync(string userId);
    Task GenerateRecommendationsAsync(string userId);
}

public interface IRecommendationWorker
{
    Task ProcessAllUsersRecommendationsAsync();
    Task ProcessUserRecommendationsAsync(string userId);
}
```

## Step 52.3: Implement Simple Recommendation Agent

*Build a basic recommendation agent with simple statistics and AI integration.*

The recommendation agent provides the foundation for the recommendation system. It gathers basic transaction statistics and uses AI to generate general financial recommendations. In the next workshop step, you'll enhance this with tool-calling capabilities for sophisticated analysis.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationAgent.cs`:

```csharp
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
```

## Step 52.4: Implement Background Processing Service

*Create the background service that runs the recommendation agent autonomously.*

The background service ensures that recommendations are generated proactively without impacting user experience, running on a schedule and triggered by new data.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationBackgroundService.cs`:

```csharp
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public class RecommendationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecommendationBackgroundService> _logger;
    private readonly TimeSpan _dailyRunTime = TimeSpan.FromHours(6); // 6 AM daily

    public RecommendationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecommendationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        _logger.LogInformation("Recommendation background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IRecommendationWorker>();

                await processor.ProcessAllUsersRecommendationsAsync();
                await CleanupExpiredRecommendationsAsync();

                var nextRun = GetNextRunTime();
                var delay = nextRun - DateTime.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next recommendation run scheduled for {NextRun}", nextRun);
                    await Task.Delay(delay, stoppingToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Recommendation background service cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in recommendation background service");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        _logger.LogInformation("Recommendation background service stopped");
    }

    private async Task CleanupExpiredRecommendationsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerContext>();

            var expiredRecommendations = await context.Recommendations
                .Where(r => r.Status == RecommendationStatus.Active && r.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredRecommendations.Any())
            {
                foreach (var recommendation in expiredRecommendations)
                {
                    recommendation.Status = RecommendationStatus.Expired;
                }

                await context.SaveChangesAsync();
                _logger.LogInformation("Marked {Count} recommendations as expired", expiredRecommendations.Count);
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var oldRecommendations = await context.Recommendations
                .Where(r => r.GeneratedAt < cutoffDate)
                .ToListAsync();

            if (oldRecommendations.Any())
            {
                context.Recommendations.RemoveRange(oldRecommendations);
                await context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} old recommendations", oldRecommendations.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired recommendations");
        }
    }

    private DateTime GetNextRunTime()
    {
        var now = DateTime.UtcNow;
        var today6Am = DateTime.UtcNow.Date.Add(_dailyRunTime);

        if (now < today6Am)
        {
            return today6Am; // Today at 6 AM UTC
        }
        else
        {
            return DateTime.UtcNow.Date.AddDays(1).Add(_dailyRunTime); // Tomorrow at 6 AM UTC
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping recommendation background service...");
        await base.StopAsync(stoppingToken);
    }
}
```

## Step 52.5: Create Recommendation Worker

*Implement the worker that processes recommendations for multiple users.*

The recommendation processor handles the batch processing of recommendations across all users, ensuring efficient resource utilization.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationProcessor.cs`:

```csharp
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public class RecommendationProcessor : IRecommendationWorker
{
    private readonly BudgetTrackerContext _context;
    private readonly IRecommendationRepository _repository;
    private readonly ILogger<RecommendationProcessor> _logger;

    public RecommendationProcessor(
        BudgetTrackerContext context,
        IRecommendationRepository repository,
        ILogger<RecommendationProcessor> logger)
    {
        _context = context;
        _repository = repository;
        _logger = logger;
    }

    public async Task ProcessAllUsersRecommendationsAsync()
    {
        try
        {
            // Get all users with transactions
            var userIds = await _context.Transactions
                .Select(t => t.UserId)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("Processing recommendations for {UserCount} users", userIds.Count);

            var successCount = 0;
            var errorCount = 0;

            foreach (var userId in userIds)
            {
                try
                {
                    await ProcessUserRecommendationsAsync(userId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process recommendations for user {UserId}", userId);
                    errorCount++;
                }

                // Small delay to avoid overwhelming the system
                await Task.Delay(100);
            }

            _logger.LogInformation("Completed recommendation processing: {SuccessCount} successful, {ErrorCount} errors",
                successCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process recommendations for all users");
        }
    }

    public async Task ProcessUserRecommendationsAsync(string userId)
    {
        try
        {
            await _repository.GenerateRecommendationsAsync(userId);
            _logger.LogDebug("Processed recommendations for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process recommendations for user {UserId}", userId);
            throw;
        }
    }
}
```

## Step 52.6: Create Recommendation API Endpoints

*Build the REST API endpoints for recommendation functionality.*

The API endpoints provide the interface for the frontend to interact with the recommendation system, allowing users to view their active recommendations.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationApi.cs`:

```csharp
using System.Security.Claims;
using BudgetTracker.Api.Auth;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public static class RecommendationApi
{
    public static IEndpointRouteBuilder MapRecommendationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/recommendations",
                async (IRecommendationRepository recommendationRepository, ClaimsPrincipal claimsPrincipal) =>
                {
                    var userId = claimsPrincipal.GetUserId();
                    var recommendations = await recommendationRepository.GetActiveRecommendationsAsync(userId);
                    var dtos = recommendations.Select(r => r.MapToDto()).ToList();
                    return Results.Ok(dtos);
                })
            .RequireAuthorization()
            .WithName("GetRecommendations")
            .WithSummary("Get active recommendations")
            .WithDescription("Returns up to 5 active, non-expired recommendations ordered by priority")
            .Produces<List<RecommendationDto>>();

        return routes;
    }
}
```

## Step 52.7: Set Up Intelligence Endpoints Registration

*Create the main intelligence endpoints registration class.*

This class coordinates all intelligence-related endpoints, including recommendations and other AI features.

Create `src/BudgetTracker.Api/Features/Intelligence/IntelligenceEndpoints.cs`:

```csharp
using BudgetTracker.Api.Features.Intelligence.Query;
using BudgetTracker.Api.Features.Intelligence.Recommendations;

namespace BudgetTracker.Api.Features.Intelligence;

public static class IntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapQueryEndpoints();
        endpoints.MapRecommendationEndpoints();
        return endpoints;
    }
}
```

## Step 52.8: Update Database Context

*Add the recommendations table to the database context.*

The database context needs to include the recommendations table for Entity Framework to manage the recommendation data.

Update `src/BudgetTracker.Api/Infrastructure/BudgetTrackerContext.cs` to include the Recommendations DbSet:

```csharp
// Add this property to the BudgetTrackerContext class
public DbSet<Recommendation> Recommendations => Set<Recommendation>();
```

And add the using statement at the top:

```csharp
using BudgetTracker.Api.Features.Intelligence.Recommendations;
```

## Step 52.9: Register Services and Background Processing

*Configure dependency injection and background services.*

Register all the recommendation services and configure the background processing in the application startup.

Update `src/BudgetTracker.Api/Program.cs` to include recommendation services:

```csharp
// Add these service registrations after existing services
builder.Services.AddScoped<IRecommendationRepository, RecommendationAgent>();
builder.Services.AddScoped<IRecommendationWorker, RecommendationProcessor>();

// Add the background service
builder.Services.AddHostedService<RecommendationBackgroundService>();

// Add intelligence endpoints mapping after existing endpoint mappings
app.MapIntelligenceEndpoints();
```

## Step 52.10: Create Database Migration

*Generate and apply the database migration for recommendations.*

Create the database migration to add the recommendations table to the database schema.

```bash
# From src/BudgetTracker.Api/ directory
dotnet ef migrations add AddRecommendations
dotnet ef database update
```

## Step 52.11: Verify Frontend Types and API Client

*Verify TypeScript interfaces and API client for recommendations.*

The intelligence feature already has an existing `api.ts` file. Verify that it includes the recommendation types and API methods.

Verify `src/BudgetTracker.Web/src/features/intelligence/api.ts` contains:

```typescript
// This interface should already exist in api.ts
export interface ProactiveRecommendation {
  id: string;
  title: string;
  message: string;
  type: 'SpendingAlert' | 'SavingsOpportunity' | 'BehavioralInsight' | 'BudgetWarning';
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  generatedAt: string;
  expiresAt: string;
}

// This method should already exist in intelligenceApi
export const intelligenceApi = {
  // ... existing methods ...

  async getRecommendations(): Promise<ProactiveRecommendation[]> {
    const response = await api.get<ProactiveRecommendation[]>('/recommendations');
    return response.data;
  }
};
```

If these are missing, add them to the existing `api.ts` file. The `ProactiveRecommendation` interface and `getRecommendations` method are required for the recommendation system to work.

## Step 52.12: Build RecommendationsCard Component

*Create the React component to display recommendations.*

Build a comprehensive UI component that displays recommendations with proper styling and visual indicators. This component will receive recommendations as props from the dashboard, following the same pattern as the InsightsCard.

Create `src/BudgetTracker.Web/src/features/intelligence/components/RecommendationsCard.tsx`:

```tsx
import type { ProactiveRecommendation } from '../types';

interface RecommendationsCardProps {
  recommendations: ProactiveRecommendation[];
}

const AlertTriangleIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"></path>
    <path d="M12 9v4"></path>
    <path d="M12 17h.01"></path>
  </svg>
);

const DollarSignIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="12" y1="2" x2="12" y2="22"></line>
    <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
  </svg>
);

const LightbulbIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5"></path>
    <path d="M9 18h6"></path>
    <path d="M10 22h4"></path>
  </svg>
);

const TrendingDownIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="22 17 13.5 8.5 8.5 13.5 2 7"></polyline>
    <polyline points="16 17 22 17 22 11"></polyline>
  </svg>
);

export function RecommendationsCard({ recommendations }: RecommendationsCardProps) {
  const getIcon = (type: ProactiveRecommendation['type']) => {
    switch (type) {
      case 'SpendingAlert':
        return <AlertTriangleIcon />;
      case 'SavingsOpportunity':
        return <DollarSignIcon />;
      case 'BehavioralInsight':
        return <LightbulbIcon />;
      case 'BudgetWarning':
        return <TrendingDownIcon />;
      default:
        return <LightbulbIcon />;
    }
  };

  const getPriorityStyles = (priority: ProactiveRecommendation['priority']) => {
    switch (priority) {
      case 'Critical':
        return 'border-red-500 bg-red-50 text-red-900';
      case 'High':
        return 'border-orange-500 bg-orange-50 text-orange-900';
      case 'Medium':
        return 'border-yellow-500 bg-yellow-50 text-yellow-900';
      case 'Low':
        return 'border-blue-500 bg-blue-50 text-blue-900';
      default:
        return 'border-gray-500 bg-gray-50 text-gray-900';
    }
  };

  const formatType = (type: string) => {
    return type.replace(/([A-Z])/g, ' $1').trim();
  };

  if (recommendations.length === 0) {
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Financial Recommendations</h3>
        <p className="text-sm text-gray-600">
          Import more transactions to receive personalized financial recommendations.
        </p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <h3 className="text-lg font-semibold text-gray-900 mb-4">Financial Recommendations</h3>
      <div className="space-y-3">
        {recommendations.map((recommendation) => (
          <div
            key={recommendation.id}
            className={`border-l-4 rounded-lg p-4 ${getPriorityStyles(recommendation.priority)}`}
          >
            <div className="flex items-start gap-3">
              <div className="flex-shrink-0 mt-0.5">
                {getIcon(recommendation.type)}
              </div>
              <div className="flex-1 min-w-0">
                <h4 className="font-semibold text-sm mb-1">{recommendation.title}</h4>
                <p className="text-sm opacity-90 leading-relaxed">{recommendation.message}</p>
                <div className="mt-2 flex items-center gap-2 text-xs opacity-75">
                  <span>{recommendation.priority} priority</span>
                  <span>·</span>
                  <span>{formatType(recommendation.type)}</span>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
```

This component provides:
- Props-based design that receives recommendations from the dashboard loader
- Priority-based styling with distinct color schemes for each priority level
- Type-specific inline SVG icons (no external dependencies)
- Responsive design with proper accessibility
- Empty state handling when no recommendations are available

## Step 52.13: Update Intelligence Feature Index

*Update the exports for the intelligence feature.*

Update `src/BudgetTracker.Web/src/features/intelligence/index.ts` to include the RecommendationsCard export:

```typescript
export { intelligenceApi } from './api';
export { RecommendationsCard } from './components/RecommendationsCard';
export { default as QueryAssistant } from './components/QueryAssistant';
export type { ProactiveRecommendation } from './api';
```

Note: `ProactiveRecommendation` is exported from `'./api'` since the type is defined there alongside the API methods.

## Step 52.14: Integrate Recommendations into Dashboard

*Add recommendations to the dashboard with proper loading and error handling.*

Enhance the dashboard to load and display recommendation data alongside existing analytics. The dashboard will fetch recommendations in the loader and pass them as props to the RecommendationsCard component.

Update `src/BudgetTracker.Web/src/routes/dashboard.tsx`:

```tsx
import { parseISO, format } from 'date-fns';
import { useLoaderData, useNavigation, type LoaderFunctionArgs } from 'react-router';
import { SkeletonCard } from '../shared/components/Skeleton';
import { SummaryCard, InsightsCard, analyticsApi } from '../features/analytics';
import Header from '../shared/components/layout/Header';
import { QueryAssistant, RecommendationsCard, intelligenceApi } from '../features/intelligence';
import { transactionsApi } from '../features/transactions';
import type { BudgetInsights } from '../features/analytics';
import type { ProactiveRecommendation } from '../features/intelligence';
import type { TransactionSummary } from '../features/transactions';

const DollarIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="12" x2="12" y1="2" y2="22"></line>
    <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
  </svg>
);

const TrendingUpIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="22 7 13.5 15.5 8.5 10.5 2 17"></polyline>
    <polyline points="16 7 22 7 22 13"></polyline>
  </svg>
);

const TrendingDownIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="22 17 13.5 8.5 8.5 13.5 2 7"></polyline>
    <polyline points="16 17 22 17 22 11"></polyline>
  </svg>
);

export async function loader({ }: LoaderFunctionArgs) {
  try {
    const [summary, insights, recommendations] = await Promise.all([
      transactionsApi.getTransactionSummary(),
      analyticsApi.getInsights().catch(() => null),
      intelligenceApi.getRecommendations().catch(() => [])
    ]);
    return { summary, insights, recommendations };
  } catch (error) {
    console.error('Failed to load dashboard data:', error);
    throw new Error('Failed to load dashboard data');
  }
}

function Dashboard() {
  const data = useLoaderData() as {
    summary: TransactionSummary;
    insights: BudgetInsights | null;
    recommendations: ProactiveRecommendation[]
  };
  const { summary, insights, recommendations } = data;
  const navigation = useNavigation();
  const isLoading = navigation.state === 'loading';

  const formatDateRange = (summary: TransactionSummary) => {
    if (!summary.earliestDate || !summary.latestDate) {
      return 'No transactions';
    }

    try {
      const start = parseISO(summary.earliestDate);
      const end = parseISO(summary.latestDate);

      return `${format(start, 'MMM d')} – ${format(end, 'MMM d, yyyy')}`;
    } catch (error) {
      return 'Date range unavailable';
    }
  };

  if (isLoading) {
    return (
      <div className="px-4 py-6 sm:px-0">
        <div className="mb-10">
          <div className="animate-pulse bg-neutral-200 rounded-xl h-8 w-48 mb-3" />
          <div className="animate-pulse bg-neutral-200 rounded-lg h-4 w-32" />
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
          {Array.from({ length: 3 }).map((_, index) => (
            <SkeletonCard key={index} />
          ))}
        </div>

        <div className="animate-pulse bg-neutral-200 rounded-xl h-40 mb-6" />

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="animate-pulse bg-neutral-200 rounded-xl h-64" />
          <div className="animate-pulse bg-neutral-200 rounded-xl h-64" />
        </div>
      </div>
    );
  }

  return (
    <div className="px-4 py-6 sm:px-0">
      <Header
        title="Dashboard"
        subtitle={formatDateRange(summary)}
      />

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        <SummaryCard
          title="Total Income"
          value={summary.totalIncome}
          icon={<TrendingUpIcon />}
          valueColor="text-card-foreground"
          trend="+12% from last month"
          isCurrency={true}
        />

        <SummaryCard
          title="Total Expenses"
          value={summary.totalExpenses}
          icon={<TrendingDownIcon />}
          valueColor="text-card-foreground"
          trend="-5% from last month"
          isCurrency={true}
        />

        <SummaryCard
          title="Net Balance"
          value={summary.netAmount}
          icon={<DollarIcon />}
          valueColor={summary.netAmount >= 0 ? "text-chart-4" : "text-chart-1"}
          trend={summary.netAmount >= 0 ? "Positive balance" : "Negative balance"}
          isCurrency={true}
        />
      </div>

      {recommendations.length > 0 && (
        <div className="mb-6">
          <RecommendationsCard recommendations={recommendations} />
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {insights && <InsightsCard insights={insights} />}
        <QueryAssistant />
      </div>
    </div>
  );
}
export default Dashboard;
```

Key integration points:

1. **Loader Function**: Fetches recommendations alongside summary and insights data using `Promise.all` for parallel loading
2. **Error Handling**: Uses `.catch()` to gracefully handle failures without breaking the dashboard
3. **Props Passing**: Passes `recommendations` array as props to `RecommendationsCard`
4. **Conditional Rendering**: Only displays recommendations section when recommendations exist
5. **Layout**: Places recommendations prominently between summary cards and insights/query sections

## Step 52.15: Test the Recommendation System

*Test the complete recommendation agent functionality.*

### 52.15.1: Test Background Service

Verify that the background service is running and processing recommendations:

```bash
# Check logs for background service startup
dotnet run --project src/BudgetTracker.Api/

# Look for logs like:
# "Recommendation background service started"
# "Processing recommendations for X users"
```

### 52.15.2: Test Recommendation Retrieval

Verify that recommendations are properly returned by the API:

```http
### Get active recommendations
GET http://localhost:5295/api/recommendations
X-API-Key: test-key-user1
```

**Expected Response:**
```json
[
  {
    "id": "guid-here",
    "title": "Review Your Budget",
    "message": "Your expenses are approaching your income. Consider reviewing your spending in top categories to identify savings opportunities.",
    "type": "BudgetWarning",
    "priority": "Medium",
    "generatedAt": "2025-01-20T10:30:00Z",
    "expiresAt": "2025-01-27T10:30:00Z"
  },
  {
    "id": "guid-here-2",
    "title": "Track Your Top Spending Categories",
    "message": "Most of your expenses are in Groceries, Dining, and Shopping. Monitoring these categories could help you save more.",
    "type": "BehavioralInsight",
    "priority": "Low",
    "generatedAt": "2025-01-20T10:30:00Z",
    "expiresAt": "2025-01-27T10:30:00Z"
  }
]
```

### 52.15.3: Test Dashboard Integration

1. Navigate to the dashboard at `http://localhost:5173/dashboard`
2. Verify that recommendations appear prominently on the page
3. Confirm that recommendations are displayed with appropriate priority styling
4. Verify that recommendations update automatically after importing new transactions

### 52.15.4: Test Import Trigger Integration

Import new transactions and verify that recommendations are automatically generated:

---

## Summary ✅

You've successfully implemented the **foundational recommendation system** that serves as the base for agentic AI capabilities:

✅ **Autonomous Background Processing**: Recommendations generated automatically without user intervention
✅ **Complete Infrastructure**: Full-stack implementation with database, API, background service, and frontend
✅ **Basic AI Integration**: Simple AI-powered recommendations using high-level transaction statistics
✅ **Priority-Based System**: Intelligent prioritization of recommendations based on urgency and impact
✅ **Scheduled Generation**: Daily background processing with smart caching to avoid redundant generation
✅ **User-Friendly Interface**: Intuitive recommendation cards with priority-based styling

You've built a **working recommendation system** that demonstrates autonomous background processing and AI integration. In the next step, you'll see how tool-calling transforms this into a sophisticated agentic system! 🚀