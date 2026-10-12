using System.Text.Json;

namespace BookClub.Tests.Rest;

/// <summary>Личная полка: «хочу прочитать», «читаю», «прочитано» и оценка.</summary>
[Trait("Category", "Api")]
public class ShelfTests
{
    private static Task<Reply> Add(TestUser user, Guid bookId, string? status = null) =>
        status is null
            ? Api.Post("api/me/books", new { bookId }, user.Token)
            : Api.Post("api/me/books", new { bookId, status }, user.Token);

    private static async Task<JsonElement> ItemOf(TestUser user, Guid bookId) =>
        (await Api.Get("api/me/books", user.Token)).ShouldBe(200).Items()
            .Single(i => i.GetProperty("bookId").GetGuid() == bookId);

    [Fact(DisplayName = "Полка недоступна без входа: 401")]
    public async Task Shelf_requires_login()
    {
        (await Api.Get("api/me/books")).ShouldBe(401);
        (await Api.Post("api/me/books", new { bookId = Guid.NewGuid() })).ShouldBe(401);
        (await Api.Put("api/me/books/" + Guid.NewGuid(), new { status = "Reading" })).ShouldBe(401);
        (await Api.Delete("api/me/books/" + Guid.NewGuid())).ShouldBe(401);
    }

    [Fact(DisplayName = "Новая книга на полке по умолчанию «хочу прочитать»; в списке видны название и автор")]
    public async Task Added_book_defaults_to_want_to_read()
    {
        var user = await TestUser.Create();
        var title = "Для полки " + Make.Tag();
        var bookId = await Make.Book(user, title: title, author: "Автор полки");

        (await Add(user, bookId)).ShouldBe(201);

        var item = await ItemOf(user, bookId);
        Assert.Equal("WantToRead", item.GetProperty("status").GetString());
        Assert.Equal(title, item.GetProperty("title").GetString());
        Assert.Equal("Автор полки", item.GetProperty("author").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("rating").ValueKind);
    }

