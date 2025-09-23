# Workshop Step 003: CSV Import Implementation

## Mission 🎯

In this step, you'll implement the core CSV parsing functionality that transforms uploaded CSV files into Transaction objects and stores them in the database. You'll build a flexible parser that handles different bank formats.

**Your goal**: Complete the CSV import feature with robust parsing, validation, and error handling that works with various bank CSV formats.

**Learning Objectives**:
- CSV parsing with CsvHelper library
- Flexible column mapping for different formats
- Service pattern implementation
- Error handling and validation
- Dependency injection registration

---

## Prerequisites

Before starting, ensure you completed:
- [001-transactions.md](001-transactions.md) - Transaction model and database setup
- [002-api.md](002-api.md) - API endpoints with placeholder import

---

## Branches

**Starting branch:** `002-api`
**Solution branch:** `003-csv-import`

---

## Step 3.1: Install CSV Parsing Package

*CsvHelper is a robust library that handles the complexities of CSV parsing - different delimiters, escaping, and column mapping.*

```bash
cd src/BudgetTracker.Api/
dotnet add package CsvHelper --version 33.1.0
```

## Step 3.2: Create CSV Import Service

*This is the core business logic that reads CSV files and converts them to Transaction objects. It handles flexible column mapping so it works with different bank formats.*

Create `src/BudgetTracker.Api/Features/Transactions/Import/Processing/CsvImporter.cs`:

```csharp
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using BudgetTracker.Api.Features.Transactions;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class CsvImporter
{
    public async Task<(ImportResult Result, List<Transaction> Transactions)> ParseCsvAsync(
        Stream csvStream, string sourceFileName, string userId, string account)
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
                BadDataFound = null
            });

            var rowNumber = 0;

            await foreach (var record in csv.GetRecordsAsync<dynamic>())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    var transaction = ParseTransactionRow(record);
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

    private Transaction? ParseTransactionRow(dynamic record)
    {
        try
        {
            var recordDict = (IDictionary<string, object>)record;

            // Flexible column mapping - try common variations
            var description = GetColumnValue(recordDict, "Description", "Memo", "Details");
            var dateStr = GetColumnValue(recordDict, "Date", "Transaction Date", "Posting Date");
            var amountStr = GetColumnValue(recordDict, "Amount", "Transaction Amount", "Debit", "Credit");
            var balanceStr = GetColumnValue(recordDict, "Balance", "Running Balance", "Account Balance");
            var category = GetColumnValue(recordDict, "Category", "Type", "Transaction Type");

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

            // Parse date with culture-aware parsing
            if (!TryParseDate(dateStr, out var date))
            {
                throw new ArgumentException($"Invalid date format: {dateStr}");
            }

            // Parse amount using culture-aware parsing
            if (!TryParseAmount(amountStr, out var amount))
            {
                throw new ArgumentException($"Invalid amount format: {amountStr}");
            }

            // Parse balance (optional)
            decimal? balance = null;
            if (!string.IsNullOrWhiteSpace(balanceStr))
            {
                if (TryParseAmount(balanceStr, out var parsedBalance))
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

    private bool TryParseDate(string dateStr, out DateTime date)
    {
        date = default;

        // Try culture-aware parsing first
        if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseAmount(string amountStr, out decimal amount)
    {
        amount = 0;

        if (string.IsNullOrWhiteSpace(amountStr))
            return false;

        var cleanAmount = amountStr.Trim();

        // Remove common currency symbols
        cleanAmount = cleanAmount.Replace("$", "").Replace("€", "").Replace("£", "").Replace("¥", "").Replace("R$", "").Trim();

        // Use culture-specific parsing - .NET handles decimal/thousand separators automatically
        return decimal.TryParse(cleanAmount, NumberStyles.Currency, CultureInfo.InvariantCulture, out amount);
    }
}
```

## Step 3.3: Register Service in DI Container

*Dependency injection makes our service available throughout the application. This registers our CSV service so it can be injected into API endpoints.*

Add to `src/BudgetTracker.Api/Program.cs` (after the DbContext registration):

```csharp
using BudgetTracker.Api.Features.Transactions.Import.Processing;

// Add CSV Import Service
builder.Services.AddScoped<CsvImporter>();
```

## Step 3.4: Update Import Endpoint

*Now we can replace the placeholder import endpoint with the real implementation that uses our CSV service.*

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportApi.cs`:

```csharp
using System.Security.Claims;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.AntiForgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Api.Features.Transactions.Import;

public static class ImportApi
{
    public static IEndpointRouteBuilder MapTransactionImportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/import", ImportAsync)
            .DisableAntiforgery()
            .AddEndpointFilter<ConditionalAntiforgeryFilter>();

