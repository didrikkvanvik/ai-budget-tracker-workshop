# Workshop Step 031: Add Helicone LLM Observability

## Mission 🎯

In this step, you'll add comprehensive LLM observability to your budget tracking application using Helicone. You'll learn how to monitor AI requests, track costs, analyze performance, and debug LLM interactions in real-time.

**Your goal**: Integrate Helicone observability into your existing AI-powered transaction enhancement system to gain insights into LLM usage, costs, and performance.

**Learning Objectives**:
- Understanding LLM observability and its importance
- Setting up Helicone for Azure OpenAI monitoring
- Configuring secure API key management with .NET user secrets
- Implementing request/response logging and cost tracking
- Using Helicone dashboard for performance analysis and debugging
- Measuring and optimizing LLM performance

---

## Prerequisites

**Required**: You need an Azure OpenAI service with a deployed model (e.g., gpt-4o-mini).

---

## Branches

**Starting branch:** `022-smart-csv`
**Solution branch:** `031-add-helicone`

---

## Step 31.1: Create Helicone Account and Get API Key

*Set up a Helicone account to start monitoring your LLM requests.*

### 31.1.1: Sign up for Helicone

1. **Visit Helicone**: Go to [https://helicone.ai](https://helicone.ai)
2. **Create Account**: Create an account with your email or GitHub account (No credit card required, 7-day free trial). Use the European Data region.
3. **Verify Email**: Complete email verification if required
4. **Access Dashboard**: Once logged in, you'll see the Helicone dashboard

### 31.1.2: Generate API Key

1. **Navigate to Settings**: Click on your profile → "Settings"
2. **API Keys Section**: Go to the "API Keys" tab
3. **Create New Key**: Click "Create New API Key"
4. **Name Your Key**: Give it a descriptive name like "Budget Tracker Workshop". Read and Write permissions.
5. **Copy Key**: Save the API key securely - you'll need it in the next step

**Important**: Keep your Helicone API key secure and never commit it to version control.

---

## Step 31.2: Configure User Secrets for API Keys

*Set up secure storage for sensitive API keys using .NET user secrets instead of configuration files.*

### 31.2.1: Initialize User Secrets

Navigate to your API project directory and initialize user secrets:

```bash
cd src/BudgetTracker.Api
dotnet user-secrets init
```

This adds a `UserSecretsId` to your project file automatically.

### 31.2.2: Configure Helicone Headers

Add your Helicone API key to user secrets:

```bash
# Replace with your actual Helicone API key
dotnet user-secrets set "AzureAI:Headers:Helicone-Auth" "Bearer sk-helicone-xxxxx"
dotnet user-secrets set "AzureAI:Headers:Helicone-OpenAI-Api-Base" "https://your-resource.openai.azure.com/"
```

### 31.2.3: Switch to Helicone Endpoint

Update the endpoint to route through Helicone:

```bash
# Change endpoint to Helicone proxy
dotnet user-secrets set "AzureAI:Endpoint" "https://oai.helicone.ai"
```

---

## Step 31.3: Add Headers Support to Configuration

*Add the Headers dictionary to AzureAiConfiguration to support custom headers.*

### 31.3.1: Update AzureAiConfiguration

Update `src/BudgetTracker.Api/Infrastructure/AzureAiConfiguration.cs` to add Headers support:

```csharp
namespace BudgetTracker.Api.Infrastructure;

public class AzureAiConfiguration
{
    public const string SectionName = "AzureAI";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
}
```

**Note**: The `Headers` property allows us to add custom headers like Helicone authentication headers to all Azure OpenAI requests.

---

## Step 31.4: Create Custom Header Policy

*Create a policy class to inject custom headers into Azure OpenAI requests.*

### 31.4.1: Create CustomHeaderPolicy

Create `src/BudgetTracker.Api/Infrastructure/CustomHeaderPolicy.cs`:

```csharp
using System.ClientModel.Primitives;

namespace BudgetTracker.Api.Infrastructure;

public class CustomHeaderPolicy : PipelinePolicy
{
    private readonly string _headerName;
    private readonly string _headerValue;

    public CustomHeaderPolicy(string headerName, string headerValue)
    {
        _headerName = headerName;
        _headerValue = headerValue;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Add(_headerName, _headerValue);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Add(_headerName, _headerValue);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }
}
```

---

## Step 31.5: Create Azure OpenAI Client Factory Interface

*Create an interface for the Azure OpenAI client factory to enable dependency injection.*

### 31.5.1: Create IAzureOpenAIClientFactory

Create `src/BudgetTracker.Api/Infrastructure/IAzureOpenAIClientFactory.cs`:

```csharp
using Azure.AI.OpenAI;

namespace BudgetTracker.Api.Infrastructure;

public interface IAzureOpenAIClientFactory
{
    AzureOpenAIClient CreateClient();
}
```

---

## Step 31.6: Create Azure OpenAI Client Factory

*Create the Azure OpenAI client factory implementation that supports custom headers.*

### 31.6.1: Create AzureOpenAIClientFactory

Create `src/BudgetTracker.Api/Infrastructure/AzureOpenAIClientFactory.cs`:

```csharp
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Infrastructure;

public class AzureOpenAIClientFactory : IAzureOpenAIClientFactory
{
    private readonly AzureAiConfiguration _configuration;

    public AzureOpenAIClientFactory(IOptions<AzureAiConfiguration> configuration)
    {
        _configuration = configuration.Value;
    }

    public AzureOpenAIClient CreateClient()
    {
        if (string.IsNullOrEmpty(_configuration.Endpoint) || string.IsNullOrEmpty(_configuration.ApiKey))
        {
            throw new InvalidOperationException(
                "Azure AI configuration is missing. Please configure Endpoint and ApiKey.");
        }

        var options = new AzureOpenAIClientOptions();

        foreach (var header in _configuration.Headers)
        {
            options.AddPolicy(new CustomHeaderPolicy(header.Key, header.Value), PipelinePosition.PerCall);
        }

        return new AzureOpenAIClient(
            new Uri(_configuration.Endpoint),
            new Azure.AzureKeyCredential(_configuration.ApiKey),
            options);
    }
}
```

---

## Step 31.7: Update Azure Chat Service

*Modify the Azure Chat Service to use the factory pattern for creating Azure OpenAI clients.*

### 31.7.1: Update AzureChatService

Update `src/BudgetTracker.Api/Infrastructure/AzureChatService.cs` to use the factory:

```csharp
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace BudgetTracker.Api.Infrastructure;

public class AzureChatService : IAzureChatService
{
    private readonly AzureOpenAIClient _openAiClient;
    private readonly string _deploymentName;

    public AzureChatService(
        IAzureOpenAIClientFactory clientFactory,
        IOptions<AzureAiConfiguration> configuration)
    {
        var config = configuration.Value;
        _deploymentName = config.DeploymentName;
        _openAiClient = clientFactory.CreateClient();
    }

    public async Task<string> CompleteChatAsync(string systemPrompt, string userPrompt)
    {
        var client = _openAiClient.GetChatClient(_deploymentName);
        var response = await client.CompleteChatAsync(
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt));

        return response.Value.Content[0].Text;
    }

    public async Task<string> CompleteChatAsync(IEnumerable<ChatMessage> messages)
    {
        var client = _openAiClient.GetChatClient(_deploymentName);
        var response = await client.CompleteChatAsync(messages);
        return response.Value.Content[0].Text;
    }
}
```

---

## Step 31.8: Update Configuration Structure

*Add the Headers structure to support Helicone integration.*

### 31.8.1: Update appsettings.json Structure

Update `src/BudgetTracker.Api/appsettings.json` to include the `Headers` structure:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AzureAI": {
    "Endpoint": "",
    "ApiKey": "",
    "DeploymentName": "",
    "Headers": {}
  },
  "AllowedHosts": "*"
}
```

**Important**:
- The `Headers` object enables the system to read Helicone headers from configuration
- All sensitive values (Endpoint, ApiKey, Headers) remain empty here
- User secrets will populate these values securely
- This structure is available to all environments

### 31.8.2: Register Services

Update `src/BudgetTracker.Api/Program.cs` to register the new services:

```csharp
// Configuration binding (should already exist)
builder.Services.Configure<AzureAiConfiguration>(
    builder.Configuration.GetSection(AzureAiConfiguration.SectionName));

