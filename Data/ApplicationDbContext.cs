using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BubbleSortMvc.Models;

namespace BubbleSortMvc.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<SavedSort> SavedSorts => Set<SavedSort>();
    public DbSet<TrainingDataset> TrainingDatasets => Set<TrainingDataset>();
    public DbSet<TrainingRun> TrainingRuns => Set<TrainingRun>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<SavedSort>()
            .HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TrainingDataset>()
            .HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TrainingRun>()
            .HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TrainingRun>()
            .HasOne(item => item.Dataset)
            .WithMany()
            .HasForeignKey(item => item.TrainingDatasetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
