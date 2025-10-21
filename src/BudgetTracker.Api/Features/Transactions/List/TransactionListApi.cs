using System.Security.Claims;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.List;

public static class TransactionListApi
{
    public static IEndpointRouteBuilder MapTransactionListEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/",
                async (BudgetTrackerContext db, ClaimsPrincipal claimsPrincipal,
                    int page = 1, int pageSize = 20, string? category = null, string? account = null) =>
                {
                    if (page < 1) page = 1;
                    if (pageSize < 1 || pageSize > 100) pageSize = 20;

                    var query = db.Transactions.Where(t => t.UserId == claimsPrincipal.GetUserId());

                    // Apply category filter
                    if (!string.IsNullOrWhiteSpace(category))
                    {
                        query = query.Where(t => t.Category == category);
                    }

                    // Apply account filter
                    if (!string.IsNullOrWhiteSpace(account))
                    {
                        query = query.Where(t => t.Account == account);
                    }

                    var totalCount = await query.CountAsync();

                    var items = await query
                        .OrderByDescending(t => t.Date)
                        .ThenByDescending(t => t.ImportedAt)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    var result = new PagedResult<Transaction>
                    {
                        Items = items,
                        TotalCount = totalCount,
                        Page = page,
                        PageSize = pageSize
                    };

                    return Results.Ok(result);
                });

        routes.MapGet("/filters",
                async (BudgetTrackerContext db, ClaimsPrincipal claimsPrincipal) =>
                {
                    var userId = claimsPrincipal.GetUserId();

                    var categories = await db.Transactions
                        .Where(t => t.UserId == userId && !string.IsNullOrEmpty(t.Category))
                        .Select(t => t.Category!)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToListAsync();

                    var accounts = await db.Transactions
                        .Where(t => t.UserId == userId)
                        .Select(t => t.Account)
                        .Distinct()
                        .OrderBy(a => a)
                        .ToListAsync();

                    return Results.Ok(new { categories, accounts });
                });

        return routes;
    }
}