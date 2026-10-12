using System.Text.Json;

namespace BookClub.Tests.Rest;

/// <summary>Рецензии на книги, средняя оценка и анонимность.</summary>
[Trait("Category", "Api")]
public class ReviewsTests
{
    private static Task<Reply> Write(TestUser user, Guid bookId, int rating, string text = "", bool anonymous = false) =>
        Api.Put($"api/books/{bookId}/reviews/mine", new { rating, text, isAnonymous = anonymous }, user.Token);

    [Fact(DisplayName = "У книги без рецензий: счётчик 0, средней оценки нет; список открыт без входа")]
    public async Task Book_without_reviews_is_empty()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);

        var reply = (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200);

        Assert.Equal(0, reply.Int("count"));
        Assert.Equal(JsonValueKind.Null, reply.Json.GetProperty("averageRating").ValueKind);
        Assert.Empty(reply.Json.GetProperty("reviews").EnumerateArray());
    }

    [Fact(DisplayName = "Рецензии несуществующей книги: 404")]
    public async Task Reviews_of_unknown_book_are_not_found()
    {
        (await Api.Get($"api/books/{Guid.NewGuid()}/reviews")).ShouldBe(404);
    }

    [Fact(DisplayName = "Написать рецензию без входа нельзя: 401")]
    public async Task Writing_a_review_requires_login()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);

        (await Api.Put($"api/books/{bookId}/reviews/mine", new { rating = 5 })).ShouldBe(401);
    }

    [Fact(DisplayName = "Первая рецензия: 201 с именем автора и пометкой «моя»; она видна в списке")]
    public async Task First_review_is_created()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);

        var created = (await Write(user, bookId, 4, "  Хорошо  ")).ShouldBe(201);

        Assert.Equal(4, created.Int("rating"));
        Assert.Equal("Хорошо", created.Str("text"));
        Assert.Equal(user.Username, created.Str("author"));
        Assert.True(created.Bool("isMine"));
        Assert.False(created.Bool("isAnonymous"));

        var list = (await Api.Get($"api/books/{bookId}/reviews", user.Token)).ShouldBe(200);
        Assert.Equal(1, list.Int("count"));
        Assert.Equal(4.0, list.Json.GetProperty("averageRating").GetDouble());
    }

    [Fact(DisplayName = "Повторная рецензия той же книги обновляет прежнюю: 200, запись одна")]
    public async Task Second_write_updates_the_same_review()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        var first = (await Write(user, bookId, 2, "Не очень")).ShouldBe(201);

        var second = (await Write(user, bookId, 5, "Передумал")).ShouldBe(200);

        Assert.Equal(first.GuidOf("id"), second.GuidOf("id"));
        var list = (await Api.Get($"api/books/{bookId}/reviews", user.Token)).ShouldBe(200);
        Assert.Equal(1, list.Int("count"));
        var only = list.Json.GetProperty("reviews").EnumerateArray().Single();
        Assert.Equal(5, only.GetProperty("rating").GetInt32());
        Assert.Equal("Передумал", only.GetProperty("text").GetString());
    }

    [Theory(DisplayName = "Оценка вне диапазона 1-5 отклоняется: 400")]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Rating_out_of_range_is_rejected(int rating)
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);

        (await Write(user, bookId, rating)).ShouldBe(400);
    }

    [Fact(DisplayName = "Без оценки рецензия не принимается: 400")]
    public async Task Rating_is_required()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);

        (await Api.Put($"api/books/{bookId}/reviews/mine", new { text = "Только текст" }, user.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Текст можно не писать; текст длиннее 2000 знаков отклоняется")]
    public async Task Text_is_optional_but_limited()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);

        (await Write(user, bookId, 3, new string('т', 2001))).ShouldBe(400);
        var created = (await Api.Put($"api/books/{bookId}/reviews/mine", new { rating = 3 }, user.Token)).ShouldBe(201);
        Assert.Equal("", created.Str("text"));
    }

    [Fact(DisplayName = "Рецензия на несуществующую книгу: 404")]
    public async Task Review_of_unknown_book_is_not_found()
    {
        var user = await TestUser.Create();

        (await Write(user, Guid.NewGuid(), 5)).ShouldBe(404);
    }

    [Fact(DisplayName = "Средняя оценка: 5 и 4 — это 4,5; 5, 4 и 4 — это 4,3")]
    public async Task Average_rating_is_rounded_to_one_decimal()
    {
        var first = await TestUser.Create();
        var second = await TestUser.Create();
        var third = await TestUser.Create();
        var bookId = await Make.Book(first);

        await Write(first, bookId, 5);
        await Write(second, bookId, 4);
        var two = (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200);
        Assert.Equal(4.5, two.Json.GetProperty("averageRating").GetDouble());

        await Write(third, bookId, 4);
        var three = (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200);
        Assert.Equal(3, three.Int("count"));
        Assert.Equal(4.3, three.Json.GetProperty("averageRating").GetDouble());
    }

    [Fact(DisplayName = "Анонимная рецензия: для других автора не видно ни по имени, ни по номеру")]
    public async Task Anonymous_review_hides_the_author()
    {
        var author = await TestUser.Create();
        var reader = await TestUser.Create();
        var bookId = await Make.Book(author);
        await Write(author, bookId, 1, "Совсем не понравилось", anonymous: true);

        var seenByReader = (await Api.Get($"api/books/{bookId}/reviews", reader.Token)).ShouldBe(200);
        var review = seenByReader.Json.GetProperty("reviews").EnumerateArray().Single();

        Assert.True(review.GetProperty("isAnonymous").GetBoolean());
        Assert.Equal(JsonValueKind.Null, review.GetProperty("author").ValueKind);
        Assert.False(review.GetProperty("isMine").GetBoolean());
        Assert.DoesNotContain(author.Username, seenByReader.Body);
        Assert.DoesNotContain(author.Id.ToString(), seenByReader.Body, StringComparison.OrdinalIgnoreCase);

        var seenByGuest = (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200);
        Assert.DoesNotContain(author.Username, seenByGuest.Body);
    }

    [Fact(DisplayName = "Свою анонимную рецензию автор узнаёт по пометке «моя»")]
    public async Task Author_recognises_own_anonymous_review()
    {
        var author = await TestUser.Create();
        var bookId = await Make.Book(author);
        await Write(author, bookId, 2, anonymous: true);

        var list = (await Api.Get($"api/books/{bookId}/reviews", author.Token)).ShouldBe(200);

        Assert.True(list.Json.GetProperty("reviews").EnumerateArray().Single().GetProperty("isMine").GetBoolean());
    }

    [Fact(DisplayName = "Подпись можно вернуть: после снятия анонимности имя автора снова видно")]
    public async Task Anonymity_can_be_switched_off()
    {
        var author = await TestUser.Create();
        var bookId = await Make.Book(author);
        await Write(author, bookId, 3, anonymous: true);
        await Write(author, bookId, 3, anonymous: false);

        var list = (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200);

        Assert.Equal(author.Username, list.Json.GetProperty("reviews").EnumerateArray().Single().GetProperty("author").GetString());
    }

    [Fact(DisplayName = "Без входа пометка «моя» нигде не стоит")]
    public async Task Guests_see_no_mine_flag()
    {
        var author = await TestUser.Create();
        var bookId = await Make.Book(author);
        await Write(author, bookId, 5);

        var list = (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200);

        Assert.All(list.Json.GetProperty("reviews").EnumerateArray(), r => Assert.False(r.GetProperty("isMine").GetBoolean()));
    }

    [Fact(DisplayName = "Свежие и обновлённые рецензии идут первыми")]
    public async Task Reviews_are_ordered_by_last_update()
    {
        var first = await TestUser.Create();
        var second = await TestUser.Create();
        var bookId = await Make.Book(first);
        await Write(first, bookId, 3, "первая");
        await Task.Delay(50);
        await Write(second, bookId, 4, "вторая");

        var before = (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200);
        Assert.Equal("вторая", before.Json.GetProperty("reviews").EnumerateArray().First().GetProperty("text").GetString());

        await Task.Delay(50);
        await Write(first, bookId, 5, "первая, исправленная");

        var after = (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200);
        Assert.Equal("первая, исправленная", after.Json.GetProperty("reviews").EnumerateArray().First().GetProperty("text").GetString());
    }

    [Fact(DisplayName = "Свою рецензию можно удалить: 204; повторно — 404; без входа — 401")]
    public async Task Review_can_be_deleted()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        await Write(user, bookId, 4);

        (await Api.Delete($"api/books/{bookId}/reviews/mine")).ShouldBe(401);
        (await Api.Delete($"api/books/{bookId}/reviews/mine", user.Token)).ShouldBe(204);
        (await Api.Delete($"api/books/{bookId}/reviews/mine", user.Token)).ShouldBe(404);

        Assert.Equal(0, (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200).Int("count"));
    }

    [Fact(DisplayName = "Чужую рецензию удалить через «mine» нельзя: у другого человека её просто нет")]
    public async Task Cannot_delete_someone_elses_review()
    {
        var author = await TestUser.Create();
        var stranger = await TestUser.Create();
        var bookId = await Make.Book(author);
        await Write(author, bookId, 4);

        (await Api.Delete($"api/books/{bookId}/reviews/mine", stranger.Token)).ShouldBe(404);

        Assert.Equal(1, (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200).Int("count"));
    }

    [Fact(DisplayName = "Ответ на отправку анонимной рецензии не выдаёт автора, а с подписью имя автора в ответе есть")]
    public async Task Reply_to_writing_follows_anonymity()
    {
        var author = await TestUser.Create();
        var bookId = await Make.Book(author);

        var anonymous = (await Write(author, bookId, 4, "Без подписи", anonymous: true)).ShouldBe(201);
        Assert.Equal(JsonValueKind.Null, anonymous.Json.GetProperty("author").ValueKind);
        Assert.DoesNotContain(author.Username, anonymous.Body);

        var signed = (await Write(author, bookId, 4, "С подписью", anonymous: false)).ShouldBe(200);
        Assert.Equal(author.Username, signed.Str("author"));
    }

    [Fact(DisplayName = "Двадцать одновременных рецензий одного человека на одну книгу, пять раз подряд: рецензия одна, ни одной ошибки сервера")]
    public async Task Parallel_reviews_of_one_user_keep_a_single_review()
    {
        var user = await TestUser.Create();

        // гонка случается не при каждой попытке, поэтому бросаем пять пачек запросов, каждая про новую книгу
        for (var round = 0; round < 5; round++)
        {
            var bookId = await Make.Book(user);

            var replies = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Write(user, bookId, 1 + i % 5)));

            Assert.Equal(1, replies.Count(r => r.Code == 201));
            // остальные либо обновили готовую рецензию (200), либо проиграли гонку и просят повторить (409)
            Assert.All(replies.Where(r => r.Code != 201), r => Assert.Contains(r.Code, new[] { 200, 409 }));
            Assert.Equal(1, (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200).Int("count"));
        }
    }
}
