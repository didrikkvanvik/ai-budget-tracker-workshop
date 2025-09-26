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