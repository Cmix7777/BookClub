using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookClub.Data;
using BookClub.Dtos;
using BookClub.Extensions;
using BookClub.Models;

namespace BookClub.Controllers;

[ApiController]
[Route("api/clubs")]
[Authorize]
public class ClubsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClubsController(AppDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<ClubDto>>> GetClubs()
    {
        var now = DateTime.UtcNow;

        return await _context.Clubs
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ClubDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                MembersCount = c.Members.Count,
                HasActivePoll = c.Polls.Any(p => !p.IsClosed && p.ClosesAt > now),
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<ActionResult<ClubDto>> GetClub(Guid id)
    {
        var now = DateTime.UtcNow;

        var club = await _context.Clubs
            .Where(c => c.Id == id)
            .Select(c => new ClubDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                MembersCount = c.Members.Count,
                HasActivePoll = c.Polls.Any(p => !p.IsClosed && p.ClosesAt > now),
                CreatedAt = c.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (club is null)
            return NotFound("Клуб не найден");

        return club;
    }

    [HttpPost]
    public async Task<ActionResult<ClubDto>> CreateClub(CreateClubDto dto)
    {
        var ownerId = User.GetUserId();

        var club = new Club
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            OwnerId = ownerId
        };

        club.Members.Add(new ClubMember
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            Role = "Admin"
        });

        _context.Clubs.Add(club);
        await _context.SaveChangesAsync();

        var result = new ClubDto
        {
            Id = club.Id,
            Name = club.Name,
            Description = club.Description,
            MembersCount = 1,
            HasActivePoll = false,
            CreatedAt = club.CreatedAt
        };

        return CreatedAtAction(nameof(GetClub), new { id = club.Id }, result);
    }

    [HttpPost("{clubId}/join")]
    public async Task<IActionResult> JoinClub(Guid clubId)
    {
        var userId = User.GetUserId();

        var clubExists = await _context.Clubs.AnyAsync(c => c.Id == clubId);
        if (!clubExists)
            return NotFound("Клуб не найден");

        var alreadyMember = await _context.ClubMembers
            .AnyAsync(cm => cm.ClubId == clubId && cm.UserId == userId);
        if (alreadyMember)
            return Conflict("Вы уже состоите в этом клубе");

        _context.ClubMembers.Add(new ClubMember
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            UserId = userId,
            Role = "Member"
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("Вы уже состоите в этом клубе");
        }

        return Ok("Вы вступили в клуб");
    }

    [HttpGet("{clubId}/members")]
    public async Task<ActionResult<List<ClubMemberDto>>> GetMembers(Guid clubId)
    {
        var clubExists = await _context.Clubs.AnyAsync(c => c.Id == clubId);
        if (!clubExists)
            return NotFound("Клуб не найден");

        return await _context.ClubMembers
            .Where(cm => cm.ClubId == clubId)
            .OrderBy(cm => cm.JoinedAt)
            .Select(cm => new ClubMemberDto
            {
                UserId = cm.UserId,
                Username = cm.User.Username,
                Role = cm.Role,
                JoinedAt = cm.JoinedAt
            })
            .ToListAsync();
    }

    [HttpGet("{clubId}/polls")]
    public async Task<ActionResult<List<ClubPollDto>>> GetClubPolls(Guid clubId)
    {
        var clubExists = await _context.Clubs.AnyAsync(c => c.Id == clubId);
        if (!clubExists)
            return NotFound("Клуб не найден");

        var now = DateTime.UtcNow;

        return await _context.Polls
            .Where(p => p.ClubId == clubId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ClubPollDto
            {
                Id = p.Id,
                Question = p.Question,
                IsClosed = p.IsClosed || p.ClosesAt <= now,
                ClosesAt = p.ClosesAt,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();
    }
    }