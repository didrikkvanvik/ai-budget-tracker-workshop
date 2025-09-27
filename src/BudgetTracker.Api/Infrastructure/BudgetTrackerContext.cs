using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Intelligence.Recommendations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore; // Add pgvector EF Core support

namespace BudgetTracker.Api.Infrastructure;

public class BudgetTrackerContext : IdentityDbContext<ApplicationUser>
{
    public BudgetTrackerContext(DbContextOptions<BudgetTrackerContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // Configure Transaction entity
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Transactions_UserId");

            // Composite index for RAG context queries (most selective first)
            entity.HasIndex(e => new { e.UserId, e.Account, e.Date })
                .HasDatabaseName("IX_Transactions_RagContext")
                .IsDescending(false, false, true); // Date descending for recent first

            // Category index for context analysis (with filter for non-null values)
            entity.HasIndex(e => e.Category)
                .HasDatabaseName("IX_Transactions_Category")
                .HasFilter("\"Category\" IS NOT NULL");

            // Configure vector column with explicit dimensions (1536 for text-embedding-3-small)
            entity.Property(e => e.Embedding)
                .HasColumnType("vector(1536)");

            // Vector index for semantic search (HNSW for fast similarity search)
            entity.HasIndex(e => e.Embedding)
                .HasDatabaseName("IX_Transactions_Embedding")
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .HasPrincipalKey(u => u.Id);
        });
    }
}
