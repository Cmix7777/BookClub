namespace BookClub.Models;

public class PollOption
{
    public Guid Id { get; set; }

    public Guid PollId { get; set; }
    public Poll Poll { get; set; } = null!;

    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    public List<Vote> Votes { get; set; } = new();
}