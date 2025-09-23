# Workshop Step 061: MCP Server for AI Integration

## Mission 🎯

In this step, you'll build a Model Context Protocol (MCP) server that enables AI assistants to interact with your Budget Tracker through natural language. The MCP server will provide tools for querying transactions and importing CSV files, making your budget tracker accessible to AI assistants like Claude through Cursor IDE.

**Your goal**: Create a standalone MCP server using the Microsoft MCP SDK that provides secure access to your Budget Tracker API for natural language queries and CSV imports.

**Learning Objectives**:
- Understanding the Model Context Protocol (MCP) and its role in AI integration
- Building MCP tools with the Microsoft MCP SDK
- Implementing static API key authentication for secure tool access
- Creating natural language interfaces for financial data
- Integrating CSV import capabilities through MCP tools
- Configuring Cursor IDE to use your MCP server

---

## Prerequisites

Before starting, ensure you completed:
- [054-add-category-spending-tool.md](054-add-category-spending-tool.md) - AI Recommendation Agent
- Your Budget Tracker API is running with static API key authentication enabled

---

## Branches

**Starting branch:** `054-add-category-spending-tool`  
**Solution branch:** `061-mcp-server`  

---

## Step 61.1: Create MCP Server Project

*Set up a standalone console application using the Microsoft MCP SDK.*

The MCP server will be a separate console application that communicates with AI assistants using the Model Context Protocol. This approach provides proper protocol compliance and separation of concerns from your main API.

Create a new console project in the solution:

```bash
# From the solution root
dotnet new console -n BudgetTracker.McpServer -o src/BudgetTracker.McpServer
dotnet sln add src/BudgetTracker.McpServer
```

Update `src/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="9.0.0" />
    <PackageReference Include="ModelContextProtocol" Version="0.3.0-preview.4" />
  </ItemGroup>

</Project>
```

## Step 61.2: Implement Basic MCP Server Structure

*Create the MCP server with basic hosting infrastructure and stdio transport.*

The MCP server uses stdio (standard input/output) transport to communicate with AI assistants. This is the standard approach for MCP protocol compliance.

Replace `src/BudgetTracker.McpServer/Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (required for MCP)
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Add configuration
builder.Services.Configure<BudgetTrackerConfiguration>(
    builder.Configuration.GetSection("BudgetTracker"));

// Add HTTP client for API calls
builder.Services.AddHttpClient();

// Add MCP server with tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

[McpServerToolType]
public static class BudgetTrackerTools
{
    [McpServerTool, Description("Query recent transactions from your budget tracker")]
    public static async Task<string> QueryTransactions(
        [Description("Natural language question about your transactions")]
        string question,
        IServiceProvider serviceProvider)
    {
        var httpClient = serviceProvider.GetRequiredService<HttpClient>();
        var apiKey = Environment.GetEnvironmentVariable("BUDGET_TRACKER_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            return "❌ API key not configured. Please set BUDGET_TRACKER_API_KEY environment variable.";
        }

        try
        {
            // Add API key to request headers
            httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

            // Call the Budget Tracker API query endpoint (authenticated)
            var apiBaseUrl = GetApiBaseUrl(serviceProvider);
            var response = await httpClient.PostAsJsonAsync($"{apiBaseUrl}/api/query/ask",
                new { Query = question });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return result;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return "❌ Invalid API key. Please check your configuration.";
            }
            else
            {
                return $"Error querying transactions: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            return $"Error connecting to Budget Tracker API: {ex.Message}";
        }
    }

    private static string GetApiBaseUrl(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IOptions<BudgetTrackerConfiguration>>();
        return configuration.Value.ApiBaseUrl ?? "http://localhost:5295";
    }
}

public class BudgetTrackerConfiguration
{
    public string? ApiBaseUrl { get; set; }
    public TimeSpan? DefaultTimeout { get; set; }
}
```

Create `src/BudgetTracker.McpServer/appsettings.json`:

```json
{
  "BudgetTracker": {
    "ApiBaseUrl": "http://localhost:5295",
    "DefaultTimeout": "00:00:30"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Step 61.3: Add CSV Import Tool

*Implement CSV import functionality through the MCP server using Base64 encoding.*

This tool enables AI assistants to help users import CSV files by accepting Base64-encoded file content and proxying it to your existing import API.

Add the CSV import tool and helper methods to the `BudgetTrackerTools` class in `Program.cs`:

```csharp
[McpServerTool, Description("Import transactions from a CSV file to your budget tracker")]
public static async Task<string> ImportTransactionsCsv(
    [Description("Base64-encoded CSV file content")]
    string csvContent,
    [Description("Original filename for tracking")]
    string fileName,
    [Description("Account name to associate with imported transactions")]
    string account,
    IServiceProvider serviceProvider)
{
    var httpClient = serviceProvider.GetRequiredService<HttpClient>();
    var apiKey = Environment.GetEnvironmentVariable("BUDGET_TRACKER_API_KEY");

    if (string.IsNullOrEmpty(apiKey))
    {
        return "❌ API key not configured. Please set BUDGET_TRACKER_API_KEY environment variable.";
    }

    try
    {
        // Decode Base64 CSV content
        byte[] csvBytes;
        try
        {
            csvBytes = Convert.FromBase64String(csvContent);
        }
        catch (FormatException)
        {
            return "❌ Invalid Base64 CSV content. Please ensure the CSV content is properly Base64 encoded.";
        }

        // Create multipart form data
        using var content = new MultipartFormDataContent();
        using var csvStreamContent = new ByteArrayContent(csvBytes);
        csvStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(csvStreamContent, "file", fileName);
        content.Add(new StringContent(account), "account");

        // Add API key to request headers
        httpClient.DefaultRequestHeaders.Remove("X-API-Key");
        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        // Call the Budget Tracker API import endpoint
        var apiBaseUrl = GetApiBaseUrl(serviceProvider);
        var response = await httpClient.PostAsync($"{apiBaseUrl}/api/transactions/import", content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            return FormatImportResult(result, fileName, account);
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            return $"Import failed: {error}";
        }
    }
    catch (Exception ex)
    {
        return $"Error importing CSV: {ex.Message}";
    }
}

