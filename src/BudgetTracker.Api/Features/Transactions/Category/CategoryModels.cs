using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Api.Features.Transactions.Category;

public class TransactionCategory
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TransactionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Transaction Transaction { get; set; } = null!;
}