    [Theory(DisplayName = "Книгу можно сразу положить с любым из трёх статусов")]
    [InlineData("WantToRead")]
    [InlineData("Reading")]
    [InlineData("Finished")]
    public async Task Book_can_be_added_with_any_status(string status)
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);

        (await Add(user, bookId, status)).ShouldBe(201);

        Assert.Equal(status, (await ItemOf(user, bookId)).GetProperty("status").GetString());
    }

    [Fact(DisplayName = "Одну книгу на полку дважды не положить: 409")]
    public async Task Duplicate_on_shelf_is_a_conflict()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        await Add(user, bookId);

        (await Add(user, bookId)).ShouldBe(409);
    }

    [Fact(DisplayName = "Несуществующую книгу на полку не положить: 404; неизвестный статус: 400")]
    public async Task Adding_checks_book_and_status()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);

        (await Add(user, Guid.NewGuid())).ShouldBe(404);
        (await Add(user, bookId, "Dropped")).ShouldBe(400);
        (await Api.Post("api/me/books", new { }, user.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "У каждого человека своя полка: чужие книги не видны")]
    public async Task Shelves_are_private()
    {
        var first = await TestUser.Create();
        var second = await TestUser.Create();
        var bookId = await Make.Book(first);
        await Add(first, bookId);

        Assert.Empty((await Api.Get("api/me/books", second.Token)).ShouldBe(200).Items());
        Assert.Single((await Api.Get("api/me/books", first.Token)).ShouldBe(200).Items());
    }

    [Fact(DisplayName = "Фильтр по статусу работает; неверный статус в фильтре: 400")]
    public async Task Shelf_can_be_filtered_by_status()
    {
        var user = await TestUser.Create();
        var reading = await Make.Book(user);
        var finished = await Make.Book(user);
        await Add(user, reading, "Reading");
        await Add(user, finished, "Finished");

        var filtered = (await Api.Get("api/me/books?status=Finished", user.Token)).ShouldBe(200).Items();

        Assert.Equal(finished, Assert.Single(filtered).GetProperty("bookId").GetGuid());
        (await Api.Get("api/me/books?status=Nonsense", user.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Последняя добавленная книга идёт первой")]
    public async Task Newest_shelf_item_comes_first()
    {
        var user = await TestUser.Create();
        var older = await Make.Book(user);
        var newer = await Make.Book(user);
        await Add(user, older);
        await Task.Delay(50);
        await Add(user, newer);

        var list = (await Api.Get("api/me/books", user.Token)).ShouldBe(200).Items();

        Assert.Equal(newer, list[0].GetProperty("bookId").GetGuid());
        Assert.Equal(older, list[1].GetProperty("bookId").GetGuid());
    }

    [Fact(DisplayName = "Прочитанной книге можно поставить оценку")]
    public async Task Finished_book_can_be_rated()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        await Add(user, bookId);

        (await Api.Put("api/me/books/" + bookId, new { status = "Finished", rating = 5 }, user.Token)).ShouldBe(200);

        var item = await ItemOf(user, bookId);
        Assert.Equal("Finished", item.GetProperty("status").GetString());
        Assert.Equal(5, item.GetProperty("rating").GetInt32());
    }

    [Fact(DisplayName = "Оценку нельзя поставить непрочитанной книге: 400")]
    public async Task Unfinished_book_cannot_be_rated()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        await Add(user, bookId);

        (await Api.Put("api/me/books/" + bookId, new { status = "Reading", rating = 3 }, user.Token)).ShouldBe(400);
    }

    [Theory(DisplayName = "Неверные данные при обновлении полки: 400")]
    [InlineData("Finished", 6)]
    [InlineData("Finished", 0)]
    [InlineData("Dropped", null)]
    public async Task Invalid_update_is_rejected(string status, int? rating)
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        await Add(user, bookId);

        (await Api.Put("api/me/books/" + bookId, new { status, rating }, user.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Если книгу вернуть из «прочитано» в «читаю», оценка сбрасывается")]
    public async Task Moving_away_from_finished_clears_the_rating()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        await Add(user, bookId);
        await Api.Put("api/me/books/" + bookId, new { status = "Finished", rating = 4 }, user.Token);

        (await Api.Put("api/me/books/" + bookId, new { status = "Reading" }, user.Token)).ShouldBe(200);

        var item = await ItemOf(user, bookId);
        Assert.Equal("Reading", item.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("rating").ValueKind);
    }

    [Fact(DisplayName = "Обновить книгу, которой нет на полке: 404")]
    public async Task Updating_a_book_not_on_the_shelf_is_not_found()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);

        (await Api.Put("api/me/books/" + bookId, new { status = "Reading" }, user.Token)).ShouldBe(404);
    }

    [Fact(DisplayName = "Убрать книгу с полки: 204; повторно — 404")]
    public async Task Book_can_be_removed_from_the_shelf()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        await Add(user, bookId);

        (await Api.Delete("api/me/books/" + bookId, user.Token)).ShouldBe(204);
        (await Api.Delete("api/me/books/" + bookId, user.Token)).ShouldBe(404);
        Assert.Empty((await Api.Get("api/me/books", user.Token)).ShouldBe(200).Items());
    }

    [Fact(DisplayName = "Чужую книгу с полки убрать нельзя: у другого человека её нет")]
    public async Task Cannot_touch_someone_elses_shelf()
    {
        var owner = await TestUser.Create();
        var stranger = await TestUser.Create();
        var bookId = await Make.Book(owner);
        await Add(owner, bookId);

        (await Api.Delete("api/me/books/" + bookId, stranger.Token)).ShouldBe(404);
        (await Api.Put("api/me/books/" + bookId, new { status = "Finished", rating = 1 }, stranger.Token)).ShouldBe(404);

        Assert.Equal("WantToRead", (await ItemOf(owner, bookId)).GetProperty("status").GetString());
    }

    [Theory(DisplayName = "Фильтр по статусу работает для каждого из трёх статусов")]
    [InlineData("WantToRead")]
    [InlineData("Reading")]
    [InlineData("Finished")]
    public async Task Shelf_filter_works_for_every_status(string status)
    {
        var user = await TestUser.Create();
        var wanted = await Make.Book(user);
        var reading = await Make.Book(user);
        var finished = await Make.Book(user);
        await Add(user, wanted, "WantToRead");
        await Add(user, reading, "Reading");
        await Add(user, finished, "Finished");
        var expected = status switch { "WantToRead" => wanted, "Reading" => reading, _ => finished };

        var filtered = (await Api.Get("api/me/books?status=" + status, user.Token)).ShouldBe(200).Items();

        Assert.Equal(expected, Assert.Single(filtered).GetProperty("bookId").GetGuid());
    }

    [Fact(DisplayName = "Одну и ту же книгу могут положить на свои полки разные люди")]
    public async Task Different_people_can_shelve_the_same_book()
    {
        var first = await TestUser.Create();
        var second = await TestUser.Create();
        var bookId = await Make.Book(first);

        (await Add(first, bookId)).ShouldBe(201);
        (await Add(second, bookId, "Reading")).ShouldBe(201);

        Assert.Equal("WantToRead", (await ItemOf(first, bookId)).GetProperty("status").GetString());
        Assert.Equal("Reading", (await ItemOf(second, bookId)).GetProperty("status").GetString());
    }
}
