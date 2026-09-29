namespace BookClub.Models;

public class UserBook
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    public string Status { get; set; } = "WantToRead"; // WantToRead | Reading | Finished
    public int? Rating { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}