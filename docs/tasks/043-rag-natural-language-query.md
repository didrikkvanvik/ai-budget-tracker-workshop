# Workshop Step 042: RAG Semantic Search & Natural Language Query Assistant

## Mission 🎯

In this step, you'll implement a powerful semantic search system using RAG (Retrieval-Augmented Generation) and create a natural language query assistant that allows users to ask questions about their financial data in plain English.

**Your goal**: Build a complete semantic search system with vector embeddings and a natural language query interface that can answer questions like "What was my biggest expense last week?" or "Show me all coffee-related purchases."

**Learning Objectives**:
- Setting up pgvector for PostgreSQL vector storage
- Implementing Azure OpenAI embeddings for semantic search
- Building background services for embedding generation
- Creating semantic search capabilities
- Developing a natural language query assistant
- Integrating RAG patterns for contextual AI responses

---

## Prerequisites

Before starting, ensure you completed the previous workshop steps and have:
- Basic authentication and infrastructure from section-05
- Azure OpenAI service configured
- PostgreSQL database running

---

## Branches

**Starting branch:** `042-rag-semantic-search`
**Solution branch:** `043-rag-nlq`

---

## Step 42.1: Add Required NuGet Packages

*Install the necessary packages for vector operations and Azure OpenAI integration.*

First, we need to add support for pgvector and Azure OpenAI to handle vector embeddings and AI chat completions.

Update `src/BudgetTracker.Api/BudgetTracker.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.0.9" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.9" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.9">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />

    <!-- Add AI and Vector packages -->
    <PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
    <PackageReference Include="Pgvector" Version="0.3.0" />
    <PackageReference Include="Pgvector.EntityFrameworkCore" Version="0.3.0" />
  </ItemGroup>

</Project>
```

## Step 42.2: Create Transaction Entity with Vector Support

*Define the core Transaction entity with vector embedding support for semantic search.*

Create `src/BudgetTracker.Api/Features/Transactions/TransactionTypes.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BudgetTracker.Api.Auth;
using Pgvector;

namespace BudgetTracker.Api.Features.Transactions;

public class Transaction
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime Date { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Balance { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(200)]
    public string? Labels { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime ImportedAt { get; set; }

    [Required]
    [MaxLength(100)]
    public string Account { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? ImportSessionHash { get; set; }

    /// <summary>
    /// Vector embedding for semantic search (1536 dimensions for text-embedding-3-small)
    /// </summary>
    public Vector? Embedding { get; set; }
}

public class TransactionDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public string? Category { get; set; }
    public string? Labels { get; set; }
    public DateTime ImportedAt { get; set; }
    public string Account { get; set; } = string.Empty;
}

internal static class TransactionExtensions
{
    public static TransactionDto MapToDto(this Transaction transaction)
    {
        return new TransactionDto
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Description = transaction.Description,
            Amount = transaction.Amount,
            Balance = transaction.Balance,
            Category = transaction.Category,
            Labels = transaction.Labels,
            ImportedAt = transaction.ImportedAt,
            Account = transaction.Account
        };
    }
}
```

## Step 42.3: Update Database Context for Vector Support

*Extend the database context to support vector operations and the Transaction entity.*

Update `src/BudgetTracker.Api/Infrastructure/BudgetTrackerContext.cs`:

```csharp
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

public class BudgetTrackerContext : IdentityDbContext<ApplicationUser>
{
    public BudgetTrackerContext(DbContextOptions<BudgetTrackerContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // Configure Transaction entity
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.ImportSessionHash);

            // Configure vector column with 1536 dimensions for text-embedding-3-small
            entity.Property(e => e.Embedding)
                .HasColumnType("vector(1536)");
        });
    }
}
```

## Step 42.4: Create Azure OpenAI Infrastructure

*Set up Azure OpenAI client factory and configuration for AI operations.*

Create `src/BudgetTracker.Api/Infrastructure/AzureOpenAI.cs`:

