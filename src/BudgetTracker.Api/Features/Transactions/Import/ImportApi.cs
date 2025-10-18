using System.Security.Claims;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
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

            // TODO: Implement CSV parsing in next step
            // For now, return a placeholder response
            var result = new ImportResult
            {
                TotalRows = 0,
                ImportedCount = 0,
                FailedCount = 0,
                Errors = new List<string>(),
                SourceFile = file.FileName,
                ImportedAt = DateTime.UtcNow
            };

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex.Message);
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