# Budget Tracker MCP Server

A Model Context Protocol (MCP) server that provides AI assistants with access to Budget Tracker functionality.

## Features

- **QueryTransactions**: Ask natural language questions about transactions
- **ImportTransactionsCsv**: Import CSV files with Base64 encoding

## Setup

1. **API Key**: Set environment variable:
   ```bash
   export BUDGET_TRACKER_API_KEY="your_api_key_here"
   ```

2. **Run the server**:
   ```bash
   dotnet run --project src/BudgetTracker.McpServer
   ```

## IDE Integration - Cursor AI Example

Configure `.cursor/mcp.json`:
```json
{
  "mcpServers": {
    "budget-tracker": {
      "command": "dotnet",
      "args": ["run", "--project", "src/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj"],
      "env": {
        "BUDGET_TRACKER_API_KEY": "bt_mcp_key_2025_secure_static_api_key_for_ai_integration"
      }
    }
  }
}
```

## Supported CSV Formats

- Generic bank format (Date, Description, Amount, Balance)
- Chase bank format
- Bank of America format
- Auto-detection handles various column layouts