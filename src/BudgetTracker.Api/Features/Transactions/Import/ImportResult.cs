namespace BudgetTracker.Api.Features.Transactions.Import;

public class ImportResult
{
    public string SourceFile { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalRows { get; set; }
    public List<string> Errors { get; set; } = new();
    public string ImportSessionHash { get; set; } = string.Empty;
    public List<TransactionEnhancementResult> Enhancements { get; set; } = new();
    public string? DetectionMethod { get; set; } // "RuleBased" or "AI"
    public double DetectionConfidence { get; set; } // 0-100
}

public class EnhanceImportRequest
{
    public string ImportSessionHash { get; set; } = string.Empty;
    public List<TransactionEnhancementResult> Enhancements { get; set; } = new();
    public double MinConfidenceScore { get; set; } = 0.5;
    public bool ApplyEnhancements { get; set; } = true;
}

public class EnhanceImportResult
{
    public string ImportSessionHash { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public int EnhancedCount { get; set; }
    public int SkippedCount { get; set; }
}

public class TransactionEnhancementResult
{
    public Guid TransactionId { get; set; }
    public string ImportSessionHash { get; set; } = string.Empty;
    public int TransactionIndex { get; set; }
    public string OriginalDescription { get; set; } = string.Empty;
    public string EnhancedDescription { get; set; } = string.Empty;
    public string? SuggestedCategory { get; set; }
    public double ConfidenceScore { get; set; }
}