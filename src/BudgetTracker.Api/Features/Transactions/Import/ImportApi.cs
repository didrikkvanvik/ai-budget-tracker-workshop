using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Features.Transactions.Import.Enhancement;
using BudgetTracker.Api.AntiForgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Import;

public static class ImportApi
{
    public static IEndpointRouteBuilder MapTransactionImportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/import", ImportAsync)
            .DisableAntiforgery()
            .AddEndpointFilter<ConditionalAntiforgeryFilter>();

        routes.MapPost("/import/enhance", EnhanceImportAsync);

        return routes;
    }

    private static async Task<Results<Ok<ImportResult>, BadRequest<string>>> ImportAsync(
        IFormFile file, [FromForm] string account,
        CsvImporter csvImporter, ITransactionEnhancer enhancer,
        BudgetTrackerContext context, ClaimsPrincipal claimsPrincipal)
    {
        var validationResult = ValidateFileInput(file, account);
        if (validationResult != null)
        {
            return validationResult;
        }

        try
        {
            var userId = claimsPrincipal.GetUserId();

            var sessionHash = GenerateSessionHash(file.FileName, DateTime.UtcNow);

            using var stream = file.OpenReadStream();
            var (result, transactions) = await csvImporter.ParseCsvAsync(stream, file.FileName, userId, account);

            if (transactions.Any())
            {
                var descriptions = transactions.Select(t => t.Description).Distinct().ToList();

                var enhancedDescriptions = await enhancer.EnhanceDescriptionsAsync(
                    descriptions, account, userId, sessionHash);

                foreach (var transaction in transactions)
                {
                    var enhancement = enhancedDescriptions.FirstOrDefault(e =>
                        e.OriginalDescription == transaction.Description);

                    if (enhancement != null)
                    {
                        transaction.Description = enhancement.EnhancedDescription;
                        if (!string.IsNullOrEmpty(enhancement.SuggestedCategory))
                        {
                            transaction.Category = enhancement.SuggestedCategory;
                        }
                    }

                    transaction.ImportSessionHash = sessionHash;
                }

                await context.Transactions.AddRangeAsync(transactions);
                await context.SaveChangesAsync();

                var enhancementResults = await ProcessEnhancementsAsync(
                    enhancer, transactions, account, userId, sessionHash);

                result = CreateImportResult(result, sessionHash, enhancementResults);
            }

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Import failed: {ex.Message}");
        }
    }

    private static async Task<Results<Ok<EnhanceImportResult>, BadRequest<string>>> EnhanceImportAsync(
        [FromBody] EnhanceImportRequest request,
        [FromServices] BudgetTrackerContext context,
        ClaimsPrincipal claimsPrincipal)
    {
        try
        {
            var userId = claimsPrincipal.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return TypedResults.BadRequest("User not authenticated");

            var enhancedCount = 0;

            if (request.ApplyEnhancements)
            {
                var transactions = await context.Transactions
                    .Where(t => t.UserId == userId && t.ImportSessionHash == request.ImportSessionHash)
                    .ToListAsync();

                foreach (var enhancement in request.Enhancements)
                {
                    if (!(enhancement.ConfidenceScore >= request.MinConfidenceScore)) continue;

                    var transaction = transactions.FirstOrDefault(t => t.Id == enhancement.TransactionId);
                    if (transaction == null) continue;

                    transaction.Description = enhancement.EnhancedDescription;

                    if (!string.IsNullOrEmpty(enhancement.SuggestedCategory))
                    {
                        transaction.Category = enhancement.SuggestedCategory;
                    }

                    enhancedCount++;
                }

                if (enhancedCount > 0)
                {
                    await context.SaveChangesAsync();
                }
            }

            return TypedResults.Ok(new EnhanceImportResult
            {
                ImportSessionHash = request.ImportSessionHash,
                TotalTransactions = request.Enhancements.Count,
                EnhancedCount = enhancedCount,
                SkippedCount = request.Enhancements.Count - enhancedCount
            });
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Enhancement failed: {ex.Message}");
        }
    }

    private static string GenerateSessionHash(string fileName, DateTime timestamp)
    {
        var input = $"{fileName}_{timestamp:yyyyMMddHHmmss}_{Guid.NewGuid()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..12];
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

    private static async Task<List<TransactionEnhancementResult>> ProcessEnhancementsAsync(
        ITransactionEnhancer enhancementService, List<Transaction> transactions,
        string account, string userId, string importSessionHash)
    {
        var descriptions = transactions.Select(t => t.Description).ToList();
        var enhancements = await enhancementService.EnhanceDescriptionsAsync(descriptions, account, userId, importSessionHash);

        return enhancements.Select((enhancement, index) => new TransactionEnhancementResult
        {
            TransactionId = transactions[index].Id,
            ImportSessionHash = importSessionHash,
            TransactionIndex = index,
            OriginalDescription = enhancement.OriginalDescription,
            EnhancedDescription = enhancement.EnhancedDescription,
            SuggestedCategory = enhancement.SuggestedCategory,
            ConfidenceScore = enhancement.ConfidenceScore
        }).ToList();
    }

    private static ImportResult CreateImportResult(
        ImportResult originalResult, string importSessionHash,
        List<TransactionEnhancementResult> enhancementResults)
    {
        return new ImportResult
        {
            TotalRows = originalResult.TotalRows,
            ImportedCount = originalResult.ImportedCount,
            FailedCount = originalResult.FailedCount,
            Errors = originalResult.Errors,
            SourceFile = originalResult.SourceFile,
            ImportedAt = originalResult.ImportedAt,
            ImportSessionHash = importSessionHash,
            Enhancements = enhancementResults
        };
    }
}