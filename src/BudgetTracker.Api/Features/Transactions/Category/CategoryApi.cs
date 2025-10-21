using System.Security.Claims;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Category;

public static class CategoryApi
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/{transactionId:guid}/categories",
            async (BudgetTrackerContext db, ClaimsPrincipal claimsPrincipal,
                Guid transactionId, [FromBody] AddCategoryRequest request) =>
            {
                var userId = claimsPrincipal.GetUserId();

                // Verify transaction exists and belongs to user
                var transaction = await db.Transactions
                    .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId);

                if (transaction == null)
                {
                    return Results.NotFound(new { error = "Transaction not found" });
                }

                // Check if category already exists for this transaction
                var existingCategory = await db.TransactionCategories
                    .FirstOrDefaultAsync(tc =>
                        tc.TransactionId == transactionId &&
                        tc.CategoryName == request.CategoryName);

                if (existingCategory != null)
                {
                    return Results.BadRequest(new { error = "Category already exists for this transaction" });
                }

                var transactionCategory = new TransactionCategory
                {
                    TransactionId = transactionId,
                    CategoryName = request.CategoryName,
                    UserId = userId
                };

                db.TransactionCategories.Add(transactionCategory);
                await db.SaveChangesAsync();

                return Results.Ok(new { id = transactionCategory.Id, categoryName = transactionCategory.CategoryName });
            });

        routes.MapDelete("/{transactionId:guid}/categories/{categoryName}",
            async (BudgetTrackerContext db, ClaimsPrincipal claimsPrincipal,
                Guid transactionId, string categoryName) =>
            {
                var userId = claimsPrincipal.GetUserId();

                var transactionCategory = await db.TransactionCategories
                    .FirstOrDefaultAsync(tc =>
                        tc.TransactionId == transactionId &&
                        tc.CategoryName == categoryName &&
                        tc.UserId == userId);

                if (transactionCategory == null)
                {
                    return Results.NotFound(new { error = "Category not found" });
                }

                db.TransactionCategories.Remove(transactionCategory);
                await db.SaveChangesAsync();

                return Results.Ok(new { message = "Category removed successfully" });
            });

        routes.MapPost("/bulk-categories",
            async (BudgetTrackerContext db, ClaimsPrincipal claimsPrincipal,
                [FromBody] BulkAddCategoriesRequest request) =>
            {
                var userId = claimsPrincipal.GetUserId();

                // Verify all transactions exist and belong to user
                var transactions = await db.Transactions
                    .Where(t => request.TransactionIds.Contains(t.Id) && t.UserId == userId)
                    .Select(t => t.Id)
                    .ToListAsync();

                if (transactions.Count != request.TransactionIds.Count)
                {
                    return Results.BadRequest(new { error = "Some transactions not found or do not belong to user" });
                }

                var categoriesToAdd = new List<TransactionCategory>();

                foreach (var transactionId in request.TransactionIds)
                {
                    foreach (var categoryName in request.CategoryNames)
                    {
                        // Check if category already exists
                        var exists = await db.TransactionCategories
                            .AnyAsync(tc => tc.TransactionId == transactionId && tc.CategoryName == categoryName);

                        if (!exists)
                        {
                            categoriesToAdd.Add(new TransactionCategory
                            {
                                TransactionId = transactionId,
                                CategoryName = categoryName,
                                UserId = userId
                            });
                        }
                    }
                }

                if (categoriesToAdd.Count > 0)
                {
                    db.TransactionCategories.AddRange(categoriesToAdd);
                    await db.SaveChangesAsync();
                }

                return Results.Ok(new
                {
                    addedCount = categoriesToAdd.Count,
                    message = $"Added {categoriesToAdd.Count} categories to {request.TransactionIds.Count} transactions"
                });
            });

        return routes;
    }
}

public record AddCategoryRequest(string CategoryName);
public record BulkAddCategoriesRequest(List<Guid> TransactionIds, List<string> CategoryNames);
