using BudgetTracker.Api.Features.Transactions.Import;
using BudgetTracker.Api.Features.Transactions.List;
using BudgetTracker.Api.Features.Transactions.Category;

namespace BudgetTracker.Api.Features.Transactions;

public static class TransactionApi
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder routes)
    {
        var transactionsGroup = routes.MapGroup("/transactions")
            .WithTags("Transactions")
            .WithOpenApi()
            .RequireAuthorization();

        transactionsGroup
            .MapTransactionImportEndpoints()
            .MapTransactionListEndpoint()
            .MapCategoryEndpoints();

        return routes;
    }
}