using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Features.Transactions.Import.Enhancement;
using BudgetTracker.Api.Features.Transactions.Import.Detection;
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
        CsvImporter csvImporter, IImageImporter imageImporter, BudgetTrackerContext context,
        ITransactionEnhancer enhancementService, ClaimsPrincipal claimsPrincipal,
        ICsvStructureDetector detectionService, IServiceProvider serviceProvider)
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

    private static async Task<(ImportResult, List<Transaction>, CsvStructureDetectionResult?)> ProcessFileAsync(
        Stream stream, string fileName, string userId, string account,
        CsvImporter csvImporter, IImageImporter imageImporter, ICsvStructureDetector detectionService)
    {
        var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
        return fileExtension switch
        {
            ".csv" => await ProcessCsvFileAsync(stream, fileName, userId, account, csvImporter, detectionService),
            ".png" or ".jpg" or ".jpeg" => await ProcessImageFileAsync(stream, fileName, userId, account, imageImporter),
            _ => throw new InvalidOperationException("Unsupported file type")
        };
    }

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


    private static async Task<(ImportResult, List<Transaction>, CsvStructureDetectionResult?)> ProcessImageFileAsync(
        Stream stream, string fileName, string userId, string account,
        IImageImporter imageImporter)
    {
        var (importResult, transactions) = await imageImporter.ProcessImageAsync(stream, fileName, userId, account);

        return (importResult, transactions, null);
    }

    private static string GenerateImportSessionHash(string fileName, string account)
    {
        var input = $"{fileName}_{account}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..12];
    }

    private static void AssignImportSessionToTransactions(List<Transaction> transactions, string importSessionHash)
    {
        foreach (var transaction in transactions)
        {
            transaction.ImportSessionHash = importSessionHash;
        }
    }

    private static async Task SaveTransactionsAsync(BudgetTrackerContext context, List<Transaction> transactions)
    {
        if (transactions.Any())
        {
            await context.Transactions.AddRangeAsync(transactions);
            await context.SaveChangesAsync();
        }
    }

    private static BadRequest<string>? ValidateFileInput(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return TypedResults.BadRequest("Please select a valid file.");
        }

        const int maxFileSize = 10 * 1024 * 1024; // 10MB
        if (file.Length > maxFileSize)
        {
            return TypedResults.BadRequest("File size must be less than 10MB.");
        }

        var allowedExtensions = new[] { ".csv", ".png", ".jpg", ".jpeg" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(fileExtension))
        {
            return TypedResults.BadRequest("Only CSV files and images (PNG, JPG, JPEG) are supported.");
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
}