        return routes;
    }

    private static async Task<Results<Ok<ImportResult>, BadRequest<string>>> ImportAsync(
        IFormFile file, [FromForm] string account,
        CsvImporter csvImporter, BudgetTrackerContext context, ClaimsPrincipal claimsPrincipal)
    {
        var validationResult = ValidateFileInput(file, account);
        if (validationResult != null)
        {
            return validationResult;
        }

        try
        {
            var userId = claimsPrincipal.GetUserId();

            using var stream = file.OpenReadStream();
            var (result, transactions) = await csvImporter.ParseCsvAsync(stream, file.FileName, userId, account);

            if (transactions.Any())
            {
                await context.Transactions.AddRangeAsync(transactions);
                await context.SaveChangesAsync();
            }

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Import failed: {ex.Message}");
        }
    }

    private static BadRequest<string>? ValidateFileInput(IFormFile file, string account)
    {
        if (file == null || file.Length == 0)
        {
            return TypedResults.BadRequest("No file uploaded");
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest("Only CSV files are supported");
        }

        if (file.Length > 10 * 1024 * 1024) // 10MB limit
        {
            return TypedResults.BadRequest("File size exceeds 10MB limit");
        }

        if (string.IsNullOrWhiteSpace(account))
        {
            return TypedResults.BadRequest("Account name is required");
        }

        return null;
    }
}
```

## Step 3.5: Test the Complete Import

Use one of the provided sample CSV files located in `samples/`. For this test, we'll use the `generic-bank-sample.csv` file:

**Available Sample Files:**
- `generic-bank-sample.csv` - Simple format with Date, Description, Amount, Balance
- `chase-bank-sample.csv` - Chase Bank format
- `bank-of-america-sample.csv` - Bank of America format
- `activobank-pt-sample.csv` - Portuguese bank format

Update `test-api.http`:

```http
### Test Complete Import with Sample CSV
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="generic-bank-sample.csv"
Content-Type: text/csv

Date,Description,Amount,Balance
01/15/2025,Amazon Purchase,-45.67,1250.33
01/16/2025,Coffee Shop,-5.89,1244.44
01/17/2025,Salary Deposit,2500.00,3744.44
01/18/2025,Netflix Subscription,-15.99,3728.45
01/19/2025,Gas Station,-52.30,3676.15
01/20/2025,Grocery Store,-89.45,3586.70
01/21/2025,Uber Ride,-12.50,3574.20
01/22/2025,Apple Services,-2.99,3571.21
01/23/2025,Cash Withdrawal,-60.00,3511.21
01/24/2025,Music Streaming,-9.99,3501.22
--WebAppBoundary--

### Test Transaction List After Import
GET http://localhost:5295/api/transactions
X-API-Key: test-key-user1
```

**Alternative: Using VS Code REST Client**
You can also test by uploading the actual sample file. In VS Code with the REST Client extension, you can reference the sample file directly:

```http
### Test Import with File Reference
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="generic-bank-sample.csv"
Content-Type: text/csv

< ./generic-bank-sample.csv
--WebAppBoundary--
```

## Step 3.6: Verify Database Storage

After running the import test:

```bash
# Connect to PostgreSQL
docker exec -it docker-postgres-1 psql -U budgetuser -d budgettracker

# Check imported transactions
SELECT id, date, description, amount, account, category FROM "Transactions" LIMIT 10;
```

---

## Expected Import Response

```json
{
  "totalRows": 10,
  "importedCount": 10,
  "failedCount": 0,
  "errors": [],
  "sourceFile": "generic-bank-sample.csv",
  "importedAt": "2025-01-15T10:30:00Z"
}
```

---

## Troubleshooting 🔧

### Common Issues

1. **CSV Parsing Errors**:
   - Check that CSV headers match expected column names
   - Verify date formats are supported
   - Ensure amounts don't contain unsupported characters

2. **Database Connection Issues**:
   - Verify PostgreSQL is running
   - Check that migrations are applied

3. **Service Registration Issues**:
   - Ensure `CsvImporter` is registered in DI container
   - Verify using statements are included

---

## Summary ✅

You've successfully implemented:

✅ **CSV Parsing Service**: Flexible parser handling multiple bank formats with culture-aware parsing
✅ **Import Result Tracking**: Detailed success/failure reporting with proper error counting
✅ **Error Handling**: Comprehensive validation and error messages with specific field validation
✅ **Database Integration**: Automatic storage of parsed transactions with proper async patterns
✅ **Service Registration**: Simple dependency injection setup without unnecessary interfaces
✅ **Feature-Based Architecture**: Well-organized code structure following clean architecture patterns
✅ **Simplified Design**: Direct service usage without interface abstraction for workshop clarity

**Next Step**: Move to [004-react-transactions-list.md](004-react-transactions-list.md) to build the React frontend component that displays the imported transactions.