```csharp
using Azure.AI.OpenAI;

namespace BudgetTracker.Api.Infrastructure;

public interface IAzureOpenAIClientFactory
{
    AzureOpenAIClient CreateClient();
}

public class AzureOpenAIClientFactory : IAzureOpenAIClientFactory
{
    private readonly AzureOpenAIConfiguration _configuration;

    public AzureOpenAIClientFactory(AzureOpenAIConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AzureOpenAIClient CreateClient()
    {
        return new AzureOpenAIClient(new Uri(_configuration.Endpoint), new System.ClientModel.ApiKeyCredential(_configuration.ApiKey));
    }
}

public class AzureOpenAIConfiguration
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}

public interface IAzureChatService
{
    Task<string> CompleteChatAsync(string systemPrompt, string userPrompt);
}

public class AzureChatService : IAzureChatService
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly AzureOpenAIConfiguration _configuration;
    private readonly ILogger<AzureChatService> _logger;

    public AzureChatService(
        IAzureOpenAIClientFactory clientFactory,
        AzureOpenAIConfiguration configuration,
        ILogger<AzureChatService> logger)
    {
        _openAIClient = clientFactory.CreateClient();
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> CompleteChatAsync(string systemPrompt, string userPrompt)
    {
        try
        {
            var client = _openAIClient.GetChatClient(_configuration.ChatModel);

            var messages = new[]
            {
                new OpenAI.Chat.SystemChatMessage(systemPrompt),
                new OpenAI.Chat.UserChatMessage(userPrompt)
            };

            var response = await client.CompleteChatAsync(messages);
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete chat with Azure OpenAI");
            throw;
        }
    }
}
```

## Step 42.5: Implement Embedding Service

*Create the service responsible for generating vector embeddings from text.*

Create `src/BudgetTracker.Api/Features/Intelligence/Search/IAzureEmbeddingService.cs`:

```csharp
using Pgvector;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public interface IAzureEmbeddingService
{
    Task<Vector> GenerateEmbeddingAsync(string text);
    Task<Vector> GenerateTransactionEmbeddingAsync(string description, string? category = null);
}
```

Create `src/BudgetTracker.Api/Features/Intelligence/Search/AzureEmbeddingService.cs`:

```csharp
using Azure.AI.OpenAI;
using BudgetTracker.Api.Infrastructure;
using OpenAI.Embeddings;
using Pgvector;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public class AzureEmbeddingService : IAzureEmbeddingService
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly ILogger<AzureEmbeddingService> _logger;

    // Use text-embedding-3-small for cost efficiency (1536 dimensions)
    private const string EmbeddingModel = "text-embedding-3-small";
    private const int MaxBatchSize = 100; // Azure OpenAI batch limit

    public AzureEmbeddingService(
        IAzureOpenAIClientFactory clientFactory,
        ILogger<AzureEmbeddingService> logger)
    {
        _logger = logger;
        _openAIClient = clientFactory.CreateClient();
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        try
        {
            var client = _openAIClient.GetEmbeddingClient(EmbeddingModel);
            var response = await client.GenerateEmbeddingAsync(text);

            var embeddingValues = response.Value.ToFloats().ToArray();
            return new Vector(embeddingValues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text: {Text}", text[..Math.Min(text.Length, 50)]);
            throw;
        }
    }

    public async Task<Vector> GenerateTransactionEmbeddingAsync(string description, string? category = null)
    {
        // Combine description and category for richer semantic representation
        var text = string.IsNullOrEmpty(category)
            ? description
            : $"{description} [{category}]";

        return await GenerateEmbeddingAsync(text);
    }
}
```

## Step 42.6: Create Semantic Search Service

*Build the semantic search service that finds relevant transactions using vector similarity.*

Create `src/BudgetTracker.Api/Features/Intelligence/Search/ISemanticSearchService.cs`:

```csharp
using BudgetTracker.Api.Features.Transactions;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public interface ISemanticSearchService
{
    Task<List<Transaction>> FindRelevantTransactionsAsync(string queryText, string userId, int maxResults = 50);
}
```

Create `src/BudgetTracker.Api/Features/Intelligence/Search/SemanticSearchService.cs`:

```csharp
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public class SemanticSearchService : ISemanticSearchService
{
    private readonly BudgetTrackerContext _context;
    private readonly IAzureEmbeddingService _embeddingService;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        BudgetTrackerContext context,
        IAzureEmbeddingService embeddingService,
        ILogger<SemanticSearchService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<List<Transaction>> FindRelevantTransactionsAsync(
        string queryText,
        string userId,
        int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(queryText) || string.IsNullOrWhiteSpace(userId))
        {
            return new List<Transaction>();
        }

        try
        {
            // Generate embedding for the query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(queryText);
            var vectorString = queryEmbedding.ToString();

            // Use raw SQL with pgvector cosine_distance for efficient similarity search
            var similarTransactions = await _context.Transactions
                .FromSqlRaw(@"
                    SELECT *
                    FROM ""Transactions""
                    WHERE ""Embedding"" IS NOT NULL
                    AND ""UserId"" = {0}
                    ORDER BY cosine_distance(""Embedding"", {1}::vector) ASC
                    LIMIT {2}", userId, vectorString, maxResults)
                .ToListAsync();

            _logger.LogInformation("Found {Count} relevant transactions for query: {Query}",
                similarTransactions.Count, queryText[..Math.Min(queryText.Length, 50)]);

            return similarTransactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find relevant transactions for query: {Query}", queryText);
            return new List<Transaction>();
        }
    }
}
```

