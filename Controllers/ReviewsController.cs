using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookClub.Data;
using BookClub.Dtos;
using BookClub.Extensions;
using BookClub.Models;

namespace BookClub.Controllers;

[ApiController]
[Route("api/books/{bookId}/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReviewsController(AppDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<BookReviewsDto>> GetReviews(Guid bookId)
    {
        var bookExists = await _context.Books.AnyAsync(b => b.Id == bookId);
        if (!bookExists)
            return NotFound("Книга не найдена");

        Guid? currentUserId = User.Identity?.IsAuthenticated == true
            ? User.GetUserId()
            : null;

        var reviews = await _context.Reviews
            .Where(r => r.BookId == bookId)
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                Rating = r.Rating,
                Text = r.Text,
                Author = r.IsAnonymous ? null : r.User.Username,
                IsAnonymous = r.IsAnonymous,
                IsMine = currentUserId != null && r.UserId == currentUserId,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();

        return new BookReviewsDto
        {
            BookId = bookId,
            Count = reviews.Count,
            AverageRating = reviews.Count == 0
                ? null
                : Math.Round(reviews.Average(r => r.Rating), 1),
            Reviews = reviews
        };
    }

    [HttpPut("mine")]
    public async Task<ActionResult<ReviewDto>> UpsertMyReview(Guid bookId, UpsertReviewDto dto)
    {
        var userId = User.GetUserId();

        var bookExists = await _context.Books.AnyAsync(b => b.Id == bookId);
        if (!bookExists)
            return NotFound("Книга не найдена");

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);

        var isNew = review is null;
        if (review is null)
        {
            review = new Review
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                UserId = userId
            };
            _context.Reviews.Add(review);
        }

        review.Rating = dto.Rating!.Value;
        review.Text = dto.Text.Trim();
        review.IsAnonymous = dto.IsAnonymous;
        review.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("Рецензия уже создаётся, повторите запрос");
        }

        var result = new ReviewDto
        {
            Id = review.Id,
            Rating = review.Rating,
            Text = review.Text,
            Author = review.IsAnonymous ? null : User.GetUsername(),
            IsAnonymous = review.IsAnonymous,
            IsMine = true,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };

        return isNew
            ? CreatedAtAction(nameof(GetReviews), new { bookId }, result)
            : Ok(result);
    }

    [HttpDelete("mine")]
    public async Task<IActionResult> DeleteMyReview(Guid bookId)
    {
        var userId = User.GetUserId();

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);

        if (review is null)
            return NotFound("У вас нет рецензии на эту книгу");

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}