private static string FormatImportResult(string apiResponse, string fileName, string account)
{
    try
    {
        using var doc = JsonDocument.Parse(apiResponse);
        var root = doc.RootElement;

        var importedCount = root.TryGetProperty("importedCount", out var imported) ? imported.GetInt32() : 0;
        var failedCount = root.TryGetProperty("failedCount", out var failed) ? failed.GetInt32() : 0;

        var result = new StringBuilder();
        result.AppendLine("CSV Import Results:");
        result.AppendLine($"✅ Imported: {importedCount} transactions");

        if (failedCount > 0)
        {
            result.AppendLine($"❌ Failed: {failedCount} transactions");
        }

        result.AppendLine($"📁 File: {fileName}");
        result.AppendLine($"🏦 Account: {account}");
        result.AppendLine();
        result.AppendLine("Raw API Response:");
        result.AppendLine(apiResponse);

        return result.ToString();
    }
    catch
    {
        // If parsing fails, just return the raw response
        return $"CSV Import completed.\n📁 File: {fileName}\n🏦 Account: {account}\n\nRaw API Response:\n{apiResponse}";
    }
}
```

Add the required imports at the top of `Program.cs`:

```csharp
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
```

## Step 61.4: IDE Integration - Cursor AI Example

*Configure Cursor IDE to use your MCP server for natural language interaction with your Budget Tracker.*

Create or update `.cursor/mcp.json` in your project root:

```json
{
  "mcpServers": {
    "budget-tracker": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Development",
        "BUDGET_TRACKER_API_KEY": "bt_mcp_key_2025_secure_static_api_key_for_ai_integration"
      }
    }
  }
}
```

**Important**: Make sure your Budget Tracker API is running on `http://localhost:5295` before testing the MCP server integration.

## Step 61.5: Test with AI Assistant

*Open your IDE and explore the MCP server integration with natural language prompts.*

1. **Start your Budget Tracker API**:
   ```bash
   cd src/BudgetTracker.Api
   dotnet run
   ```

2. **Open Cursor IDE** and ensure the `.cursor/mcp.json` configuration is in place.

3. **Test the integration** by asking Claude in Cursor:

   > "What transactions do I have in my budget tracker? Show me my recent spending patterns."

The MCP server will automatically start when Claude needs to access your Budget Tracker data, and you'll see real-time responses about your financial information.

**Additional prompts to try**:
- "What did I spend on groceries this month?"
- "Show me my largest expenses from last week"
- "Help me import a CSV file with my bank transactions"

## Step 61.6: Create Documentation

*Add comprehensive documentation for the MCP server.*

Create `src/BudgetTracker.McpServer/README.md`:

```markdown
# Budget Tracker MCP Server

A Model Context Protocol (MCP) server that provides AI assistants with access to Budget Tracker functionality.

## Features

- **QueryTransactions**: Ask natural language questions about transactions
- **ImportTransactionsCsv**: Import CSV files with Base64 encoding

## IDE Integration - Cursor AI Example

Configure `.cursor/mcp.json`:
```json
{
  "mcpServers": {
    "budget-tracker": {
      "command": "dotnet",
      "args": ["run", "--project", "src/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj"],
      "env": {
        "BUDGET_TRACKER_API_KEY": "bt_mcp_key_2025_secure_static_api_key_for_ai_integration",
        "BudgetTracker__ApiBaseUrl": "http://localhost:5295"
      }
    }
  }
}
```

## Troubleshooting 🔧

**Authorization Issues:**
- Verify endpoint URL
- Verify the API key
- Check if the API Key UserId is valid
- Ensure the API is running

---

## Summary ✅

You've successfully built an MCP server for AI integration with your Budget Tracker:

✅ **MCP Server Structure**: Standalone console application using Microsoft MCP SDK
✅ **Authentication**: Static API key authentication for secure tool access
✅ **Natural Language Queries**: AI assistants can query financial data conversationally
✅ **CSV Import**: Base64-encoded file import through MCP protocol
✅ **Cursor Integration**: Ready for use with Claude through Cursor IDE
✅ **Documentation**: Complete setup and usage documentation

**Key Features Implemented**:
- **Protocol Compliance**: Full MCP specification compliance with stdio transport
- **Secure Access**: API key authentication with user isolation
- **File Import**: CSV import capability through Base64 encoding
- **Natural Interface**: Direct conversation with your financial data

**Technical Achievements**:
- **Microsoft MCP SDK**: Used official SDK for proper protocol implementation
- **Hosted Service**: Proper .NET hosting with dependency injection and configuration
- **HTTP Client Integration**: Authenticated API communication with your Budget Tracker
- **Multi-Format Support**: CSV import supports various bank formats through existing API

**What Users Get**:
- **AI Financial Assistant**: Natural language access to financial data through Cursor IDE
- **Simplified Import**: AI-guided CSV file import process
- **Real-time Queries**: Instant answers about spending patterns and transactions
- **Secure Integration**: Protected by API key authentication

The MCP server transforms your Budget Tracker into a powerful context provider for AI assistants, enabling natural language interaction with financial data! 🤖💰