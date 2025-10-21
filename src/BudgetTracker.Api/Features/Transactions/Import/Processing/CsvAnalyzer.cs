using System.Text;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class CsvAnalyzer : ICsvAnalyzer
{
    private readonly IAzureChatService _chatService;

    public CsvAnalyzer(IAzureChatService chatService)
    {
        _chatService = chatService;
    }

    public async Task<string> AnalyzeCsvStructureAsync(string csvContent)
    {
        var systemPrompt = "You are a CSV structure analysis expert. Analyze CSV files and identify their format, columns, and cultural settings.";
        var userPrompt = CreateStructureAnalysisPrompt(csvContent);

        return await _chatService.CompleteChatAsync(systemPrompt, userPrompt);
    }

    private string CreateStructureAnalysisPrompt(string csvContent)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Analyze this CSV file structure and identify the following elements:");
        prompt.AppendLine("1. Column separator (comma, semicolon, tab, pipe)");
        prompt.AppendLine("2. Culture/locale for number and date parsing (e.g., 'en-US', 'pt-PT', 'de-DE', 'fr-FR')");
        prompt.AppendLine("3. Date column name and format pattern");
        prompt.AppendLine("4. Description/memo column name");
        prompt.AppendLine("5. Amount/value column name");
        prompt.AppendLine("6. Confidence score (0-100)");
        prompt.AppendLine();
        prompt.AppendLine("CSV Data:");
        prompt.AppendLine(csvContent);
        prompt.AppendLine();
        prompt.AppendLine("Respond with a JSON object with this exact structure:");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"columnSeparator\": \",\" | \";\" | \"\\t\" | \"|\",");
        prompt.AppendLine("  \"cultureCode\": \"en-US\" | \"pt-PT\" | \"de-DE\" | \"fr-FR\" | \"es-ES\" | \"it-IT\" | etc,");
        prompt.AppendLine("  \"dateColumn\": \"column_name\",");
        prompt.AppendLine("  \"dateFormat\": \"MM/dd/yyyy\" | \"dd/MM/yyyy\" | \"yyyy-MM-dd\" | etc,");
        prompt.AppendLine("  \"descriptionColumn\": \"column_name\",");
        prompt.AppendLine("  \"amountColumn\": \"column_name\",");
        prompt.AppendLine("  \"confidenceScore\": 85");
        prompt.AppendLine("}");

        return prompt.ToString();
    }
}