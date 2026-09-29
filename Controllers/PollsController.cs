using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookClub.Data;
using BookClub.Dtos;
using BookClub.Extensions;
using BookClub.Models;

namespace BookClub.Controllers;

[ApiController]
[Route("api/polls")]
[Authorize]
public class PollsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PollsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PollDto>> GetPoll(Guid id)
    {
        var poll = await LoadPollDto(id, User.GetUserId());
        if (poll is null)
            return NotFound("Опрос не найден");

        return poll;
    }

    [HttpPost]
    public async Task<ActionResult<PollDto>> CreatePoll(CreatePollDto dto)
    {
        var userId = User.GetUserId();
        var clubId = dto.ClubId!.Value;

        var clubExists = await _context.Clubs.AnyAsync(c => c.Id == clubId);
        if (!clubExists)
            return NotFound("Клуб не найден");

        var isMember = await _context.ClubMembers
            .AnyAsync(cm => cm.ClubId == clubId && cm.UserId == userId);
        if (!isMember)
            return StatusCode(403, "Создавать отбор могут только участники клуба");

        var now = DateTime.UtcNow;
        var hasActive = await _context.Polls
            .AnyAsync(p => p.ClubId == clubId && !p.IsClosed && p.ClosesAt > now);
        if (hasActive)
            return Conflict("В клубе уже идёт отбор");

        var bookIds = dto.BookIds.Distinct().ToList();
        if (bookIds.Count < 2)
            return BadRequest("Нужно минимум две разные книги");

        var foundBooks = await _context.Books.CountAsync(b => bookIds.Contains(b.Id));
        if (foundBooks != bookIds.Count)
            return BadRequest("Некоторые книги не найдены в каталоге");

        var poll = new Poll
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            Question = dto.Question.Trim(),
            ClosesAt = now.AddHours(dto.DurationHours)
        };

        foreach (var bookId in bookIds)
        {
            poll.Options.Add(new PollOption
            {
                Id = Guid.NewGuid(),
                BookId = bookId
            });
        }

        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        var result = await LoadPollDto(poll.Id, userId);
        return CreatedAtAction(nameof(GetPoll), new { id = poll.Id }, result);
    }

    private async Task<PollDto?> LoadPollDto(Guid id, Guid userId)
    {
        var now = DateTime.UtcNow;

        return await _context.Polls
            .Where(p => p.Id == id)
            .Select(p => new PollDto
            {
                Id = p.Id,
                ClubId = p.ClubId,
                Question = p.Question,
                IsClosed = p.IsClosed || p.ClosesAt <= now,
                ClosesAt = p.ClosesAt,
                VotersCount = _context.PollVoteChecks.Count(c => c.PollId == p.Id),
                HasVoted = _context.PollVoteChecks.Any(c => c.PollId == p.Id && c.UserId == userId),
                Options = p.Options.Select(o => new PollOptionDto
                {
                    Id = o.Id,
                    BookId = o.BookId,
                    Title = o.Book.Title,
                    Author = o.Book.Author
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }
}