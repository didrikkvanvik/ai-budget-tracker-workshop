# Workshop Step 022: Smart CSV Structure Detection

## Mission 🎯

In this step, you'll implement intelligent CSV structure detection using AI to handle a wide variety of bank CSV formats, even those you've never seen before. The system will automatically detect column separators, date formats, and column mappings for international CSV files, falling back to AI analysis when rule-based detection fails.

**Your goal**: Build a robust CSV detection system that can handle any bank CSV format by combining rule-based detection with LLM-powered analysis for unknown structures.

**Learning Objectives**:
- Implementing a layered detection approach with rule-based and AI fallbacks
- Using LLMs to analyze and understand unknown CSV structures
- Building culture-aware parsing for international CSV formats
- Creating confidence scoring systems for structure detection accuracy
- Integrating detection results with existing CSV parsing workflows

---

## Prerequisites

Before starting, ensure you completed:
- [021-image-import.md](021-image-import.md) - Multi-modal image import functionality
- [015-ai-transaction-categorization.md](015-ai-transaction-categorization.md) - AI categorization system
- [012-ai-transaction-enhancement-backend.md](012-ai-transaction-enhancement-backend.md) - Azure AI setup

---

## Branches

**Starting branch:** `021-image-import`
**Solution branch:** `022-smart-csv`

---

## Step 22.1: Create CSV Structure Detection Result Types

*Define the data structures for storing CSV detection results and confidence scores.*

The detection system needs a comprehensive way to represent the results of CSV analysis, including column mappings, culture settings, and confidence scoring. This will support both simple rule-based detection and complex AI-driven analysis.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/CsvStructureDetectionResult.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public class CsvStructureDetectionResult
{
    public string Delimiter { get; set; } = ",";
    public Dictionary<string, string> ColumnMappings { get; set; } = new();
    public string CultureCode { get; set; } = "en-US";
    public double ConfidenceScore { get; set; }
    public DetectionMethod DetectionMethod { get; set; }
}

public enum DetectionMethod
{
    RuleBased,
    AI
}
```

## Step 22.2: Create Column Mapping Dictionary

*Define standard column name patterns for rule-based detection.*

Before falling back to AI analysis, the system will attempt to match common English column names using predefined patterns. This provides fast detection for standard formats while preserving AI resources for complex cases.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/ColumnMappingDictionary.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public static class ColumnMappingDictionary
{
    // Simple English-only column mappings - if it doesn't match, use AI
    public static readonly string[] DateColumns =
        ["Date", "Transaction Date", "Posting Date", "Value Date", "Txn Date"];

    public static readonly string[] DescriptionColumns =
        ["Description", "Memo", "Details", "Transaction Description", "Reference"];

    public static readonly string[] AmountColumns =
        ["Amount", "Transaction Amount", "Debit", "Credit", "Value"];
}
```

## Step 22.3: Create CSV Structure Detection Interface

*Define the interface for CSV structure detection services.*

The detection system needs a clean interface that can be implemented by different detection strategies. This allows for easy testing and future extension with additional detection methods.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/ICsvStructureDetector.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public interface ICsvStructureDetector
{
    Task<CsvStructureDetectionResult> DetectStructureAsync(Stream csvStream);
}
```

## Step 22.4: Create AI-Powered CSV Analyzer

*Implement the AI service that analyzes CSV structures using Azure OpenAI.*

The `CsvAnalyzer` is the core AI component that sends CSV samples to the language model for intelligent structure analysis. It creates detailed prompts that guide the AI to identify column mappings, delimiters, and cultural formatting.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Processing/ICsvAnalyzer.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public interface ICsvAnalyzer
{
    Task<string> AnalyzeCsvStructureAsync(string csvContent);
}
```

Create `src/BudgetTracker.Api/Features/Transactions/Import/Processing/CsvAnalyzer.cs`:

```csharp
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
```

## Step 22.5: Create AI Detection Service

*Implement the service that processes AI responses and converts them to detection results.*

The `CsvDetector` service acts as a bridge between the raw AI analysis and the structured detection results. It handles JSON parsing, error recovery, and confidence assessment of AI responses.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/ICsvDetector.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public interface ICsvDetector
{
    Task<CsvStructureDetectionResult> AnalyzeCsvStructureAsync(Stream csvStream);
}
```

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/CsvDetector.cs`:

```csharp
using System.Text;
using System.Text.Json;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Infrastructure.Extensions;

namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public class CsvDetector : ICsvDetector
{
    private readonly ICsvAnalyzer _structureAnalysisService;
    private readonly ILogger<CsvDetector> _logger;

    public CsvDetector(
        ICsvAnalyzer structureAnalysisService,
        ILogger<CsvDetector> logger)
    {
        _structureAnalysisService = structureAnalysisService;
        _logger = logger;
    }

    public async Task<CsvStructureDetectionResult> AnalyzeCsvStructureAsync(Stream csvStream)
    {
        try
        {
            _logger.LogDebug("Starting AI CSV structure analysis");

            // Read CSV headers and sample rows
            csvStream.Position = 0;
            using var reader = new StreamReader(csvStream, Encoding.UTF8, leaveOpen: true);

            var lines = new List<string>();
            for (int i = 0; i < 5 && !reader.EndOfStream; i++) // Read first 5 lines
            {
                var line = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            if (lines.Count == 0)
            {
                _logger.LogWarning("No data found in CSV for AI analysis");
                return new CsvStructureDetectionResult { ConfidenceScore = 0, DetectionMethod = DetectionMethod.AI };
            }

            _logger.LogDebug("Sending CSV structure analysis request to AI service");

            // Use dedicated CSV structure analysis service
            var csvContent = string.Join("\n", lines);
            var responseText = await _structureAnalysisService.AnalyzeCsvStructureAsync(csvContent);

            if (string.IsNullOrEmpty(responseText))
            {
                _logger.LogWarning("AI service returned empty response for CSV structure analysis");
                return new CsvStructureDetectionResult { ConfidenceScore = 0, DetectionMethod = DetectionMethod.AI };
            }

            // Parse AI response
            var result = ParseAiResponse(responseText.ExtractJsonFromCodeBlock());
            result.DetectionMethod = DetectionMethod.AI;

            _logger.LogDebug("AI detection completed - confidence: {Confidence}%, method: AI", result.ConfidenceScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI CSV structure analysis failed");
            return new CsvStructureDetectionResult { ConfidenceScore = 0, DetectionMethod = DetectionMethod.AI };
        }
    }

    private CsvStructureDetectionResult ParseAiResponse(string aiResponse)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                var result = new CsvStructureDetectionResult
                {
                    DetectionMethod = DetectionMethod.AI
                };

                // Extract column separator
                if (root.TryGetProperty("columnSeparator", out var columnSep))
                {
                    result.Delimiter = columnSep.GetString() ?? ",";
                    if (result.Delimiter == "\\t") result.Delimiter = "\t"; // Handle tab character
                }

                // Extract culture code for parsing
                if (root.TryGetProperty("cultureCode", out var cultureCode))
                {
                    result.CultureCode = cultureCode.GetString() ?? "en-US";
                }

                // Extract column mappings
                result.ColumnMappings = new Dictionary<string, string>();

                if (root.TryGetProperty("dateColumn", out var dateCol) && !string.IsNullOrEmpty(dateCol.GetString()))
                {
                    result.ColumnMappings["Date"] = dateCol.GetString()!;
                }

                if (root.TryGetProperty("descriptionColumn", out var descCol) && !string.IsNullOrEmpty(descCol.GetString()))
                {
                    result.ColumnMappings["Description"] = descCol.GetString()!;
                }

                if (root.TryGetProperty("amountColumn", out var amountCol) && !string.IsNullOrEmpty(amountCol.GetString()))
                {
                    result.ColumnMappings["Amount"] = amountCol.GetString()!;
                }

                // Extract confidence score
                if (root.TryGetProperty("confidenceScore", out var confidence))
                {
                    result.ConfidenceScore = confidence.GetDouble();
                }

                _logger.LogDebug("AI detection successful - separator: '{Delimiter}', culture: '{Culture}', confidence: {Confidence}%, method: AI",
                    result.Delimiter, result.CultureCode, result.ConfidenceScore);

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response JSON: {Response}", aiResponse);
        }

        // Fallback: return low confidence result
        _logger.LogWarning("Could not parse AI response, returning low confidence result");
        return new CsvStructureDetectionResult
        {
            ConfidenceScore = 0,
            DetectionMethod = DetectionMethod.AI
        };
    }
}
```

