using BudgetTracker.Api.AntiForgery;
using BudgetTracker.Api.Auth;
using Microsoft.EntityFrameworkCore;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Features.Transactions.Import.Enhancement;
using BudgetTracker.Api.Features.Transactions.Import.Detection;
using BudgetTracker.Api.Features.Intelligence;
using BudgetTracker.Api.Features.Intelligence.Search;
using BudgetTracker.Api.Features.Intelligence.Query;
using BudgetTracker.Api.Features.Intelligence.Recommendations;
using BudgetTracker.Api.Features.Analytics;
using BudgetTracker.Api.Features.Analytics.Insights;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Budget Tracker API",
        Version = "v1",
        Description = "A minimal API for budget tracking with user authentication"
    });

    // Add API Key authentication
    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "API Key needed to access the endpoints. Enter your API key in the text input below.",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-API-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });

    // Make API Key required for all endpoints
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            new string[] {}
        }
    });
});

// Add Entity Framework
builder.Services.AddDbContext<BudgetTrackerContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseVector()));

// Add import services
// Add CSV detection services
builder.Services.AddScoped<ICsvStructureDetector, CsvStructureDetector>();
builder.Services.AddScoped<ICsvDetector, CsvDetector>();
builder.Services.AddScoped<ICsvAnalyzer, CsvAnalyzer>();
builder.Services.AddScoped<IImageImporter, ImageImporter>();
builder.Services.AddScoped<CsvImporter>();

// Configure Azure AI
builder.Services.Configure<AzureAiConfiguration>(
    builder.Configuration.GetSection(AzureAiConfiguration.SectionName));

// Azure OpenAI services
builder.Services.AddScoped<IAzureOpenAIClientFactory, AzureOpenAIClientFactory>();
builder.Services.AddScoped<IAzureChatService, AzureChatService>();

// Register TransactionEnhancer with all its dependencies
builder.Services.AddScoped<ITransactionEnhancer, TransactionEnhancer>();

// Register embedding service for vector generation
builder.Services.AddScoped<IAzureEmbeddingService, AzureEmbeddingService>();

// Register semantic search and query services
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddScoped<IQueryAssistantService, AzureAiQueryAssistantService>();

// Register background service for automatic embedding generation
builder.Services.AddHostedService<EmbeddingBackgroundService>();

// Add analytics services
builder.Services.AddScoped<IInsightsService, AzureAiInsightsService>();

// Add recommendation services
builder.Services.AddScoped<IRecommendationRepository, RecommendationAgent>();
builder.Services.AddScoped<IRecommendationWorker, RecommendationProcessor>();
builder.Services.AddHostedService<RecommendationBackgroundService>();

// Add Auth with multiple schemes
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("Identity.Application", "StaticApiKey")
        .RequireAuthenticatedUser()
        .Build();
});

// Add Identity
builder.Services
    .AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<BudgetTrackerContext>();

// Configure Static API Keys
builder.Services.Configure<StaticApiKeysConfiguration>(
    builder.Configuration.GetSection(StaticApiKeysConfiguration.SectionName));

// Add Static API Key Authentication
builder.Services.AddAuthentication()
    .AddScheme<StaticApiKeyAuthenticationSchemeOptions, StaticApiKeyAuthenticationHandler>("StaticApiKey", options =>
    {
        var staticApiKeysConfig = builder.Configuration.GetSection(StaticApiKeysConfiguration.SectionName).Get<StaticApiKeysConfiguration>();
        if (staticApiKeysConfig?.Keys != null)
        {
            // Map each configured API key to its associated user ID
            foreach (var keyConfig in staticApiKeysConfig.Keys)
            {
                var apiKey = keyConfig.Key;
                var keyInfo = keyConfig.Value;
                options.ValidApiKeys[apiKey] = keyInfo.UserId;
            }
        }
    });

// Add Anti-forgery services
builder.Services.AddAntiforgery(options =>
{
    options.SuppressXFrameOptionsHeader = false;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalDevelopment", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "https://localhost:5173", "http://localhost:3001") // TODO: Update with configurable origins
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Apply migrations at startup
// This is suitable for development but not recommended for production scenarios.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerContext>();
    context.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowLocalDevelopment");
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Map feature endpoints
app.MapGet("/", () => "API");
app
    .MapGroup("/api")
    .MapAntiForgeryEndpoints()
    .MapAuthEndpoints()
    .MapTransactionEndpoints()
    .MapIntelligenceEndpoints()
    .MapAnalyticsEndpoints();

app.Run();