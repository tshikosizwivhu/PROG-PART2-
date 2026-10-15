using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core context. The model mirrors the Part 1 ERD and RaceDay_Schema.sql
/// (same tables, keys, unique constraints, check constraints and defaults).
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Organiser> Organisers => Set<Organiser>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventRoute> Routes => Set<EventRoute>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organiser>(e =>
        {
            e.ToTable("Organiser");
            e.HasKey(x => x.OrganiserId);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(150);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255);
            e.Property(x => x.DateRegistered).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Participant>(e =>
        {
            e.ToTable("Participant");
            e.HasKey(x => x.ParticipantId);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(150);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255);
            e.Property(x => x.DateRegistered).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Event", t => t.HasCheckConstraint("CK_Event_Type", "[EventType] IN ('Run','Walk','Cycle')"));
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventName).IsRequired().HasMaxLength(150);
            e.Property(x => x.EventDate).HasColumnType("date");
            e.Property(x => x.Location).IsRequired().HasMaxLength(150);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(20);
            e.Property(x => x.DistanceKm).HasColumnType("decimal(6,2)");
            e.HasOne(x => x.Organiser)
                .WithMany(o => o.Events)
                .HasForeignKey(x => x.OrganiserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Category");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.CategoryName).IsRequired().HasMaxLength(50);
            e.Property(x => x.DistanceKm).HasColumnType("decimal(5,2)");
            e.Property(x => x.EntryFee).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            e.Property(x => x.MaxParticipants).HasDefaultValue(500);
            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EventRoute>(e =>
        {
            e.ToTable("Route");
            e.HasKey(x => x.RouteId);
            e.Property(x => x.MapUrl).HasMaxLength(255);
            e.Property(x => x.WeatherNotes).HasMaxLength(255);
            e.HasIndex(x => x.EventId).IsUnique();
            e.HasOne(x => x.Event)
                .WithOne(ev => ev.Route)
                .HasForeignKey<EventRoute>(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolment", t => t.HasCheckConstraint("CK_Enrolment_Status", "[Status] IN ('Confirmed','Cancelled')"));
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(EnrolmentStatus.Confirmed);
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId })
                .IsUnique()
                .HasDatabaseName("UQ_Enrolment_ParticipantCategory");
            e.HasOne(x => x.Participant)
                .WithMany(p => p.Enrolments)
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Result");
            e.HasKey(x => x.ResultId);
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.HasIndex(x => x.EnrolmentId).IsUnique();
            e.HasOne(x => x.Enrolment)
                .WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EnrolmentId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CapturedByOrganiser)
                .WithMany()
                .HasForeignKey(x => x.CapturedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
