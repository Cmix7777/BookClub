using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookClub.Data;
using BookClub.Dtos;
using BookClub.Extensions;

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

    // ---------- статистика и пользователи ----------

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

    [HttpPost("users/{id}/block")]
    public async Task<IActionResult> BlockUser(Guid id)
    {
        if (id == User.GetUserId())
            return BadRequest("Нельзя заблокировать самого себя");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound("Пользователь не найден");

        if (user.Role == "Admin")
            return StatusCode(403, "Нельзя заблокировать администратора — сначала снимите роль");

        if (user.IsBlocked)
            return Conflict("Пользователь уже заблокирован");

        user.IsBlocked = true;
        await _context.SaveChangesAsync();

        return Ok($"Пользователь {user.Username} заблокирован");
    }

    [HttpPost("users/{id}/unblock")]
    public async Task<IActionResult> UnblockUser(Guid id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound("Пользователь не найден");

        if (!user.IsBlocked)
            return Conflict("Пользователь не заблокирован");

        user.IsBlocked = false;
        await _context.SaveChangesAsync();

        return Ok($"Пользователь {user.Username} разблокирован");
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeRoleDto dto)
    {
        if (id == User.GetUserId())
            return BadRequest("Нельзя изменить собственную роль");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound("Пользователь не найден");

        if (user.Role == dto.Role)
            return Conflict($"У пользователя уже роль {dto.Role}");

        if (dto.Role == "Admin" && user.IsBlocked)
            return BadRequest("Сначала разблокируйте пользователя");

        user.Role = dto.Role;
        await _context.SaveChangesAsync();

        return Ok($"Пользователю {user.Username} назначена роль {dto.Role}");
    }

    // ---------- рецензии ----------

    [HttpGet("reviews")]
    public async Task<ActionResult<List<AdminReviewDto>>> GetReviews([FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);

        return await _context.Reviews
            .OrderByDescending(r => r.UpdatedAt)
            .Take(take)
            .Select(r => new AdminReviewDto
            {
                Id = r.Id,
                BookId = r.BookId,
                BookTitle = r.Book.Title,
                AuthorId = r.UserId,
                AuthorUsername = r.User.Username,
                IsAnonymous = r.IsAnonymous,
                Rating = r.Rating,
                Text = r.Text,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();
    }

    [HttpDelete("reviews/{id}")]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == id);
        if (review is null)
            return NotFound("Рецензия не найдена");

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // ---------- книги ----------

    [HttpGet("books/duplicates")]
    public async Task<ActionResult<List<DuplicateBooksDto>>> GetDuplicateBooks()
    {
        return await _context.Books
            .GroupBy(b => new { Title = b.Title.ToLower(), Author = b.Author.ToLower() })
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateBooksDto
            {
                Title = g.Min(b => b.Title),
                Author = g.Min(b => b.Author),
                BookIds = g.Select(b => b.Id).ToList()
            })
            .ToListAsync();
    }

    [HttpDelete("books/{id}")]
    public async Task<IActionResult> DeleteBook(Guid id)
    {
        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null)
            return NotFound("Книга не найдена");

        var usedInPolls = await _context.PollOptions.AnyAsync(o => o.BookId == id);
        if (usedInPolls)
            return Conflict("Книга участвует в отборах клубов, удалить её нельзя");

        _context.Books.Remove(book);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}