## Step 42.7: Build Natural Language Query Assistant

*Create the query assistant service that processes natural language questions.*

Create `src/BudgetTracker.Api/Features/Intelligence/Query/IQueryAssistantService.cs`:

```csharp
using BudgetTracker.Api.Features.Transactions;

namespace BudgetTracker.Api.Features.Intelligence.Query;

public interface IQueryAssistantService
{
    Task<QueryResponse> ProcessQueryAsync(string query, string userId);
}

public class QueryRequest
{
    public string Query { get; set; } = string.Empty;
}

public class QueryResponse
{
    public string Answer { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public List<TransactionDto>? Transactions { get; set; }
}
```

Create `src/BudgetTracker.Api/Features/Intelligence/Query/AzureAiQueryAssistantService.cs`:

```csharp
using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Intelligence.Search;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Query;

public class AzureAiQueryAssistantService : IQueryAssistantService
{
    private readonly BudgetTrackerContext _context;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly IAzureChatService _chatService;
    private readonly ILogger<AzureAiQueryAssistantService> _logger;

    public AzureAiQueryAssistantService(
        BudgetTrackerContext context,
        ISemanticSearchService semanticSearchService,
        IAzureChatService chatService,
        ILogger<AzureAiQueryAssistantService> logger)
    {
        _context = context;
        _semanticSearchService = semanticSearchService;
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<QueryResponse> ProcessQueryAsync(string query, string userId)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new QueryResponse { Answer = "Please provide a question about your finances." };
        }

        if (query.Length > 500)
        {
            return new QueryResponse { Answer = "Your question is too long. Please keep it under 500 characters." };
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return new QueryResponse { Answer = "User authentication required." };
        }

        try
        {
            var userTransactions = GetUserTransactions(userId);

            if (!await userTransactions.AnyAsync())
            {
                return new QueryResponse
                {
                    Answer =
                        "You don't have any transactions yet. Import some transactions to start asking questions about your finances."
                };
            }

            var relevantTransactions = await _semanticSearchService.FindRelevantTransactionsAsync(
                query, userId, maxResults: 10);

            var recentTransactions = userTransactions.Take(10).ToList();

            return await ProcessQueryDirectlyWithAi(query, recentTransactions, relevantTransactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process query: {Query} for user {UserId}", query, userId);
            return new QueryResponse
            {
                Answer = "I'm sorry, I couldn't process your question right now. Please try again later."
            };
        }
    }

    private IOrderedQueryable<Transaction> GetUserTransactions(string userId)
    {
        return _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date);
    }

    private async Task<QueryResponse> ProcessQueryDirectlyWithAi(string query, List<Transaction> transactions,
        List<Transaction> relevantTransactions)
    {
        var systemPrompt = CreateSystemPrompt();
        var userPrompt = CreateUserPrompt(query, transactions, relevantTransactions);

        var content = await _chatService.CompleteChatAsync(systemPrompt, userPrompt);
        return ParseAiResponse(content, transactions);
    }

    private static string CreateSystemPrompt()
    {
        return """
               You are a helpful financial assistant that answers questions about the user's spending and transactions.

               You can analyze spending patterns, find specific transactions, calculate totals, identify trends, and provide insights.
               Be conversational and helpful. Provide specific numbers, dates, and transaction details when relevant.

               The transactions provided to you have been semantically filtered to be most relevant to the user's query,
               so you're working with the most pertinent financial data for their question.

               When responding, provide:
               1. A clear, natural language answer to their question
               2. If relevant, include specific transaction details or amounts
               3. If showing multiple transactions, limit to the most relevant 3-5 items

               Always respond with JSON in this exact format:
               {
                 "answer": "Your natural language response here",
                 "amount": null or decimal value if relevant,
                 "transactions": null or array of relevant transaction objects
               }

               For transactions, use this format:
               {
                 "id": "transaction-guid",
                 "date": "YYYY-MM-DD",
                 "description": "transaction description",
                 "amount": decimal-value,
                 "category": "category-name-or-null",
                 "account": "account-name"
               }

               Examples of queries you can handle:
               - "What was my biggest expense last week?"
               - "Show me all Amazon purchases"
               - "What categories do I spend the most on?"
               - "Show me transactions over $100"
               - "When did I last go to Starbucks?"
               - "How much have I saved this year?"
               - "Find all my coffee-related expenses"
               - "Show me subscription services I'm paying for"
               - "What do I spend on transportation?"
               """;
    }

    private static string CreateUserPrompt(string query, List<Transaction> transactions,
        List<Transaction> relevantTransactions)
    {
        var earliestDate = transactions.Min(t => t.Date);
        var latestDate = transactions.Max(t => t.Date);
        var totalTransactions = transactions.Count;
        var totalIncome = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
        var totalExpenses = Math.Abs(transactions.Where(t => t.Amount < 0).Sum(t => t.Amount));

        // Get category breakdown
        var categoryBreakdown = transactions
            .Where(t => t.Amount < 0 && !string.IsNullOrEmpty(t.Category))
            .GroupBy(t => t.Category!)
            .Select(g => new { Category = g.Key, Total = Math.Abs(g.Sum(t => t.Amount)) })
            .OrderByDescending(c => c.Total)
            .Take(10)
            .ToList();

        // Get recent transactions sample
        var recentTransactions = transactions
            .OrderByDescending(t => t.Date)
            .Take(10)
            .Select(t => new
            {
                Id = t.Id,
                Date = t.Date.ToString("yyyy-MM-dd"),
                Description = t.Description,
                Amount = t.Amount,
                Category = t.Category,
                Account = t.Account
            })
            .ToList();

        var relatedTransactions = relevantTransactions
            .OrderByDescending(t => t.Date)
            .Take(10)
            .Select(t => new
            {
                Id = t.Id,
                Date = t.Date.ToString("yyyy-MM-dd"),
                Description = t.Description,
                Amount = t.Amount,
                Category = t.Category,
                Account = t.Account
            })
            .ToList();

        var transactionsJson =
            JsonSerializer.Serialize(recentTransactions, new JsonSerializerOptions { WriteIndented = false });

        var relevantTransactionsJson =
            JsonSerializer.Serialize(relatedTransactions, new JsonSerializerOptions { WriteIndented = false });

        return $"""
                User query: "{query}"

                Transaction Summary:
                - Total transactions: {totalTransactions}
                - Date range: {earliestDate:yyyy-MM-dd} to {latestDate:yyyy-MM-dd}
                - Total income: €{totalIncome:F2}
                - Total expenses: €{totalExpenses:F2}
                - Net amount: €{(totalIncome - totalExpenses):F2}

                Top spending categories:
                {string.Join("\n", categoryBreakdown.Select(c => $"- {c.Category}: €{c.Total:F2}"))}

                Recent transactions (sample of {recentTransactions.Count}):
                {transactionsJson}

                Relevant transactions for the prompt:
                {relevantTransactionsJson}

                Please analyze this data and answer the user's query. Include specific transaction details in your response when relevant.
                """;
    }

    private QueryResponse ParseAiResponse(string content, List<Transaction> transactions)
    {
        try
        {
            var jsonResponse = JsonSerializer.Deserialize<AiQueryResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (jsonResponse == null)
            {
                return new QueryResponse { Answer = "I couldn't process your question. Please try rephrasing it." };
            }

            var response = new QueryResponse
            {
                Answer = jsonResponse.Answer ?? "I processed your query but couldn't generate a response.",
                Amount = jsonResponse.Amount
            };

            // If AI provided transaction references, try to match them with actual transactions
            if (jsonResponse.Transactions == null || jsonResponse.Transactions.Count == 0) return response;

            var matchedTransactions = new List<TransactionDto>();

            foreach (var aiTransaction in jsonResponse.Transactions.Take(5))
            {
                if (Guid.TryParse(aiTransaction.Id, out var transactionId))
                {
                    var actualTransaction = transactions.FirstOrDefault(t => t.Id == transactionId);
                    if (actualTransaction != null)
                    {
                        matchedTransactions.Add(actualTransaction.MapToDto());
                        continue;
                    }
                }

                // If no exact match, create a DTO from AI response
                if (DateTime.TryParse(aiTransaction.Date, out var date))
                {
                    matchedTransactions.Add(new TransactionDto
                    {
                        Id = Guid.TryParse(aiTransaction.Id, out var id) ? id : Guid.NewGuid(),
                        Date = date,
                        Description = aiTransaction.Description ?? "Transaction",
                        Amount = aiTransaction.Amount,
                        Category = aiTransaction.Category,
                        Account = aiTransaction.Account ?? "Account",
                        ImportedAt = DateTime.UtcNow
                    });
                }
            }

            if (matchedTransactions.Count != 0)
            {
                response.Transactions = matchedTransactions;
            }

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response: {Content}", content);
            return new QueryResponse
            {
                Answer =
                    "I processed your question but had trouble formatting the response. Please try asking in a different way."
            };
        }
    }

    private class AiQueryResponse
    {
        public string? Answer { get; set; }
        public decimal? Amount { get; set; }
        public List<AiTransactionReference>? Transactions { get; set; }
    }

    private class AiTransactionReference
    {
        public string Id { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? Category { get; set; }
        public string? Account { get; set; }
    }
}
```

