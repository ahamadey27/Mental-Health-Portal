using MentalHealthPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace MentalHealthPortal.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<DocumentMetadata> DocumentMetadata { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure StoredFileName to be unique - REMOVED FOR SESSION-ONLY MVP
            // modelBuilder.Entity<DocumentMetadata>()
            //     .HasIndex(d => d.StoredFileName)
            //     .IsUnique();
        }
    }
}
