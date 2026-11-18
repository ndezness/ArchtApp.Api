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
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The ten project stages. Steps 1-2 of the firm's process (new lead, convert to
        // client) are handled by the Leads list, because a project only exists once the
        // consultation fee has been paid.
        modelBuilder.Entity<Stage>().HasData(
            new Stage { StageId = 1, Name = "Consultation", SortOrder = 1 },
            new Stage { StageId = 2, Name = "Site Visit", SortOrder = 2 },
            new Stage { StageId = 3, Name = "Preliminary Design", SortOrder = 3 },
            new Stage { StageId = 4, Name = "Design Presentation", SortOrder = 4 },
            new Stage { StageId = 5, Name = "Design Approval", SortOrder = 5 },
            new Stage { StageId = 6, Name = "Building Plans", SortOrder = 6 },
            new Stage { StageId = 7, Name = "PMC Review", SortOrder = 7 },
            new Stage { StageId = 8, Name = "Permit Submission", SortOrder = 8 },
            new Stage { StageId = 9, Name = "Permit Collection", SortOrder = 9 },
            new Stage { StageId = 10, Name = "Project Completion", SortOrder = 10 }
        );

        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<Project>().HasIndex(p => p.Reference).IsUnique();

        modelBuilder.Entity<Setting>().HasKey(s => s.Key);
        modelBuilder.Entity<Setting>().HasData(
            new Setting { Key = "BusinessName", Value = "BDL" },
            new Setting { Key = "BusinessPhone", Value = "" },
            new Setting { Key = "BusinessEmail", Value = "" },
            new Setting { Key = "BusinessAddress", Value = "" },
            new Setting { Key = "ReferencePrefix", Value = "BDL" },
            new Setting { Key = "ConsultationFee", Value = "0" }
        );
    }
}