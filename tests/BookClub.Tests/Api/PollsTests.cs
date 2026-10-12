namespace BookClub.Tests.Rest;

/// <summary>Создание отбора («конверта») и его просмотр.</summary>
[Trait("Category", "Api")]
public class PollsTests
{
    [Fact(DisplayName = "Участник создаёт отбор: 201, два варианта с названиями книг, срок через час, голосов нет")]
    public async Task Member_creates_a_poll()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var titleA = "Первая " + Make.Tag();
        var titleB = "Вторая " + Make.Tag();
        var a = await Make.Book(owner, title: titleA, author: "Автор А");
        var b = await Make.Book(owner, title: titleB, author: "Автор Б");

        var reply = (await Api.Post("api/polls", new
        {
            clubId,
            question = "Что читаем дальше?",
            bookIds = new[] { a, b },
            durationMinutes = 60
        }, owner.Token)).ShouldBe(201);

        Assert.EndsWith("/api/polls/" + reply.GuidOf("id"), reply.Location);
        Assert.Equal("Что читаем дальше?", reply.Str("question"));
        Assert.False(reply.Bool("isClosed"));
        Assert.Equal(0, reply.Int("votersCount"));
        Assert.False(reply.Bool("hasVoted"));
        Assert.InRange(reply.Json.GetProperty("closesAt").GetDateTime().ToUniversalTime(),
            DateTime.UtcNow.AddMinutes(58), DateTime.UtcNow.AddMinutes(62));

