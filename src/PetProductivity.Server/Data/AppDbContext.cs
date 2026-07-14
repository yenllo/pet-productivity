using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PetProductivity.Shared.Models;
using System.Text.Json;

namespace PetProductivity.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Pet> Pets { get; set; }
    public DbSet<TaskItem> TaskItems { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<SharedPet> SharedPets { get; set; }
    public DbSet<GroupMembership> GroupMemberships { get; set; }
    public DbSet<JoinRequest> JoinRequests { get; set; }
    public DbSet<FocusSession> FocusSessions { get; set; }
    public DbSet<TaskApproval> TaskApprovals { get; set; }
    public DbSet<FocusProof> FocusProofs { get; set; }
    public DbSet<GroupFocusSession> GroupFocusSessions { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; } // T14-C0

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasOne(u => u.UserPet);

        // T14-C0: lookup por hash en cada refresh; único porque el hash identifica al token.
        modelBuilder.Entity<RefreshToken>().HasIndex(r => r.Hash).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(r => r.UserId);

        modelBuilder.Entity<User>().Property(u => u.Inventory)
            .HasConversion(v => Json(v), v => FromJson<Dictionary<string, int>>(v))
            .Metadata.SetValueComparer(JsonCmp<Dictionary<string, int>>());

        modelBuilder.Entity<User>().Property(u => u.PlacedFurniture)
            .HasConversion(v => Json(v), v => FromJson<List<PlacedFurniture>>(v))
            .Metadata.SetValueComparer(JsonCmp<List<PlacedFurniture>>());

        modelBuilder.Entity<User>().Property(u => u.LastNotifications)
            .HasConversion(v => Json(v), v => FromJson<Dictionary<string, DateTime>>(v))
            .Metadata.SetValueComparer(JsonCmp<Dictionary<string, DateTime>>());

        modelBuilder.Entity<Pet>().Property(p => p.Stats)
            .HasConversion(v => Json(v), v => FromJson<Dictionary<string, double>>(v))
            .Metadata.SetValueComparer(JsonCmp<Dictionary<string, double>>());

        modelBuilder.Entity<Group>().Property(g => g.MemberIds)
            .HasConversion(v => Json(v), v => FromJson<List<Guid>>(v))
            .Metadata.SetValueComparer(JsonCmp<List<Guid>>());

        modelBuilder.Entity<SharedPet>().Property(sp => sp.UserAffection)
            .HasConversion(v => Json(v), v => FromJson<Dictionary<Guid, double>>(v))
            .Metadata.SetValueComparer(JsonCmp<Dictionary<Guid, double>>());

        modelBuilder.Entity<SharedPet>().Property(sp => sp.HatchVotes)
            .HasConversion(v => Json(v), v => FromJson<List<Guid>>(v))
            .Metadata.SetValueComparer(JsonCmp<List<Guid>>());

        modelBuilder.Entity<JoinRequest>().Property(j => j.Approvals)
            .HasConversion(v => Json(v), v => FromJson<List<Guid>>(v))
            .Metadata.SetValueComparer(JsonCmp<List<Guid>>());

        modelBuilder.Entity<TaskApproval>().Property(t => t.Approvals)
            .HasConversion(v => Json(v), v => FromJson<List<Guid>>(v))
            .Metadata.SetValueComparer(JsonCmp<List<Guid>>());

        modelBuilder.Entity<GroupFocusSession>().Property(g => g.Participants)
            .HasConversion(v => Json(v), v => FromJson<List<Guid>>(v))
            .Metadata.SetValueComparer(JsonCmp<List<Guid>>());

        // FK real Group → SharedPet (opcional: tolera SharedPetId vacío en filas legadas)
        modelBuilder.Entity<Group>()
            .HasOne(g => g.SharedPet).WithOne()
            .HasForeignKey<Group>(g => g.SharedPetId).IsRequired(false);

        modelBuilder.Entity<Group>().HasIndex(g => g.InviteCode).IsUnique();
        modelBuilder.Entity<GroupMembership>().HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();

        // T23: índices de los caminos calientes (antes estas tablas solo tenían PK → seq-scan).
        modelBuilder.Entity<TaskItem>().HasIndex(t => new { t.UserId, t.CreatedAt });  // dedupe, rendimientos, historial
        modelBuilder.Entity<TaskItem>().HasIndex(t => new { t.PetId, t.CreatedAt });   // historial del grupo
        modelBuilder.Entity<TaskItem>().HasIndex(t => t.ProofId);                      // autorización de GET /proof
        modelBuilder.Entity<TaskApproval>().HasIndex(t => t.GroupId);
        modelBuilder.Entity<JoinRequest>().HasIndex(j => new { j.GroupId, j.RequesterUserId }).IsUnique(); // + cierra la doble solicitud
        modelBuilder.Entity<FocusSession>().HasIndex(s => new { s.UserId, s.PetId });
        modelBuilder.Entity<FocusProof>().HasIndex(p => new { p.SessionId, p.UserId });
        modelBuilder.Entity<GroupFocusSession>().HasIndex(s => s.GroupId);
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();                 // login + cierra la carrera de registro
    }

    // ponytail: one JSON round-trip + comparer for every serialized collection prop.
    // Order-sensitive equality (same as the old SequenceEqual comparers).
    private static string Json<T>(T v) => JsonSerializer.Serialize(v, JsonSerializerOptions.Default);
    // Tolerante a '' / null: las migraciones crean columnas JSON nuevas con default "" en las filas
    // existentes, y Deserialize("") lanza — esto rompió el login en vivo tras AddNotificationLog (T17-smoke).
    private static T FromJson<T>(string v) where T : new()
        => string.IsNullOrWhiteSpace(v) ? new T() : JsonSerializer.Deserialize<T>(v, JsonSerializerOptions.Default) ?? new T();
    private static ValueComparer<T> JsonCmp<T>() => new(
        (a, b) => Json(a) == Json(b),
        v => Json(v).GetHashCode(),
        v => JsonSerializer.Deserialize<T>(Json(v), JsonSerializerOptions.Default)!);
}