// Azure OpenAI services (add these)
builder.Services.AddScoped<IAzureOpenAIClientFactory, AzureOpenAIClientFactory>();
builder.Services.AddScoped<IAzureChatService, AzureChatService>();
```

**Important**: These service registrations are required for dependency injection to work correctly with the factory pattern.

---

## Step 31.9: Test Helicone Integration

*Verify that Helicone is properly capturing and logging your LLM requests.*

### 31.9.1: Build and Run the Application

```bash
# Build the solution to ensure everything compiles
dotnet build

# Start the database
cd docker
docker-compose up -d

# Run the API
cd ../src/BudgetTracker.Api
dotnet run
```

### 31.9.2: Test AI Enhancement with Sample Data

Upload a sample CSV file to trigger AI enhancement:

```http
### Test AI enhancement with Helicone tracking
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="sample-transactions.csv"
Content-Type: text/csv

Date,Description,Amount,Balance
2025-01-15,AMZN MKTP US*123456789,-45.67,1250.33
2025-01-16,STARBUCKS COFFEE #1234,-5.89,1244.44
2025-01-17,DD VODAFONE PORTU 222111000,-52.30,3676.15
--WebAppBoundary--
```

### 31.9.3: Verify Helicone Dashboard

1. **Access Helicone Dashboard**: Go to [https://app.helicone.ai](https://app.helicone.ai)
2. **Login**: Use your Helicone account credentials
3. **Check Requests**: You should see your AI requests appear in the dashboard
4. **Inspect Details**: Click on individual requests to see:
   - Request/response payloads
   - Token usage and costs
   - Response times
   - Custom properties

**Expected Results**:
- ✅ Requests appear in Helicone dashboard within 30 seconds
- ✅ Request details show Azure OpenAI endpoint routing
- ✅ Token counts and estimated costs are displayed
- ✅ Custom properties like "service" and "environment" are visible

---

## Step 31.10: Monitor and Analyze LLM Performance

*Use Helicone's analytics features to understand your AI usage patterns.*

### 31.10.1: Key Metrics to Monitor

In the Helicone dashboard, focus on these important metrics:

**Cost Analysis**:
- Total daily/weekly/monthly costs
- Cost per request
- Token usage patterns
- Most expensive operations

**Performance Metrics**:
- Average response time
- Success/failure rates
- Rate limiting issues
- Error patterns

**Usage Patterns**:
- Request volume over time
- Peak usage hours
- Feature adoption (enhancement vs. categorization)
- User behavior analysis

### 31.10.2: Set Up Alerts (Optional)

1. **Cost Alerts**: Set up notifications when daily costs exceed thresholds
2. **Performance Alerts**: Monitor when response times spike
3. **Error Alerts**: Get notified of API failures or rate limits

### 31.10.3: Optimize Based on Insights

Use Helicone data to optimize your AI integration:

**Cost Optimization**:
- Identify expensive prompts that could be shortened
- Find opportunities to use smaller models for simple tasks
- Implement request caching for repeated queries

**Performance Optimization**:
- Monitor slow requests and optimize prompts
- Identify bottlenecks in your AI pipeline
- Adjust retry strategies based on error patterns

---

## Summary ✅

You've successfully integrated Helicone LLM observability into your budget tracking application:

✅ **Account Setup**: Created Helicone account and obtained API keys
✅ **Secure Configuration**: Used .NET user secrets for sensitive API keys
✅ **Service Integration**: Configured Azure OpenAI client to route through Helicone
✅ **Request Tracking**: All AI requests are now logged and monitored
✅ **Custom Metadata**: Added service categorization and custom properties
✅ **Dashboard Access**: Real-time monitoring of AI requests and performance
✅ **Cost Tracking**: Visibility into token usage and LLM costs

**Key Features Implemented**:
- **Complete Observability**: Every LLM request is tracked with full context
- **Cost Monitoring**: Real-time tracking of AI usage costs
- **Performance Analytics**: Response time and success rate monitoring
- **Error Tracking**: Detailed logging of failures and debugging information
- **Custom Properties**: Service categorization for better organization
- **Secure Configuration**: API keys stored safely using .NET user secrets

**Technical Achievements**:
- **Proxy Integration**: Seamless routing through Helicone without code changes
- **Metadata Enrichment**: Custom properties for enhanced request categorization
- **Configuration Management**: Secure handling of sensitive API credentials
- **Service Factory Pattern**: Clean architecture for client creation
- **Environment Separation**: Workshop-specific tagging and organization

**What You Get**:
- **Cost Insights**: Understand exactly how much your AI features cost
- **Performance Monitoring**: Track response times and identify bottlenecks
- **Usage Analytics**: See which AI features are most popular
- **Debugging Tools**: Full request/response logging for troubleshooting
- **Alert Capabilities**: Proactive monitoring of costs and performance
- **Optimization Data**: Insights to improve prompts and reduce costs

**Security Benefits**:
- **No Exposed Keys**: API keys stored in user secrets, not in source code
- **Environment Isolation**: Clear separation between workshop and production
- **Request Isolation**: User-specific tracking without data leakage
- **Audit Trail**: Complete history of AI requests and responses

The system now provides comprehensive observability for all AI operations, giving you the insights needed to optimize performance, manage costs, and debug issues effectively! 🔍✨

**Next Steps**:
- Monitor your AI usage patterns in the Helicone dashboard
- Set up cost and performance alerts
- Use insights to optimize prompts and reduce costs
- Consider implementing request caching for repeated queries
- Explore Helicone's advanced features like A/B testing and prompt versioning