using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookClub.Data;
using BookClub.Dtos;

namespace BookClub.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats()
    {
        var now = DateTime.UtcNow;

        return new AdminStatsDto
        {
            Users = await _context.Users.CountAsync(),
            Admins = await _context.Users.CountAsync(u => u.Role == "Admin"),
            BlockedUsers = await _context.Users.CountAsync(u => u.IsBlocked),
            Books = await _context.Books.CountAsync(),
            Clubs = await _context.Clubs.CountAsync(),
            ActivePolls = await _context.Polls.CountAsync(p => !p.IsClosed && p.ClosesAt > now),
            Votes = await _context.Votes.CountAsync(),
            Reviews = await _context.Reviews.CountAsync()
        };
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserDto>>> GetUsers()
    {
        return await _context.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role,
                IsBlocked = u.IsBlocked,
                CreatedAt = u.CreatedAt,
                ClubsCount = _context.ClubMembers.Count(cm => cm.UserId == u.Id),
                ReviewsCount = _context.Reviews.Count(r => r.UserId == u.Id)
            })
            .ToListAsync();
    }
}