using ArchtApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ArchtApp.Api.Data;

public class ArchtAppDbContext : DbContext
{
    public ArchtAppDbContext(DbContextOptions<ArchtAppDbContext> options)
        : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTrack> ProjectTracks => Set<ProjectTrack>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed initial stages so you have test data from the start
        modelBuilder.Entity<Stage>().HasData(
            new Stage { StageId = 1, Name = "Intake", SortOrder = 1 },
            new Stage { StageId = 2, Name = "Design", SortOrder = 2 },
            new Stage { StageId = 3, Name = "Client Review", SortOrder = 3 },
            new Stage { StageId = 4, Name = "Permitting", SortOrder = 4 },
            new Stage { StageId = 5, Name = "Construction", SortOrder = 5 }
        );
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
    }
}