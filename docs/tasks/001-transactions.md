# Workshop Step 001: Transaction Model

## Mission 🎯

In this step, you'll build the core Transaction domain model and database schema for the budget tracking application. This forms the foundation that will support CSV imports and transaction displays in later steps.

**Your goal**: Create a complete Transaction entity with proper Entity Framework Core configuration and database migration.

**Learning Objectives**:
- Entity Framework Core model creation
- Database schema design with proper indexing
- Migration generation and application

---

## Branches

**Starting branch:** `main`
**Solution branch:** `001-transactions`

---

## Prerequisites

Before starting, ensure you have:


1. **Database Setup**:
   ```bash
   # Start PostgreSQL with Docker
   cd docker/
   docker-compose up -d
   ```

2. **Verify Template**:
   ```bash
   # Backend
   cd src/BudgetTracker.Api/
   dotnet build
   dotnet run
   ```

---

## Step 1.1: Create Transaction Entity

*We start by defining what a transaction looks like in our system - this is the core domain model that will drive everything else.*

Create `src/BudgetTracker.Api/Features/Transactions/TransactionTypes.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BudgetTracker.Api.Auth;

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
}
```

## Step 1.2: Create Transaction DTO

*DTOs (Data Transfer Objects) are what we send to the frontend - they're clean versions of our entities without database-specific details.*

Add to `src/BudgetTracker.Api/Features/Transactions/TransactionTypes.cs`:

```csharp
public class TransactionDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public string? Category { get; set; }
    public string? Labels { get; set; }
    public DateTime ImportedAt { get; set; }
    public string Account { get; set; } = string.Empty;
}

internal static class TransactionExtensions
{
    public static TransactionDto MapToDto(this Transaction transaction)
    {
        return new TransactionDto
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Description = transaction.Description,
            Amount = transaction.Amount,
            Balance = transaction.Balance,
            Category = transaction.Category,
            Labels = transaction.Labels,
            ImportedAt = transaction.ImportedAt,
            Account = transaction.Account
        };
    }
}
```

## Step 1.3: Update Database Context

*The DbContext is how Entity Framework knows about our entities. We're adding our Transaction table and creating indexes for better query performance.*

Add to `src/BudgetTracker.Api/Infrastructure/BudgetTrackerContext.cs`:

```csharp
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Features.Transactions; // Add this
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

public class BudgetTrackerContext : IdentityDbContext<ApplicationUser>
{
    public BudgetTrackerContext(DbContextOptions<BudgetTrackerContext> options) : base(options)
    {
    }

    // Add this DbSet
    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Add indexes for better query performance
        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.Date);

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.UserId);

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.ImportedAt);

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .HasPrincipalKey(u => u.Id);
    }
}
```

## Step 1.4: Create and Apply Migration

*Migrations are Entity Framework's way of updating the database schema. This creates the SQL commands needed to add our Transaction table.*

```bash
cd src/BudgetTracker.Api/

# Install EF Tools if not already installed
dotnet tool install --global dotnet-ef

# Create migration
dotnet ef migrations add AddTransactionEntity

# Apply migration
dotnet ef database update
```

**Verification**: Check that the migration was created and applied successfully. You should see a new `Transactions` table in your PostgreSQL database.

---

## Summary ✅

You've successfully created:

✅ **Transaction Entity**: Core domain model with proper data annotations
✅ **Transaction DTO**: Clean data transfer object with mapping extensions
✅ **Database Context**: Updated with Transaction DbSet and performance indexes
✅ **Migration**: Applied database schema changes

**Next Step**: Move to [002-api.md](002-api.md) to create the API endpoints for transaction management.