        var options = reply.Json.GetProperty("options").EnumerateArray().ToList();
        Assert.Equal(2, options.Count);
        Assert.Contains(options, o => o.GetProperty("title").GetString() == titleA && o.GetProperty("author").GetString() == "Автор А");
        Assert.Contains(options, o => o.GetProperty("title").GetString() == titleB && o.GetProperty("author").GetString() == "Автор Б");
    }

    [Fact(DisplayName = "Срок голосования по умолчанию — 48 часов")]
    public async Task Default_duration_is_48_hours()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var a = await Make.Book(owner);
        var b = await Make.Book(owner);

        var reply = (await Api.Post("api/polls",
            new { clubId, question = "Без срока", bookIds = new[] { a, b } }, owner.Token)).ShouldBe(201);

        Assert.InRange(reply.Json.GetProperty("closesAt").GetDateTime().ToUniversalTime(),
            DateTime.UtcNow.AddHours(47), DateTime.UtcNow.AddHours(49));
    }

    [Fact(DisplayName = "Отбор можно открыть по номеру; без входа — 401, чужой номер — 404")]
    public async Task Poll_can_be_fetched_by_logged_in_users_only()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));

        var fetched = (await Api.Get("api/polls/" + poll.Id, owner.Token)).ShouldBe(200);
        Assert.Equal(poll.Id, fetched.GuidOf("id"));
        Assert.Equal(poll.ClubId, fetched.GuidOf("clubId"));

        (await Api.Get("api/polls/" + poll.Id)).ShouldBe(401);
        (await Api.Get("api/polls/" + Guid.NewGuid(), owner.Token)).ShouldBe(404);
    }

    [Fact(DisplayName = "Создать отбор без входа нельзя: 401")]
    public async Task Creating_a_poll_requires_login()
    {
        (await Api.Post("api/polls", new { clubId = Guid.NewGuid(), question = "Без входа?", bookIds = new[] { Guid.NewGuid(), Guid.NewGuid() } }))
            .ShouldBe(401);
    }

    [Fact(DisplayName = "Не участник клуба не может создать отбор: 403")]
    public async Task Outsider_cannot_create_a_poll()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var outsider = await TestUser.Create();
        var a = await Make.Book(outsider);
        var b = await Make.Book(outsider);

        (await Api.Post("api/polls", new { clubId, question = "Чужой клуб", bookIds = new[] { a, b } }, outsider.Token))
            .ShouldBe(403);
    }

    [Fact(DisplayName = "Отбор в несуществующем клубе: 404")]
    public async Task Poll_in_unknown_club_is_not_found()
    {
        var user = await TestUser.Create();
        var a = await Make.Book(user);
        var b = await Make.Book(user);

        (await Api.Post("api/polls", new { clubId = Guid.NewGuid(), question = "Нет клуба", bookIds = new[] { a, b } }, user.Token))
            .ShouldBe(404);
    }

    [Fact(DisplayName = "Пока идёт один отбор, второй в том же клубе создать нельзя: 409")]
    public async Task Only_one_active_poll_per_club()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        await Make.Poll(owner, clubId);
        var a = await Make.Book(owner);
        var b = await Make.Book(owner);

        (await Api.Post("api/polls", new { clubId, question = "Второй сразу", bookIds = new[] { a, b } }, owner.Token))
            .ShouldBe(409);
    }

    [Fact(DisplayName = "После закрытия отбора можно создать новый")]
    public async Task New_poll_is_possible_after_closing()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var first = await Make.Poll(owner, clubId);
        await Make.Close(owner, first);

        await Make.Poll(owner, clubId);
    }

    [Fact(DisplayName = "Отбор, у которого вышло время, не мешает создать новый")]
    public async Task Expired_poll_does_not_block_a_new_one()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var first = await Make.Poll(owner, clubId);
        await Db.Exec("UPDATE \"Polls\" SET \"ClosesAt\" = now() - interval '1 minute' WHERE \"Id\" = @id", ("id", first.Id));

        await Make.Poll(owner, clubId);
    }

    [Fact(DisplayName = "Одна и та же книга дважды — это не два варианта: 400")]
    public async Task Same_book_twice_is_not_enough()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var book = await Make.Book(owner);

        (await Api.Post("api/polls", new { clubId, question = "Одна книга", bookIds = new[] { book, book } }, owner.Token))
            .ShouldBe(400);
    }

    [Fact(DisplayName = "Книга, которой нет в каталоге, отклоняется: 400")]
    public async Task Unknown_book_in_poll_is_rejected()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var book = await Make.Book(owner);

        (await Api.Post("api/polls", new { clubId, question = "Призрак", bookIds = new[] { book, Guid.NewGuid() } }, owner.Token))
            .ShouldBe(400);
    }

    [Theory(DisplayName = "Неверное число книг в отборе: 400")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    public async Task Wrong_number_of_books_is_rejected(int count)
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
            ids.Add(i == 0 ? await Make.Book(owner) : Guid.NewGuid());

        (await Api.Post("api/polls", new { clubId, question = "Сколько книг?", bookIds = ids }, owner.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Ровно десять книг в отборе — допустимо")]
    public async Task Ten_books_are_allowed()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);

        var poll = await Make.Poll(owner, clubId, books: 10);

        Assert.Equal(10, poll.OptionIds.Count);
    }

    [Theory(DisplayName = "Неверный вопрос отбора: 400")]
    [InlineData("")]
    [InlineData("ab")]
    public async Task Invalid_question_is_rejected(string question)
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var a = await Make.Book(owner);
        var b = await Make.Book(owner);

        (await Api.Post("api/polls", new { clubId, question, bookIds = new[] { a, b } }, owner.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Вопрос длиннее 150 знаков отклоняется")]
    public async Task Too_long_question_is_rejected()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var a = await Make.Book(owner);
        var b = await Make.Book(owner);

        (await Api.Post("api/polls", new { clubId, question = new string('в', 151), bookIds = new[] { a, b } }, owner.Token))
            .ShouldBe(400);
    }

    [Theory(DisplayName = "Срок голосования вне диапазона 1 минута — 7 суток: 400")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10081)]
    public async Task Duration_out_of_range_is_rejected(int minutes)
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var a = await Make.Book(owner);
        var b = await Make.Book(owner);

        (await Api.Post("api/polls", new { clubId, question = "Срок?", bookIds = new[] { a, b }, durationMinutes = minutes }, owner.Token))
            .ShouldBe(400);
    }

    [Theory(DisplayName = "Крайние допустимые сроки принимаются: 1 минута и 7 суток")]
    [InlineData(1)]
    [InlineData(10080)]
    public async Task Duration_limits_are_accepted(int minutes)
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);

        await Make.Poll(owner, clubId, minutes: minutes);
    }

    [Fact(DisplayName = "Список отборов клуба: новые первыми, у закрытых и у просроченных isClosed = true")]
    public async Task Club_poll_list_shows_newest_first_with_closed_flag()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var older = await Make.Poll(owner, clubId);
        await Make.Close(owner, older);
        await Task.Delay(50);
        var newer = await Make.Poll(owner, clubId);

        var list = (await Api.Get($"api/clubs/{clubId}/polls", owner.Token)).ShouldBe(200).Items();

        Assert.Equal(2, list.Count);
        Assert.Equal(newer.Id, list[0].GetProperty("id").GetGuid());
        Assert.False(list[0].GetProperty("isClosed").GetBoolean());
        Assert.Equal(older.Id, list[1].GetProperty("id").GetGuid());
        Assert.True(list[1].GetProperty("isClosed").GetBoolean());
    }
}