## Step 22.6: Implement Smart CSV Structure Detector

*Create the main detection service that combines rule-based and AI approaches.*

The `CsvStructureDetector` is the orchestrator that tries simple rule-based detection first, then falls back to AI analysis for complex or unknown formats. This provides optimal performance while ensuring comprehensive format support.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/CsvStructureDetector.cs`:

```csharp
using System.Globalization;

namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public class CsvStructureDetector : ICsvStructureDetector
{
    private readonly ICsvDetector _aiDetectionService;
    private readonly ILogger<CsvStructureDetector> _logger;

    public CsvStructureDetector(
        ICsvDetector aiDetectionService,
        ILogger<CsvStructureDetector> logger)
    {
        _aiDetectionService = aiDetectionService;
        _logger = logger;
    }

    public async Task<CsvStructureDetectionResult> DetectStructureAsync(Stream csvStream)
    {
        try
        {
            _logger.LogDebug("Starting CSV structure detection");

            // Try simple parsing first
            var simpleResult = TrySimpleParsing(csvStream);

            if (simpleResult.ConfidenceScore >= 85)
            {
                _logger.LogDebug("Simple parsing successful with {Confidence}% confidence",
                    simpleResult.ConfidenceScore);
                return simpleResult;
            }

            _logger.LogDebug("Simple parsing failed, falling back to AI detection");
            csvStream.Position = 0; // Reset stream for AI analysis
            return await _aiDetectionService.AnalyzeCsvStructureAsync(csvStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CSV structure detection");

            _logger.LogDebug("Attempting AI fallback after error");
            csvStream.Position = 0;
            return await _aiDetectionService.AnalyzeCsvStructureAsync(csvStream);
        }
    }

    private CsvStructureDetectionResult TrySimpleParsing(Stream csvStream)
    {
        csvStream.Position = 0;
        using var reader = new StreamReader(csvStream, leaveOpen: true);

        var lines = new List<string>();
        for (var i = 0; i < 100 && !reader.EndOfStream; i++)
        {
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        if (lines.Count < 1)
        {
            return new CsvStructureDetectionResult { ConfidenceScore = 0 };
        }

        var result = new CsvStructureDetectionResult
        {
            DetectionMethod = DetectionMethod.RuleBased,
            Delimiter = ",",
            CultureCode = "en-US" // Default to US format for simple parsing
        };

        // Try to find English column names in the header
        var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();

        var dateColumn = FindColumn(headers, ColumnMappingDictionary.DateColumns);
        var descriptionColumn = FindColumn(headers, ColumnMappingDictionary.DescriptionColumns);
        var amountColumn = FindColumn(headers, ColumnMappingDictionary.AmountColumns);

        // Check if we found the required columns
        if (dateColumn == null || descriptionColumn == null || amountColumn == null)
        {
            result.ConfidenceScore = 0; // No required columns found
            return result;
        }

        // Set up column mappings
        result.ColumnMappings["Date"] = dateColumn;
        result.ColumnMappings["Description"] = descriptionColumn;
        result.ColumnMappings["Amount"] = amountColumn;

        // Optional columns (simple patterns for English-only detection)
        var balanceColumn = FindColumn(headers, ["Balance", "Running Balance", "Account Balance"]);
        if (balanceColumn != null)
        {
            result.ColumnMappings["Balance"] = balanceColumn;
        }

        var categoryColumn = FindColumn(headers, ["Category", "Type", "Transaction Type"]);
        if (categoryColumn != null)
        {
            result.ColumnMappings["Category"] = categoryColumn;
        }

        // Try to parse a few sample rows to validate the format
        var sampleRows = lines.Skip(1).Take(3);
        var successfulParses = 0;
        var totalSamples = 0;

        foreach (var row in sampleRows)
        {
            totalSamples++;
            var parts = row.Split(',');
            if (parts.Length >= headers.Length && TryParseRow(parts, headers, result.ColumnMappings))
            {
                successfulParses++;
            }
        }

        // Calculate confidence based on successful parsing
        if (totalSamples > 0)
        {
            var successRate = (double)successfulParses / totalSamples;
            result.ConfidenceScore = successRate * 100;
        }
        else
        {
            result.ConfidenceScore = 85; // Found columns but no data to validate
        }

        return result;
    }

    private string? FindColumn(string[] headers, string[] patterns)
    {
        return headers.FirstOrDefault(header =>
            patterns.Any(pattern =>
                string.Equals(pattern, header.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private bool TryParseRow(string[] parts, string[] headers, Dictionary<string, string> mappings)
    {
        try
        {
            // Try to parse date
            if (mappings.TryGetValue("Date", out var dateColumn))
            {
                var dateIndex = Array.IndexOf(headers, dateColumn);
                if (dateIndex >= 0 && dateIndex < parts.Length)
                {
                    var dateStr = parts[dateIndex].Trim().Trim('"');
                    if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        return false;
                    }
                }
            }

            // Try to parse amount
            if (mappings.TryGetValue("Amount", out var amountColumn))
            {
                var amountIndex = Array.IndexOf(headers, amountColumn);
                if (amountIndex >= 0 && amountIndex < parts.Length)
                {
                    var amountStr = parts[amountIndex].Trim().Trim('"').Replace("$", "").Replace(",", "");
                    if (!decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

## Step 22.7: Update CSV Importer for Smart Detection

*Enhance the CSV importer to use detection results for flexible parsing.*

The existing `CsvImporter` needs to be updated to accept and use detection results, allowing it to parse CSV files with different delimiters, cultures, and column mappings determined by the detection system.

Update `src/BudgetTracker.Api/Features/Transactions/Import/Processing/CsvImporter.cs`:

```csharp
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.Import.Detection;
using BudgetTracker.Api.Features.Transactions.List;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class CsvImporter
{
    public async Task<(ImportResult Result, List<Transaction> Transactions)> ParseCsvAsync(Stream csvStream,
        string sourceFileName, string userId, string account)
    {
        return await ParseCsvAsync(csvStream, sourceFileName, userId, account, null);
    }

    public async Task<(ImportResult Result, List<Transaction> Transactions)> ParseCsvAsync(Stream csvStream,
        string sourceFileName, string userId, string account, CsvStructureDetectionResult? detectionResult)
    {
        var result = new ImportResult
        {
            SourceFile = sourceFileName,
            ImportedAt = DateTime.UtcNow
        };

        var transactions = new List<Transaction>();

        try
        {
            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
                Delimiter = detectionResult?.Delimiter ?? ","
            });

            var rowNumber = 0;

            await foreach (var record in csv.GetRecordsAsync<dynamic>())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    var transaction = ParseTransactionRow(record, detectionResult);
                    if (transaction != null)
                    {
                        transaction.UserId = userId;
                        transaction.Account = account;

                        transactions.Add(transaction);
                        result.ImportedCount++;
                    }
                    else
                    {
                        result.FailedCount++;
                        result.Errors.Add($"Row {rowNumber}: Failed to parse transaction");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                }
            }

            result.ImportedCount = transactions.Count;
            result.FailedCount = result.TotalRows - result.ImportedCount;

            return (result, transactions);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"CSV parsing error: {ex.Message}");
            return (result, new List<Transaction>());
        }
    }

    private Transaction? ParseTransactionRow(dynamic record, CsvStructureDetectionResult? detectionResult = null)
    {
        try
        {
            var recordDict = (IDictionary<string, object>)record;

            // Use detected column mappings if available, otherwise fall back to English defaults
            var description = GetColumnValueWithDetection(recordDict, detectionResult, "Description",
                "Description", "Memo", "Details");

            var dateStr = GetColumnValueWithDetection(recordDict, detectionResult, "Date",
                "Date", "Transaction Date", "Posting Date");

            var amountStr = GetColumnValueWithDetection(recordDict, detectionResult, "Amount",
                "Amount", "Transaction Amount", "Debit", "Credit");

            var balanceStr = GetColumnValueWithDetection(recordDict, detectionResult, "Balance",
                "Balance", "Running Balance", "Account Balance");

            var category = GetColumnValueWithDetection(recordDict, detectionResult, "Category",
                "Category", "Type", "Transaction Type");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description is required");
            }

            if (string.IsNullOrWhiteSpace(dateStr))
            {
                throw new ArgumentException("Date is required");
            }

            if (string.IsNullOrWhiteSpace(amountStr))
            {
                throw new ArgumentException("Amount is required");
            }

            // Parse date
            if (!TryParseDate(dateStr, out var date, null, detectionResult))
            {
                throw new ArgumentException($"Invalid date format: {dateStr}");
            }

            // Parse amount using culture-aware parsing
            if (!TryParseAmountWithCulture(amountStr, detectionResult, out var amount))
            {
                throw new ArgumentException($"Invalid amount format: {amountStr}");
            }

            // Parse balance (optional)
            decimal? balance = null;
            if (!string.IsNullOrWhiteSpace(balanceStr))
            {
                if (TryParseAmountWithCulture(balanceStr, detectionResult, out var parsedBalance))
                {
                    balance = parsedBalance;
                }
            }

            return new Transaction
            {
                Id = Guid.NewGuid(),
                Date = date,
                Description = description.Trim(),
                Amount = amount,
                Balance = balance,
                Category = !string.IsNullOrWhiteSpace(category?.Trim()) ? category.Trim() : "Uncategorized",
                ImportedAt = DateTime.UtcNow,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? GetColumnValueWithDetection(IDictionary<string, object> record,
        CsvStructureDetectionResult? detectionResult, string mappingKey, params string[] fallbackColumnNames)
    {
        // First, try the detected column mapping if available
        if (detectionResult?.ColumnMappings != null &&
            detectionResult.ColumnMappings.TryGetValue(mappingKey, out var detectedColumnName) &&
            !string.IsNullOrEmpty(detectedColumnName))
        {
            if (record.TryGetValue(detectedColumnName, out var detectedValue) && detectedValue != null)
            {
                return detectedValue.ToString()?.Trim();
            }
        }

        // Fall back to trying the provided column name variations
        return GetColumnValue(record, fallbackColumnNames);
    }

    private static string? GetColumnValue(IDictionary<string, object> record, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (record.TryGetValue(columnName, out var value) && value != null)
            {
                return value.ToString()?.Trim();
            }
        }

        return null;
    }

    private bool TryParseDate(string dateStr, out DateTime date, string? detectedFormat = null, CsvStructureDetectionResult? detectionResult = null)
    {
        date = default;

        // Get culture for date parsing
        CultureInfo culture = CultureInfo.InvariantCulture;
        if (!string.IsNullOrEmpty(detectionResult?.CultureCode))
        {
            try
            {
                culture = new CultureInfo(detectionResult.CultureCode);
            }
            catch
            {
                culture = CultureInfo.InvariantCulture;
            }
        }

        // Try detected format first if available
        if (!string.IsNullOrEmpty(detectedFormat))
        {
            if (DateTime.TryParseExact(dateStr.Trim(), detectedFormat, culture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
            {
                return true;
            }
        }

        // Try culture-aware parsing
        if (DateTime.TryParse(dateStr.Trim(), culture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
        {
            return true;
        }

        // Final fallback to invariant culture
        return DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
    }

    private static bool TryParseAmountWithCulture(string amountStr, CsvStructureDetectionResult? detectionResult, out decimal amount)
    {
        amount = 0;

        if (string.IsNullOrWhiteSpace(amountStr))
            return false;

        var cleanAmount = amountStr.Trim();

        // Remove common currency symbols
        cleanAmount = cleanAmount.Replace("$", "").Replace("€", "").Replace("£", "").Replace("¥", "").Replace("R$", "").Trim();

        // Try to get culture from detection result
        CultureInfo culture = CultureInfo.InvariantCulture;
        if (!string.IsNullOrEmpty(detectionResult?.CultureCode))
        {
            try
            {
                culture = new CultureInfo(detectionResult.CultureCode);
            }
            catch
            {
                // Fall back to invariant culture if culture code is invalid
                culture = CultureInfo.InvariantCulture;
            }
        }

        // Use culture-specific parsing - .NET handles decimal/thousand separators automatically
        return decimal.TryParse(cleanAmount, NumberStyles.Currency, culture, out amount);
    }

}
```

## Step 22.8: Update Import API for Smart Detection

*Integrate the detection system into the import API workflow.*

The import API needs to use the new detection system before processing CSV files. This ensures that all CSV files go through intelligent structure analysis before parsing attempts.

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportApi.cs`:

```csharp
// Update the ImportAsync method signature to include ICsvStructureDetector
private static async Task<Results<Ok<ImportResult>, BadRequest<string>>> ImportAsync(
    IFormFile file, [FromForm] string account,
    CsvImporter csvImporter, IImageImporter imageImporter, BudgetTrackerContext context,
    ITransactionEnhancer enhancementService, ClaimsPrincipal claimsPrincipal,
    ICsvStructureDetector detectionService, IServiceProvider serviceProvider
)
{
    var validationResult = ValidateFileInput(file);
    if (validationResult != null)
    {
        return validationResult;
    }

    try
    {
        var userId = claimsPrincipal.GetUserId();
        await using var stream = file.OpenReadStream();

        var (importResult, transactions, detectionResult) = await ProcessFileAsync(
            stream, file.FileName, userId, account, csvImporter, imageImporter, detectionService);

        var importSessionHash = GenerateImportSessionHash(file.FileName, account);
        AssignImportSessionToTransactions(transactions, importSessionHash);

        await SaveTransactionsAsync(context, transactions);

        var enhancementResults = await ProcessEnhancementsAsync(
            enhancementService, transactions, account, userId, importSessionHash);

        var result = CreateImportResult(importResult, importSessionHash, enhancementResults, detectionResult);

        return TypedResults.Ok(result);
    }
    catch (Exception ex)
    {
        return TypedResults.BadRequest(ex.Message);
    }
}

// Add the ProcessCsvFileAsync method with detection
private static async Task<(ImportResult, List<Transaction>, CsvStructureDetectionResult?)> ProcessCsvFileAsync(
    Stream stream, string fileName, string userId, string account,
    CsvImporter csvImporter, ICsvStructureDetector detectionService)
{
    var detectionResult = await detectionService.DetectStructureAsync(stream);

    if (detectionResult.ConfidenceScore < 85)
    {
        var errorMessage = detectionResult.DetectionMethod == DetectionMethod.AI
            ? "Unable to automatically detect CSV structure using AI analysis. The file format may be too complex or non-standard. Please ensure your CSV contains Date, Description, and Amount columns with recognizable headers."
            : "Unable to automatically detect CSV structure using pattern matching. Please ensure your CSV file follows a standard banking format with clear column headers (Date, Description, Amount).";

        throw new InvalidOperationException(errorMessage);
    }

    stream.Position = 0; // Reset stream position
    var (importResult, transactions) = await csvImporter.ParseCsvAsync(stream, fileName, userId, account, detectionResult);

    return (importResult, transactions, detectionResult);
}

// Update CreateImportResult to include detection information
private static ImportResult CreateImportResult(ImportResult baseResult, string sessionHash,
    List<TransactionEnhancementResult> enhancementResults, CsvStructureDetectionResult? detectionResult)
{
    return new ImportResult
    {
        SourceFile = baseResult.SourceFile,
        ImportedAt = baseResult.ImportedAt,
        ImportedCount = baseResult.ImportedCount,
        FailedCount = baseResult.FailedCount,
        TotalRows = baseResult.TotalRows,
        Errors = baseResult.Errors,
        ImportSessionHash = sessionHash,
        Enhancements = enhancementResults,
        DetectionMethod = detectionResult?.DetectionMethod.ToString(),
        DetectionConfidence = detectionResult?.ConfidenceScore ?? 0
    };
}
```

## Step 22.9: Update Import Result Types

*Add detection information to the import result types.*

The import results need to include information about how the CSV structure was detected, providing transparency to users about the analysis method and confidence levels.

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportResult.cs`:

```csharp
public class ImportResult
{
    public string SourceFile { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalRows { get; set; }
    public List<string> Errors { get; set; } = new();
    public string ImportSessionHash { get; set; } = string.Empty;
    public List<TransactionEnhancementResult> Enhancements { get; set; } = new();
    public string? DetectionMethod { get; set; } // "RuleBased" or "AI"
    public double DetectionConfidence { get; set; } // 0-100
}
```

## Step 22.10: Register Detection Services

*Add all detection services to the dependency injection container.*

The detection services need to be registered with the DI container in the correct order to ensure proper dependency resolution and service lifetime management.

Update `src/BudgetTracker.Api/Program.cs`:

```csharp
// Add CSV detection services
builder.Services.AddScoped<ICsvStructureDetector, CsvStructureDetector>();
builder.Services.AddScoped<ICsvDetector, CsvDetector>();
builder.Services.AddScoped<ICsvAnalyzer, CsvAnalyzer>();
builder.Services.AddScoped<IImageImporter, ImageImporter>();
```

## Step 22.11: Update Frontend Types

*Add detection information to the frontend TypeScript types.*

The frontend needs to understand the new detection information to provide appropriate feedback to users about how their CSV files were processed.

Update `src/BudgetTracker.Web/src/features/transactions/types.ts`:

```tsx
export interface ImportResult {
  importedCount: number;
  failedCount: number;
  errors: string[];
  importSessionHash: string;
  enhancements: TransactionEnhancement[];
  detectionMethod?: string; // "RuleBased" | "AI"
  detectionConfidence?: number; // 0-100
}
```

## Step 22.12: Add UI Integration for Detection Results

*Update the import progress indicators to show CSV detection information.*

The frontend should display detection method and confidence information to provide transparency about how CSV files were processed.

### 22.12.1: Update Import Progress Component

Update the `FileUpload.tsx` component to show detection information in the success message:

```tsx
// In src/BudgetTracker.Web/src/components/FileUpload.tsx
// Update the success message section around line 120

{result && result.importedCount > 0 && (
  <div className="mt-4 p-4 bg-green-50 border border-green-200 rounded-md">
    <div className="flex items-center">
      <svg className="h-5 w-5 text-green-400 mr-2" fill="currentColor" viewBox="0 0 20 20">
        <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
      </svg>
      <div className="flex-1">
        <span className="text-sm font-medium text-green-800">
          Import completed successfully!
        </span>
        <div className="text-xs text-green-700 mt-1">
          {result.importedCount} transactions imported
          {result.failedCount > 0 && `, ${result.failedCount} failed`}
        </div>

        {/* Add detection information display */}
        {result.detectionMethod && (
          <div className="mt-2 p-2 bg-green-100 rounded border">
            <div className="flex items-center justify-between">
              <span className="text-xs font-medium text-green-800">
                CSV Analysis Method: {result.detectionMethod}
              </span>
              {result.detectionConfidence !== undefined && (
                <span className="text-xs text-green-700 bg-green-200 px-2 py-1 rounded">
                  {Math.round(result.detectionConfidence)}% confidence
                </span>
              )}
            </div>
            <div className="text-xs text-green-600 mt-1">
              {result.detectionMethod === 'RuleBased'
                ? 'Standard format detected using pattern matching'
                : 'Complex format analyzed using AI detection'
              }
            </div>
          </div>
        )}
      </div>
    </div>
  </div>
)}
```

### 22.12.2: Add Detection Information to Import Status

For better user experience, also show detection progress during the import process:

```tsx
// In the progress indicator section of FileUpload.tsx
{progress > 0 && progress < 100 && (
  <div className="mt-4">
    <div className="flex justify-between text-sm text-gray-600 mb-1">
      <span>
        {progress < 30 ? 'Analyzing file structure...' :
         progress < 60 ? 'Processing transactions...' :
         'Finalizing import...'}
      </span>
      <span>{Math.round(progress)}%</span>
    </div>
    <div className="w-full bg-gray-200 rounded-full h-2">
      <div
        className="bg-blue-600 h-2 rounded-full transition-all duration-300"
        style={{ width: `${progress}%` }}
      />
    </div>

    {/* Add detection status indicator */}
    {progress < 30 && (
      <div className="mt-2 text-xs text-gray-500 flex items-center">
        <svg className="animate-spin -ml-1 mr-2 h-3 w-3 text-gray-400" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
          <path className="opacity-75" fill="currentColor" d="m4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"/>
        </svg>
        Detecting CSV structure and column mappings...
      </div>
    )}
  </div>
)}

## Step 22.13: Test Smart CSV Detection

*Test the complete detection system with various CSV formats.*

### 22.13.1: Test Rule-Based Detection

Test with a standard English CSV format:

```csv
Date,Description,Amount,Balance
2025-01-15,STARBUCKS COFFEE #1234,-4.50,1245.50
2025-01-16,SALARY DEPOSIT,2500.00,3745.50
2025-01-17,GROCERY STORE,-89.32,3656.18
```

**Expected Results:**
- ✅ **Detection Method**: RuleBased
- ✅ **Confidence Score**: >85%
- ✅ **Column Mappings**: Date, Description, Amount correctly identified
- ✅ **Processing**: Fast detection without AI calls

### 22.13.2: Test AI Detection - Portuguese Format

Test with a Portuguese bank CSV format that requires AI analysis:

```csv
Data;Descrição;Valor;Saldo
15/01/2025;COMPRA CONTINENTE;-45,67;1.234,56
16/01/2025;TRANSFERÊNCIA RECEBIDA;2.500,00;3.734,56
17/01/2025;MULTIBANCO ATM;-50,00;3.684,56
```

**Expected Results:**
- ✅ **Detection Method**: AI
- ✅ **Confidence Score**: >85%
- ✅ **Column Mappings**: Data→Date, Descrição→Description, Valor→Amount
- ✅ **Culture**: pt-PT (Portuguese formatting)
- ✅ **Delimiter**: Semicolon (;)

### 22.13.3: Test AI Detection - German Format

Test with a German bank CSV format:

```csv
Datum|Verwendungszweck|Betrag|Kontostand
15.01.2025|KAUFLAND GMBH|-23,45|1.567,89
16.01.2025|GEHALTSZAHLUNG|3.200,00|4.767,89
17.01.2025|SPARKASSE GELDAUTOMAT|-100,00|4.667,89
```

**Expected Results:**
- ✅ **Detection Method**: AI
- ✅ **Culture**: de-DE (German formatting)
- ✅ **Delimiter**: Pipe (|)
- ✅ **Column Mappings**: Datum→Date, Verwendungszweck→Description, Betrag→Amount

### 22.13.4: Test Error Handling

Test with an unrecognizable format:

```csv
Col1,Col2,Col3,Col4
abc,def,ghi,jkl
123,456,789,012
```

**Expected Results:**
- ✅ **Low Confidence**: <85%
- ✅ **Error Message**: Clear explanation about unrecognizable format
- ✅ **Graceful Failure**: No crash, appropriate error handling

---

## Summary ✅

You've successfully implemented an intelligent CSV structure detection system:

✅ **Layered Detection Strategy**: Rule-based detection for common formats with AI fallback for complex cases
✅ **Multi-Cultural Support**: Automatic detection of culture-specific number and date formats
✅ **Intelligent Column Mapping**: AI-powered identification of column purposes in any language
✅ **Confidence Scoring**: Transparent confidence assessment for detection accuracy
✅ **Error Recovery**: Robust error handling with graceful fallbacks and clear user feedback
✅ **Performance Optimization**: Fast rule-based detection prevents unnecessary AI calls

**Key Features Implemented**:
- **Hybrid Detection**: Combines fast rule-based matching with comprehensive AI analysis
- **Cultural Intelligence**: Detects and handles international CSV formats with proper locale settings
- **Dynamic Column Mapping**: AI identifies column purposes regardless of language or naming conventions
- **Confidence Assessment**: Provides transparency about detection quality and reliability
- **Seamless Integration**: Works with existing CSV import pipeline without breaking changes
- **Error Resilience**: Handles edge cases and provides meaningful feedback for unparseable files

**Technical Achievements**:
- **Multi-Stage Detection**: Efficient pipeline that tries simple patterns before expensive AI analysis
- **AI Prompt Engineering**: Sophisticated prompts that guide LLMs to provide structured CSV analysis
- **Culture-Aware Parsing**: Proper handling of different decimal separators, date formats, and currencies
- **JSON Response Parsing**: Robust extraction of structured data from AI responses
- **Stream Management**: Proper handling of file streams with position resets for multiple analyses

**What Users Get**:
- **Universal CSV Support**: Can import from any bank regardless of format or language
- **Automatic Format Detection**: No need to configure import settings or specify file formats
- **International Compatibility**: Handles European, Asian, and other regional CSV formats automatically
- **Transparent Processing**: Clear feedback about how files were analyzed and processed
- **Reliable Imports**: High-confidence detection ensures accurate data extraction

The system now intelligently handles CSV files from banks worldwide, automatically detecting structure and applying appropriate parsing rules for accurate transaction import! 🎉