## Step 42.8: Create Query API Endpoints

*Create API endpoints for the natural language query functionality.*

Create `src/BudgetTracker.Api/Features/Intelligence/Query/QueryApi.cs`:

```csharp
using System.Security.Claims;
using BudgetTracker.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Api.Features.Intelligence.Query;

public static class QueryApi
{
    public static IEndpointRouteBuilder MapQueryEndpoints(this IEndpointRouteBuilder routes)
    {
        var queryGroup = routes.MapGroup("/query")
            .WithTags("Query Assistant")
            .WithOpenApi()
            .RequireAuthorization();

        queryGroup.MapPost("/ask", async (
            [FromBody] QueryRequest request,
            IQueryAssistantService queryService,
            ClaimsPrincipal claimsPrincipal) =>
        {
            var userId = claimsPrincipal.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var response = await queryService.ProcessQueryAsync(request.Query, userId);
            return Results.Ok(response);
        })
        .WithName("AskQuery")
        .WithSummary("Ask a natural language question about your finances")
        .WithDescription("Process natural language queries like 'What was my biggest expense last week?' or 'How much did I spend on groceries this month?'")
        .Produces<QueryResponse>()
        .ProducesProblem(400)
        .ProducesProblem(401);

        return routes;
    }
}
```

## Step 42.9: Background Service for Embedding Generation

