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