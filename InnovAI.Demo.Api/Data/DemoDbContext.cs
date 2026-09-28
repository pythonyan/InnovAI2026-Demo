using Microsoft.EntityFrameworkCore;
using InnovAI.Demo.Api.Data.Entities;

namespace InnovAI.Demo.Api.Data;

public class DemoDbContext(DbContextOptions<DemoDbContext> options) : DbContext(options)
{
    public DbSet<MailData> MailData { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailData>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasMany(x => x.Chunks)
             .WithOne(x => x.Document)
             .HasForeignKey(x => x.DocumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // DocumentChunk.Embedding stored as SQL Server VECTOR column via EFCore.SqlServer.VectorSearch
        modelBuilder.Entity<DocumentChunk>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Embedding).HasColumnType("VECTOR(1536)");
        });
    }
}