*Create a background service to automatically generate embeddings for new transactions.*

Create `src/BudgetTracker.Api/Features/Intelligence/Search/EmbeddingBackgroundService.cs`:

```csharp
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public class EmbeddingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmbeddingBackgroundService> _logger;

    public EmbeddingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<EmbeddingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedding background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEmbeddings(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing embeddings");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Embedding background service stopped");
    }

    private async Task ProcessPendingEmbeddings(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerContext>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IAzureEmbeddingService>();

        var transactionsWithoutEmbeddings = await context.Transactions
            .Where(t => t.Embedding == null)
            .OrderBy(t => t.ImportedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (transactionsWithoutEmbeddings.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Processing {Count} transactions for embedding generation",
            transactionsWithoutEmbeddings.Count);

        foreach (var transaction in transactionsWithoutEmbeddings)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var embedding = await embeddingService.GenerateTransactionEmbeddingAsync(
                    transaction.Description, transaction.Category);

                transaction.Embedding = embedding;

                _logger.LogDebug("Generated embedding for transaction {TransactionId}: {Description}",
                    transaction.Id, transaction.Description[..Math.Min(transaction.Description.Length, 50)]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding for transaction {TransactionId}",
                    transaction.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Completed embedding generation for {Count} transactions",
            transactionsWithoutEmbeddings.Count);
    }
}
```

## Step 42.10: Update Program.cs with Service Registration

*Configure all services and register them in the dependency injection container.*

Update `src/BudgetTracker.Api/Program.cs`:

```csharp
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Intelligence.Query;
using BudgetTracker.Api.Features.Intelligence.Search;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<BudgetTrackerContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.UseVector());
});

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<BudgetTrackerContext>()
.AddDefaultTokenProviders();

// Authentication
builder.Services.AddAuthentication()
    .AddStaticApiKey();

builder.Services.Configure<StaticApiKeysConfiguration>(
    builder.Configuration.GetSection("StaticApiKeysConfiguration"));

// Azure OpenAI Configuration
var azureOpenAIConfig = new AzureOpenAIConfiguration();
builder.Configuration.GetSection("AzureOpenAI").Bind(azureOpenAIConfig);
builder.Services.AddSingleton(azureOpenAIConfig);

// AI Services
builder.Services.AddSingleton<IAzureOpenAIClientFactory, AzureOpenAIClientFactory>();
builder.Services.AddScoped<IAzureChatService, AzureChatService>();
builder.Services.AddScoped<IAzureEmbeddingService, AzureEmbeddingService>();
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddScoped<IQueryAssistantService, AzureAiQueryAssistantService>();

// Background Services
builder.Services.AddHostedService<EmbeddingBackgroundService>();

// API Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(corsBuilder =>
    {
        corsBuilder
            .WithOrigins("http://localhost:5173")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapAuthEndpoints();
app.MapQueryEndpoints();

app.Run();
```

