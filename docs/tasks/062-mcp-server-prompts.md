# Workshop Step 062: MCP Server Prompts

## Mission 🎯

In this step, you'll add prompt templates to your MCP server that help AI assistants guide users through Budget Tracker workflows. MCP prompts provide pre-configured conversation starters that make it easier for users to interact with your tools through natural language.

**Your goal**: Add a prompt template for CSV import workflow and test it using the MCP Inspector.

**Learning Objectives**:
- Understanding MCP prompts and their role in user experience
- Implementing prompt templates with the Microsoft MCP SDK
- Creating parameterized prompts for dynamic workflows
- Testing MCP servers with the official inspector tool

---

## Prerequisites

Before starting, ensure you completed:
- [061-mcp-server.md](061-mcp-server.md) - MCP Server for AI Integration
- Node.js installed for running the MCP Inspector

---

## Branches

**Starting branch:** `061-mcp-server`
**Solution branch:** `062-mcp-server-prompts`

---

## What Are MCP Prompts?

MCP prompts are reusable conversation templates that:
- **Guide users** through complex workflows with pre-written prompts
- **Reduce friction** by providing starting points for common tasks
- **Include parameters** to customize the prompt for specific contexts
- **Appear in IDE prompt pickers** for easy discovery

**Tools vs Prompts**:
- **Tools**: Functions the AI can call to perform actions (e.g., `QueryTransactions`)
- **Prompts**: Conversation templates that guide the AI on how to use tools (e.g., "Help me import CSV transactions")

---

## Step 62.1: Add Import Prompt

*Create a prompt template that guides users through CSV import workflow.*

Add the required import for Microsoft.Extensions.AI at the top of `src/BudgetTracker.McpServer/Program.cs`:

```csharp
using Microsoft.Extensions.AI;
```

Add a new static class for prompts at the bottom of `Program.cs`, before the `BudgetTrackerConfiguration` class:

```csharp
[McpServerPromptType]
public static class BudgetTrackerPrompts
{
    [McpServerPrompt(Name = "Import"), Description("Import a csv file of transactions into the budget tracker")]
    public static ChatMessage Import([Description("Account name to import.")] string account)
    {
        return new(ChatRole.User, $"Import this csv file of transactions for account: {account}");
    }
}
```

**Key Elements**:
- **`[McpServerPromptType]`**: Marks the class as containing MCP prompts
- **`static class`**: Prompts don't require instance state
- **`[McpServerPrompt]`**: Defines the prompt with a name visible in IDEs
- **`Description`**: Helps users understand when to use this prompt
- **`ChatMessage`**: Returns a user message that guides the AI's response
- **`ChatRole.User`**: Indicates this is a user prompt (not system/assistant)

---

## Step 62.2: Register Prompts

*Configure the MCP server to expose your prompts.*

Update the MCP server configuration in `Program.cs` to include prompts. Find this section:

```csharp
// Add MCP server with tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
```

Add prompt registration:

```csharp
// Add MCP server with tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly();
```

---

## Step 62.3: Test with MCP Inspector

*Use the official MCP Inspector to test your prompts and tools.*

The MCP Inspector is an interactive testing tool provided by the Model Context Protocol project.

### Install MCP Inspector

```bash
npx @modelcontextprotocol/inspector
```

### Configure Inspector

When the inspector starts, it will ask for your MCP server configuration. Enter:

**Command:**
```
dotnet
```

**Arguments:**
```json
["run", "--project", "src/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj"]
```

**Environment Variables:**
```json
{
  "DOTNET_ENVIRONMENT": "Development",
  "BUDGET_TRACKER_API_KEY": "bt_mcp_key_2025_secure_static_api_key_for_ai_integration",
  "BudgetTracker__ApiBaseUrl": "http://localhost:5295"
}
```

### Test the Import Prompt

1. **Start your Budget Tracker API** (in a separate terminal):
   ```bash
   cd src/BudgetTracker.Api
   dotnet run
   ```

2. **In the MCP Inspector UI**:
   - Connect 
   - Navigate to the **Prompts** tab
   - You should see "Import" listed with its description
   - Click on the Import prompt
   - Enter a value for the `account` parameter (e.g., "Chase Checking")
   - Click "Get Prompt"
   - You'll see the generated prompt message that would be sent to the AI

3. **Test the Tools Tab**:
   - Navigate to the **Tools** tab
   - You should see both `QueryTransactions` and `ImportTransactionsCsv`
   - Try calling `QueryTransactions` with a test question
   - Verify the response from your Budget Tracker API

---

## Summary ✅

You've successfully added prompt templates to your MCP server:

✅ **Prompt Implementation**: Created an Import prompt with parameterization
✅ **Static Class Pattern**: Used static class for stateless prompt definitions
✅ **MCP Registration**: Registered prompts with `.WithPromptsFromAssembly()`
✅ **Inspector Testing**: Tested prompts using the official MCP Inspector tool

**Key Concepts Learned**:
- **Prompt Templates**: Pre-written conversation starters for common workflows
- **Parameterization**: Dynamic prompts that adapt based on user input
- **ChatMessage API**: Using Microsoft.Extensions.AI for structured messages
- **MCP Inspector**: Interactive testing tool for MCP server development

**Technical Achievements**:
- **Microsoft.Extensions.AI Integration**: Using `ChatMessage` and `ChatRole`
- **Attribute-Based Discovery**: MCP SDK automatically discovers prompts via attributes
- **Type-Safe Parameters**: Strongly-typed parameters with descriptions
- **Development Workflow**: Tested both in inspector and real IDE

**User Experience Benefits**:
- **Discoverability**: Users can find import workflow through prompt picker
- **Consistency**: Standardized approach to CSV imports
- **Reduced Friction**: Pre-written prompt eliminates guesswork
- **Flexibility**: Account parameter allows customization

The MCP prompt system makes your Budget Tracker more accessible by providing guided conversation starters! 💬✨
