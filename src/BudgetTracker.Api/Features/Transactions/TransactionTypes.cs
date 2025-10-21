using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BudgetTracker.Api.Auth;
using Pgvector; // Add pgvector reference

namespace BudgetTracker.Api.Features.Transactions;

public class Transaction
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime Date { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Balance { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(200)]
    public string? Labels { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime ImportedAt { get; set; }

    [Required]
    [MaxLength(100)]
    public string Account { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? ImportSessionHash { get; set; }

    /// <summary>
    /// Vector embedding for semantic search (1536 dimensions for text-embedding-3-small)
    /// </summary>
    public Vector? Embedding { get; set; } // Add vector embedding property

    /// <summary>
    /// Additional categories for this transaction (many-to-many)
    /// </summary>
    public ICollection<Category.TransactionCategory> Categories { get; set; } = new List<Category.TransactionCategory>();
}

public class TransactionDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public string? Category { get; set; } // Primary category (backward compatibility)
    public List<string> Categories { get; set; } = new(); // All categories including primary
    public string? Labels { get; set; }
    public DateTime ImportedAt { get; set; }
    public string Account { get; set; } = string.Empty;
}

internal static class TransactionExtensions
{
    public static TransactionDto MapToDto(this Transaction transaction)
    {
        // Build complete categories list: primary category + additional categories
        var categories = new List<string>();
        if (!string.IsNullOrWhiteSpace(transaction.Category))
        {
            categories.Add(transaction.Category);
        }

        if (transaction.Categories != null && transaction.Categories.Any())
        {
            categories.AddRange(transaction.Categories
                .Select(c => c.CategoryName)
                .Where(name => !categories.Contains(name))); // Avoid duplicates
        }

        return new TransactionDto
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Description = transaction.Description,
            Amount = transaction.Amount,
            Balance = transaction.Balance,
            Category = transaction.Category, // Primary category
            Categories = categories, // All categories
            Labels = transaction.Labels,
            ImportedAt = transaction.ImportedAt,
            Account = transaction.Account
        };
    }
}
