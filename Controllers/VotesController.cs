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
public class VotesController : ControllerBase
{
    private readonly AppDbContext _context;

    public VotesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("{pollId}/vote")]
    public async Task<IActionResult> Vote(Guid pollId, VoteDto dto)
    {
        var userId = User.GetUserId();
        var optionId = dto.PollOptionId!.Value;

        var poll = await _context.Polls.FirstOrDefaultAsync(p => p.Id == pollId);
        if (poll is null)
            return NotFound("Опрос не найден");

        if (poll.IsClosed || poll.ClosesAt <= DateTime.UtcNow)
            return BadRequest("Голосование уже закрыто");

        var isMember = await _context.ClubMembers
            .AnyAsync(cm => cm.ClubId == poll.ClubId && cm.UserId == userId);
        if (!isMember)
            return StatusCode(403, "Голосовать могут только участники клуба");

        var optionBelongs = await _context.PollOptions
            .AnyAsync(o => o.Id == optionId && o.PollId == pollId);
        if (!optionBelongs)
            return BadRequest("Этот вариант не относится к данному опросу");

        var alreadyVoted = await _context.PollVoteChecks
            .AnyAsync(c => c.PollId == pollId && c.UserId == userId);
        if (alreadyVoted)
            return Conflict("Вы уже голосовали в этом опросе");

        using var transaction = await _context.Database.BeginTransactionAsync();

        _context.PollVoteChecks.Add(new PollVoteCheck
        {
            Id = Guid.NewGuid(),
            PollId = pollId,
            UserId = userId
        });

        _context.Votes.Add(new Vote
        {
            Id = Guid.NewGuid(),
            PollOptionId = optionId
        });

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("Вы уже голосовали в этом опросе");
        }

        return Ok("Голос принят");
    }

    [HttpGet("{pollId}/results")]
    public async Task<ActionResult<PollResultsDto>> GetResults(Guid pollId)
    {
        var poll = await _context.Polls
            .Include(p => p.Options)
                .ThenInclude(o => o.Book)
            .FirstOrDefaultAsync(p => p.Id == pollId);

        if (poll is null)
            return NotFound("Опрос не найден");

        var isClosed = poll.IsClosed || poll.ClosesAt <= DateTime.UtcNow;

        var dto = new PollResultsDto
        {
            PollId = poll.Id,
            Question = poll.Question,
            IsClosed = isClosed,
            ClosesAt = poll.ClosesAt,
            VotersCount = await _context.PollVoteChecks.CountAsync(c => c.PollId == pollId),
            MembersCount = await _context.ClubMembers.CountAsync(cm => cm.ClubId == poll.ClubId)
        };

        // конверт запечатан: распределение голосов не отдаём
        if (!isClosed)
            return dto;

        var counts = await _context.Votes
            .Where(v => v.PollOption.PollId == pollId)
            .GroupBy(v => v.PollOptionId)
            .Select(g => new { OptionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OptionId, x => x.Count);

        var total = counts.Values.Sum();
        var max = counts.Count == 0 ? 0 : counts.Values.Max();

        dto.TotalVotes = total;
        dto.Results = poll.Options.Select(o =>
        {
            var count = counts.GetValueOrDefault(o.Id, 0);
            return new PollOptionResultDto
            {
                PollOptionId = o.Id,
                Title = o.Book.Title,
                Author = o.Book.Author,
                VoteCount = count,
                Percentage = total == 0 ? 0 : Math.Round(count * 100.0 / total, 1),
                IsWinner = max > 0 && count == max
            };
        })
        .OrderByDescending(r => r.VoteCount)
        .ToList();

        return dto;
    }

    [HttpPost("{pollId}/close")]
    public async Task<IActionResult> ClosePoll(Guid pollId)
    {
        var userId = User.GetUserId();

        var poll = await _context.Polls.FirstOrDefaultAsync(p => p.Id == pollId);
        if (poll is null)
            return NotFound("Опрос не найден");

        var isAdmin = await _context.ClubMembers.AnyAsync(cm =>
            cm.ClubId == poll.ClubId && cm.UserId == userId && cm.Role == "Admin");
        if (!isAdmin)
            return StatusCode(403, "Закрыть отбор может только администратор клуба");

        if (poll.IsClosed || poll.ClosesAt <= DateTime.UtcNow)
            return Conflict("Отбор уже закрыт");

        poll.IsClosed = true;
        await _context.SaveChangesAsync();

        return Ok("Отбор закрыт, конверт вскрыт");
    }
}