## Step 42.11: Create Database Migration

*Generate and apply database migration for the new schema with vector support.*

Run the following commands from `src/BudgetTracker.Api/`:

```bash
# Generate migration for vector support
dotnet ef migrations add AddPgVectorEmbeddingWithDimensions

# Update database
dotnet ef database update
```

## Step 42.12: Add Configuration Settings

*Configure Azure OpenAI settings in appsettings.*

Update `src/BudgetTracker.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=budgettracker;Username=budgetuser;Password=budgetpass123"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "ChatModel": "gpt-4o-mini",
    "EmbeddingModel": "text-embedding-3-small"
  },
  "StaticApiKeysConfiguration": {
    "HeaderName": "X-API-Key",
    "ApiKeys": [
      {
        "Key": "test-key-user1",
        "UserId": "user1"
      }
    ]
  }
}
```

## Step 42.13: Create Frontend Query Assistant Component

*Build the React component for the natural language query interface.*

Create `src/BudgetTracker.Web/src/features/intelligence/api.ts`:

```typescript
import { apiClient } from '../../api/apiClient';

export interface QueryRequest {
  query: string;
}

export interface TransactionDto {
  id: string;
  date: string;
  description: string;
  amount: number;
  balance?: number;
  category?: string;
  labels?: string;
  importedAt: string;
  account: string;
}

export interface QueryResponse {
  answer: string;
  amount?: number;
  transaction?: TransactionDto;
  transactions?: TransactionDto[];
}

export const intelligenceApi = {
  askQuery: async (query: string): Promise<QueryResponse> => {
    const response = await apiClient.post<QueryResponse>('/query/ask', { query });
    return response.data;
  }
};
```

Create `src/BudgetTracker.Web/src/features/intelligence/components/QueryAssistant.tsx`:

```tsx
import { useState } from 'react';
import { useToast } from '../../../shared/contexts/ToastContext';
import { intelligenceApi, type QueryResponse } from '../api';
import Card from '../../../shared/components/Card';
import { LoadingSpinner } from '../../../shared/components/LoadingSpinner';
import { formatCurrency, formatDate } from '../../../shared/utils/formatters';

const MessageIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="lucide lucide-message-circle">
    <path d="M7.9 20A9 9 0 1 0 4 16.1L2 22Z" />
  </svg>
);

const SendIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="lucide lucide-send">
    <path d="m22 2-7 20-4-9-9-4Z" />
    <path d="M22 2 11 13" />
  </svg>
);

interface QueryAssistantProps {
  className?: string;
}

export default function QueryAssistant({ className = "" }: QueryAssistantProps) {
  const [query, setQuery] = useState('');
  const [response, setResponse] = useState<QueryResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const { showToast } = useToast();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim() || isLoading) return;

    setIsLoading(true);
    try {
      const result = await intelligenceApi.askQuery(query.trim());
      setResponse(result);
    } catch (error) {
      console.error('Query failed:', error);
      showToast('error', 'Failed to process your query. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const suggestedQueries = [
    "What was my biggest expense last week?",
    "How much did I spend on groceries this month?",
    "Show me all transactions over €100",
    "What's my average daily spending?",
    "Which category do I spend the most on?",
    "When did I last go to Starbucks?"
  ];

  return (
    <Card className={`p-6 ${className}`}>
      <div className="flex items-center gap-3 mb-4">
        <div className="p-2 bg-primary/10 rounded-lg">
          <MessageIcon />
        </div>
        <div>
          <h3 className="font-semibold text-card-foreground">Ask about your finances</h3>
          <p className="text-sm text-muted-foreground">Ask me anything about your spending and transactions</p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="mb-4">
        <div className="flex gap-2">
          <input
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Ask a question about your finances..."
            className="flex-1 px-3 py-2 border border-input rounded-md text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-transparent"
            disabled={isLoading}
          />
          <button
            type="submit"
            disabled={!query.trim() || isLoading}
            className="px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            {isLoading ? <LoadingSpinner size="sm" /> : <SendIcon />}
            Ask
          </button>
        </div>
      </form>

      {!response && !isLoading && (
        <div className="space-y-2">
          <p className="text-sm text-muted-foreground mb-2">Try asking:</p>
          <div className="flex flex-wrap gap-2">
            {suggestedQueries.map((suggestion, index) => (
              <button
                key={index}
                onClick={() => setQuery(suggestion)}
                className="text-xs px-3 py-1 bg-muted text-muted-foreground rounded-full hover:bg-muted/80 transition-colors"
              >
                {suggestion}
              </button>
            ))}
          </div>
        </div>
      )}

      {response && (
        <div className="mt-4 p-4 bg-muted/50 rounded-lg">
          <p className="text-sm text-card-foreground mb-3">{response.answer}</p>

          {response.transaction && (
            <div className="mt-3 p-3 bg-background rounded-md border">
              <div className="flex justify-between items-start">
                <div className="flex-1">
                  <p className="font-medium text-sm">{response.transaction.description}</p>
                  <p className="text-xs text-muted-foreground mt-1">
                    {formatDate(response.transaction.date)} • {response.transaction.account}
                  </p>
                  {response.transaction.category && (
                    <span className="inline-block mt-1 px-2 py-1 bg-primary/10 text-primary text-xs rounded-full">
                      {response.transaction.category}
                    </span>
                  )}
                </div>
                <div className="text-right">
                  <p className={`font-medium text-sm ${response.transaction.amount < 0 ? 'text-red-600' : 'text-green-600'}`}>
                    {response.transaction.amount < 0 ? '-' : '+'}{formatCurrency(response.transaction.amount)}
                  </p>
                </div>
              </div>
            </div>
          )}

          {response.transactions && response.transactions.length > 0 && (
            <div className="mt-3 space-y-2">
              {response.transactions.slice(0, 3).map((transaction: any) => (
                <div key={transaction.id} className="p-3 bg-background rounded-md border">
                  <div className="flex justify-between items-start">
                    <div className="flex-1">
                      <p className="font-medium text-sm">{transaction.description}</p>
                      <p className="text-xs text-muted-foreground mt-1">
                        {formatDate(transaction.date)} • {transaction.account}
                      </p>
                      {transaction.category && (
                        <span className="inline-block mt-1 px-2 py-1 bg-primary/10 text-primary text-xs rounded-full">
                          {transaction.category}
                        </span>
                      )}
                    </div>
                    <div className="text-right">
                      <p className={`font-medium text-sm ${transaction.amount < 0 ? 'text-red-600' : 'text-green-600'}`}>
                        {transaction.amount < 0 ? '-' : '+'}{formatCurrency(transaction.amount)}
                      </p>
                    </div>
                  </div>
                </div>
              ))}
              {response.transactions.length > 3 && (
                <p className="text-xs text-muted-foreground text-center">
                  ... and {response.transactions.length - 3} more transactions
                </p>
              )}
            </div>
          )}
        </div>
      )}
    </Card>
  );
}
```

Create `src/BudgetTracker.Web/src/features/intelligence/index.ts`:

```typescript
export { default as QueryAssistant } from './components/QueryAssistant';
export { intelligenceApi } from './api';
export type { QueryResponse, TransactionDto } from './api';
```

## Step 42.14: Add Query Assistant to Dashboard

*Integrate the Query Assistant component into the main dashboard.*

Update `src/BudgetTracker.Web/src/routes/dashboard.tsx`:

```tsx
import { QueryAssistant } from '../features/intelligence';

export default function Dashboard() {
  return (
    <div className="container mx-auto px-4 py-8 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold text-foreground">Dashboard</h1>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <QueryAssistant className="lg:col-span-2" />

        {/* Add other dashboard components here */}
        <div className="bg-card text-card-foreground p-6 rounded-lg border">
          <h2 className="text-xl font-semibold mb-4">Recent Transactions</h2>
          <p className="text-muted-foreground">Transaction list will be implemented in future steps.</p>
        </div>

        <div className="bg-card text-card-foreground p-6 rounded-lg border">
          <h2 className="text-xl font-semibold mb-4">Spending Summary</h2>
          <p className="text-muted-foreground">Spending charts will be implemented in future steps.</p>
        </div>
      </div>
    </div>
  );
}
```

## Step 42.15: Test the Semantic Search System

*Test the complete RAG semantic search and natural language query system.*

### 42.15.1: Add Sample Data

First, you'll need some transaction data to test with. You can add sample transactions directly to the database or create an import endpoint. For testing, add a few transactions directly:

```sql
-- Sample transactions for testing (run in PostgreSQL)
INSERT INTO "Transactions" ("Id", "Date", "Description", "Amount", "Account", "UserId", "ImportedAt") VALUES
('550e8400-e29b-41d4-a716-446655440001', '2025-01-15'::timestamp, 'Starbucks Coffee Downtown', -5.99, 'Checking Account', 'user1', NOW()),
('550e8400-e29b-41d4-a716-446655440002', '2025-01-16'::timestamp, 'Amazon Purchase - Electronics', -299.99, 'Checking Account', 'user1', NOW()),
('550e8400-e29b-41d4-a716-446655440003', '2025-01-17'::timestamp, 'Salary Deposit', 3500.00, 'Checking Account', 'user1', NOW()),
('550e8400-e29b-41d4-a716-446655440004', '2025-01-18'::timestamp, 'Netflix Subscription', -15.99, 'Checking Account', 'user1', NOW()),
('550e8400-e29b-41d4-a716-446655440005', '2025-01-19'::timestamp, 'Coffee Bean & Tea Leaf', -7.50, 'Checking Account', 'user1', NOW()),
('550e8400-e29b-41d4-a716-446655440006', '2025-01-20'::timestamp, 'Whole Foods Groceries', -89.45, 'Checking Account', 'user1', NOW());
```

### 42.15.2: Test Embedding Generation

Start the application and wait for the background service to generate embeddings:

```bash
# Start the backend
cd src/BudgetTracker.Api
dotnet run

# Start the frontend
cd src/BudgetTracker.Web
npm run dev
```

Monitor the logs to see embedding generation in progress.

### 42.15.3: Test Natural Language Queries

Test various query types via the API:

```http
### Test coffee-related query
POST http://localhost:5000/query/ask
X-API-Key: test-key-user1
Content-Type: application/json

{
  "query": "Show me all my coffee purchases"
}

### Test expense analysis
POST http://localhost:5000/query/ask
X-API-Key: test-key-user1
Content-Type: application/json

{
  "query": "What was my biggest expense this month?"
}

### Test category analysis
POST http://localhost:5000/query/ask
X-API-Key: test-key-user1
Content-Type: application/json

{
  "query": "How much did I spend on entertainment?"
}
```

### 42.15.4: Test Frontend Interface

Open http://localhost:5173 and test the Query Assistant:

**Expected Results:**
- ✅ **Coffee query**: Should find both Starbucks and Coffee Bean transactions
- ✅ **Biggest expense**: Should identify the Amazon purchase ($299.99)
- ✅ **Entertainment spending**: Should find Netflix subscription
- ✅ **Semantic matching**: Coffee-related queries should work even without exact keyword matches
- ✅ **Contextual responses**: AI should provide natural language explanations with specific amounts and dates

---

## Summary ✅

You've successfully implemented a complete RAG semantic search system with natural language query capabilities:

✅ **Vector Database**: PostgreSQL with pgvector extension for semantic search
✅ **Embedding Generation**: Azure OpenAI text-embedding-3-small for vector creation
✅ **Semantic Search**: Cosine similarity search over transaction embeddings
✅ **Natural Language Processing**: GPT-4o-mini for conversational query understanding
✅ **RAG Implementation**: Retrieval-Augmented Generation combining semantic search with AI reasoning
✅ **Background Processing**: Automatic embedding generation for new transactions
✅ **Query Assistant UI**: React component for natural language financial queries
✅ **API Integration**: RESTful endpoints for query processing

**Key Features Implemented**:
- **Semantic Search**: Find transactions by meaning, not just keywords
- **Natural Language Queries**: Ask questions in plain English about your finances
- **RAG Pattern**: Combine relevant transaction retrieval with AI-generated responses
- **Vector Embeddings**: Rich semantic representation of transaction data
- **Background Processing**: Automatic embedding generation without user intervention
- **Contextual Responses**: AI provides specific amounts, dates, and transaction details

**Technical Achievements**:
- **pgvector Integration**: Native PostgreSQL vector storage and similarity search
- **Azure OpenAI Integration**: Both embedding and chat completion APIs
- **Efficient Search**: Raw SQL with vector operations for performance
- **Type Safety**: Full TypeScript interfaces for all AI interactions
- **Error Handling**: Graceful fallbacks for AI service failures
- **Scalable Architecture**: Background services for processing at scale

**What Users Get**:
- **Conversational Finance**: Ask questions like "What's my biggest coffee expense?"
- **Smart Discovery**: Find transactions by concept, not exact words
- **Instant Insights**: Get immediate answers about spending patterns and trends
- **Natural Interface**: No need to learn query syntax or filter interfaces
- **Contextual Understanding**: AI understands financial context and provides relevant details

The system now provides powerful semantic search capabilities that make financial data exploration intuitive and conversational! 🎉

**Next Steps**: You can extend this system by adding more sophisticated query types, implementing transaction categorization, or building recommendation engines using the same RAG patterns.