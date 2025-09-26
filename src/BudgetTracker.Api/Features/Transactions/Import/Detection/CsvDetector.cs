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