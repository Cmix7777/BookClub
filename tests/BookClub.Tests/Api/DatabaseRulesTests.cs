using Npgsql;

namespace BookClub.Tests.Rest;

/// <summary>
/// Правила, которые держит сама база данных. Даже если в коде сайта появится ошибка или кто-то зайдёт в базу
/// напрямую, нарушить эти правила не получится. Здесь мы специально пытаемся их нарушить.
/// </summary>
[Trait("Category", "Api")]
public class DatabaseRulesTests
{
    private static async Task ShouldFailWith(string sqlState, string sql, params (string Name, object Value)[] parameters)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => Db.Exec(sql, parameters));
        Assert.Equal(sqlState, error.SqlState);
    }

    private const string InsertUser =
        "INSERT INTO \"Users\" (\"Id\", \"CreatedAt\", \"Email\", \"IsBlocked\", \"PasswordHash\", \"Role\", \"Username\") " +
        "VALUES (@id, now(), @email, false, 'x', 'User', @name)";

    [Fact(DisplayName = "База не даст завести двух пользователей с одним логином")]
    public async Task Username_is_unique_in_the_database()
    {
        var user = await TestUser.Create();

        await ShouldFailWith(PostgresErrorCodes.UniqueViolation, InsertUser,
            ("id", Guid.NewGuid()), ("email", "x" + Make.Tag() + "@example.test"), ("name", user.Username));
    }

    [Fact(DisplayName = "База не даст завести двух пользователей с одной почтой")]
    public async Task Email_is_unique_in_the_database()
    {
        var user = await TestUser.Create();

        await ShouldFailWith(PostgresErrorCodes.UniqueViolation, InsertUser,
            ("id", Guid.NewGuid()), ("email", user.Email), ("name", "x" + Make.Tag()));
    }

    [Fact(DisplayName = "База не принимает выдуманную роль пользователя")]
    public async Task Role_is_limited_in_the_database()
    {
        var user = await TestUser.Create();

        await ShouldFailWith(PostgresErrorCodes.CheckViolation,
            "UPDATE \"Users\" SET \"Role\" = 'Root' WHERE \"Id\" = @id", ("id", user.Id));
    }

    [Fact(DisplayName = "База не принимает оценку рецензии вне диапазона 1-5")]
    public async Task Review_rating_is_limited_in_the_database()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        (await Api.Put($"api/books/{bookId}/reviews/mine", new { rating = 3 }, user.Token)).ShouldBe(201);

        await ShouldFailWith(PostgresErrorCodes.CheckViolation,
            "UPDATE \"Reviews\" SET \"Rating\" = 9 WHERE \"BookId\" = @id", ("id", bookId));
    }

    [Fact(DisplayName = "База не принимает выдуманный статус книги на полке и оценку вне диапазона")]
    public async Task Shelf_values_are_limited_in_the_database()
    {
        var user = await TestUser.Create();
        var bookId = await Make.Book(user);
        (await Api.Post("api/me/books", new { bookId }, user.Token)).ShouldBe(201);

        await ShouldFailWith(PostgresErrorCodes.CheckViolation,
            "UPDATE \"UserBooks\" SET \"Status\" = 'Dropped' WHERE \"BookId\" = @id AND \"UserId\" = @user",
            ("id", bookId), ("user", user.Id));
        await ShouldFailWith(PostgresErrorCodes.CheckViolation,
            "UPDATE \"UserBooks\" SET \"Rating\" = 7 WHERE \"BookId\" = @id AND \"UserId\" = @user",
            ("id", bookId), ("user", user.Id));
    }

    [Fact(DisplayName = "База не запишет второй голос одного человека в одном отборе, даже в обход сайта")]
    public async Task Second_vote_check_is_rejected_by_the_database()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));
        await Make.Vote(owner, poll, 0);

        await ShouldFailWith(PostgresErrorCodes.UniqueViolation,
            "INSERT INTO \"PollVoteChecks\" (\"Id\", \"PollId\", \"UserId\", \"VotedAt\") VALUES (@id, @poll, @user, now())",
            ("id", Guid.NewGuid()), ("poll", poll.Id), ("user", owner.Id));
    }

    [Fact(DisplayName = "База не даст одному человеку дважды вступить в один клуб")]
    public async Task Membership_is_unique_in_the_database()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);

        await ShouldFailWith(PostgresErrorCodes.UniqueViolation,
            "INSERT INTO \"ClubMembers\" (\"Id\", \"ClubId\", \"UserId\", \"Role\", \"JoinedAt\") VALUES (@id, @club, @user, 'Member', now())",
            ("id", Guid.NewGuid()), ("club", clubId), ("user", owner.Id));
    }

    [Fact(DisplayName = "Нельзя удалить из базы человека, который владеет клубом")]
    public async Task Club_owner_cannot_be_deleted()
    {
        var owner = await TestUser.Create();
        await Make.Club(owner);

        await ShouldFailWith(PostgresErrorCodes.ForeignKeyViolation,
            "DELETE FROM \"Users\" WHERE \"Id\" = @id", ("id", owner.Id));
    }
}
