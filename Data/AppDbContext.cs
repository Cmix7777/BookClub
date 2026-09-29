using Microsoft.EntityFrameworkCore;
using BookClub.Models;

namespace BookClub.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<UserBook> UserBooks { get; set; }
    public DbSet<Club> Clubs { get; set; }
    public DbSet<ClubMember> ClubMembers { get; set; }
    public DbSet<Poll> Polls { get; set; }
    public DbSet<PollOption> PollOptions { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<PollVoteCheck> PollVoteChecks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Один пользователь — один голос в одном опросе.
        // Защита на уровне БД, а не кода.
        modelBuilder.Entity<PollVoteCheck>()
            .HasIndex(pvc => new { pvc.PollId, pvc.UserId })
            .IsUnique();

        // Один пользователь не может добавить одну и ту же книгу на полку дважды
        modelBuilder.Entity<UserBook>()
            .HasIndex(ub => new { ub.UserId, ub.BookId })
            .IsUnique();

        // Один пользователь состоит в клубе только один раз
        modelBuilder.Entity<ClubMember>()
            .HasIndex(cm => new { cm.ClubId, cm.UserId })
            .IsUnique();

        // Владелец клуба: запрещаем удалять пользователя, если у него есть клубы
        modelBuilder.Entity<Club>()
            .HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Poll>()
            .Property(p => p.ClosesAt)
            .HasDefaultValueSql("now() + interval '48 hours'");
    }
}