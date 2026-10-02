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
    public DbSet<Review> Reviews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // один голос на пользователя в одном опросе
        modelBuilder.Entity<PollVoteCheck>()
            .HasIndex(pvc => new { pvc.PollId, pvc.UserId })
            .IsUnique();

        // одна книга на полке пользователя один раз
        modelBuilder.Entity<UserBook>()
            .HasIndex(ub => new { ub.UserId, ub.BookId })
            .IsUnique();

        // пользователь состоит в клубе один раз
        modelBuilder.Entity<ClubMember>()
            .HasIndex(cm => new { cm.ClubId, cm.UserId })
            .IsUnique();

        // нельзя удалить пользователя, пока он владеет клубом
        modelBuilder.Entity<Club>()
            .HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // уникальные логин и email
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // дедлайн опроса по умолчанию — через 48 часов
        modelBuilder.Entity<Poll>()
            .Property(p => p.ClosesAt)
            .HasDefaultValueSql("now() + interval '48 hours'");

        // одна рецензия пользователя на книгу
        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.UserId, r.BookId })
            .IsUnique();

        // оценка только от 1 до 5 — проверяет сама база
        modelBuilder.Entity<Review>()
            .ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "\"Rating\" BETWEEN 1 AND 5"));

        // у существующих пользователей роль станет "User"
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasDefaultValue("User");

        // роль — только одно из двух значений
        modelBuilder.Entity<User>()
            .ToTable(t => t.HasCheckConstraint("CK_Users_Role", "\"Role\" IN ('User', 'Admin')"));
    }
}