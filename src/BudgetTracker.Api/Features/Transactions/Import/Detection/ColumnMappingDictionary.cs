namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public static class ColumnMappingDictionary
{
    // Simple English-only column mappings - if it doesn't match, use AI
    public static readonly string[] DateColumns =
        ["Date", "Transaction Date", "Posting Date", "Value Date", "Txn Date"];

    public static readonly string[] DescriptionColumns =
        ["Description", "Memo", "Details", "Transaction Description", "Reference"];

    public static readonly string[] AmountColumns =
        ["Amount", "Transaction Amount", "Debit", "Credit", "Value"];
}