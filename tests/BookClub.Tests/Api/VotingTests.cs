using System.Text.Json;

namespace BookClub.Tests.Rest;

/// <summary>Голосование: один человек — один голос, запечатанный конверт, закрытие, подсчёт и тайна голоса.</summary>
[Trait("Category", "Api")]
public class VotingTests
{
    private static Task<Reply> Vote(TestUser user, PollInfo poll, int optionIndex) =>
        Api.Post($"api/polls/{poll.Id}/vote", new { pollOptionId = poll.OptionIds[optionIndex] }, user.Token);

    private static Task<Reply> Results(TestUser user, PollInfo poll) =>
        Api.Get($"api/polls/{poll.Id}/results", user.Token);

    private static JsonElement ResultOf(Reply results, Guid optionId) =>
        results.Json.GetProperty("results").EnumerateArray()
            .Single(r => r.GetProperty("pollOptionId").GetGuid() == optionId);

    private static Task Expire(PollInfo poll) =>
        Db.Exec("UPDATE \"Polls\" SET \"ClosesAt\" = now() - interval '1 minute' WHERE \"Id\" = @id", ("id", poll.Id));

    // ---------- голос ----------

    [Fact(DisplayName = "Участник голосует: 200, число проголосовавших растёт, hasVoted только у него")]
    public async Task Member_can_vote_once()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(1);
        var other = members[0];
        var poll = await Make.Poll(owner, clubId);

        (await Vote(owner, poll, 0)).ShouldBe(200);

        var mine = (await Api.Get("api/polls/" + poll.Id, owner.Token)).ShouldBe(200);
        Assert.Equal(1, mine.Int("votersCount"));
        Assert.True(mine.Bool("hasVoted"));

        var theirs = (await Api.Get("api/polls/" + poll.Id, other.Token)).ShouldBe(200);
        Assert.Equal(1, theirs.Int("votersCount"));
        Assert.False(theirs.Bool("hasVoted"));
    }

    [Fact(DisplayName = "Второй голос в том же отборе: 409, счётчик не меняется")]
    public async Task Second_vote_is_rejected()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));
        await Make.Vote(owner, poll, 0);

        (await Vote(owner, poll, 1)).ShouldBe(409);
        (await Vote(owner, poll, 0)).ShouldBe(409);

        var state = (await Api.Get("api/polls/" + poll.Id, owner.Token)).ShouldBe(200);
        Assert.Equal(1, state.Int("votersCount"));
    }

    [Fact(DisplayName = "Один человек голосует в двух разных отборах: «уже голосовал» считается отдельно по каждому отбору")]
    public async Task Same_user_can_vote_in_two_different_polls()
    {
        var firstOwner = await TestUser.Create();
        var firstClub = await Make.Club(firstOwner);
        var firstPoll = await Make.Poll(firstOwner, firstClub);

        var secondOwner = await TestUser.Create();
        var secondClub = await Make.Club(secondOwner);
        var secondPoll = await Make.Poll(secondOwner, secondClub);

        var guest = await TestUser.Create();
        await Make.Join(guest, firstClub);
        await Make.Join(guest, secondClub);

        (await Vote(guest, firstPoll, 0)).ShouldBe(200);
        (await Vote(guest, secondPoll, 1)).ShouldBe(200);

        // каждый голос лёг в свой отбор, а повторный голос в любом из них по-прежнему отклоняется
        Assert.Equal(1, (await Api.Get("api/polls/" + firstPoll.Id, guest.Token)).ShouldBe(200).Int("votersCount"));
        Assert.Equal(1, (await Api.Get("api/polls/" + secondPoll.Id, guest.Token)).ShouldBe(200).Int("votersCount"));
        (await Vote(guest, firstPoll, 1)).ShouldBe(409);
        (await Vote(guest, secondPoll, 0)).ShouldBe(409);
    }

    [Fact(DisplayName = "Тридцать одновременных голосов одного человека, пять раз подряд: засчитан один за раз, остальные 409, ни одной ошибки сервера")]
    public async Task Parallel_votes_of_one_user_count_once()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var poll = await Make.Poll(owner, clubId);

        // гонка случается не при каждой попытке, поэтому бросаем пять пачек запросов, каждая от нового человека
        for (var round = 0; round < 5; round++)
        {
            var voter = await TestUser.Create();
            await Make.Join(voter, clubId);

            var replies = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Vote(voter, poll, 0)));

            Assert.Equal(1, replies.Count(r => r.Code == 200));
            Assert.All(replies.Where(r => r.Code != 200), r => Assert.Equal(409, r.Code));
        }

        Assert.Equal(5, (await Api.Get("api/polls/" + poll.Id, owner.Token)).ShouldBe(200).Int("votersCount"));
        await Make.Close(owner, poll);
        Assert.Equal(5, (await Results(owner, poll)).ShouldBe(200).Int("totalVotes"));
    }

    [Fact(DisplayName = "Не участник клуба голосовать не может: 403")]
    public async Task Outsider_cannot_vote()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));
        var outsider = await TestUser.Create();

        (await Vote(outsider, poll, 0)).ShouldBe(403);
    }

    [Fact(DisplayName = "Голос без входа: 401")]
    public async Task Voting_requires_login()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));

        (await Api.Post($"api/polls/{poll.Id}/vote", new { pollOptionId = poll.OptionIds[0] })).ShouldBe(401);
    }

    [Fact(DisplayName = "Вариант из другого отбора не принимается: 400")]
    public async Task Option_of_another_poll_is_rejected()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));
        var otherOwner = await TestUser.Create();
        var otherPoll = await Make.Poll(otherOwner, await Make.Club(otherOwner));

        (await Api.Post($"api/polls/{poll.Id}/vote", new { pollOptionId = otherPoll.OptionIds[0] }, owner.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Голос в несуществующем отборе: 404; без выбранного варианта: 400")]
    public async Task Vote_needs_an_existing_poll_and_an_option()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));

        (await Api.Post($"api/polls/{Guid.NewGuid()}/vote", new { pollOptionId = poll.OptionIds[0] }, owner.Token)).ShouldBe(404);
        (await Api.Post($"api/polls/{poll.Id}/vote", new { }, owner.Token)).ShouldBe(400);
    }

    // ---------- запечатанный конверт ----------

    [Fact(DisplayName = "Пока отбор идёт, сервер не отдаёт распределение голосов: ни участнику, ни админу клуба")]
    public async Task Results_are_sealed_while_poll_is_open()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(2);
        var poll = await Make.Poll(owner, clubId);
        await Make.Vote(owner, poll, 0);
        await Make.Vote(members[0], poll, 0);
        await Make.Vote(members[1], poll, 1);

        foreach (var viewer in new[] { owner, members[0] })
        {
            var results = (await Results(viewer, poll)).ShouldBe(200);

            Assert.False(results.Bool("isClosed"));
            Assert.Equal(3, results.Int("votersCount"));
            Assert.Equal(3, results.Int("membersCount"));
            Assert.Equal(JsonValueKind.Null, results.Json.GetProperty("results").ValueKind);
            Assert.Equal(JsonValueKind.Null, results.Json.GetProperty("totalVotes").ValueKind);
            Assert.DoesNotContain("voteCount", results.Body);
            Assert.DoesNotContain("percentage", results.Body);
        }

        var pollBody = (await Api.Get("api/polls/" + poll.Id, owner.Token)).ShouldBe(200).Body;
        Assert.DoesNotContain("voteCount", pollBody);
    }

    [Fact(DisplayName = "После закрытия видно, кто победил: 2 голоса против 1 — 66,7% и 33,3%")]
    public async Task Closed_poll_shows_counts_percentages_and_winner()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(2);
        var poll = await Make.Poll(owner, clubId);
        await Make.Vote(owner, poll, 0);
        await Make.Vote(members[0], poll, 0);
        await Make.Vote(members[1], poll, 1);
        await Make.Close(owner, poll);

        var results = (await Results(members[1], poll)).ShouldBe(200);

        Assert.True(results.Bool("isClosed"));
        Assert.Equal(3, results.Int("totalVotes"));

        var winner = ResultOf(results, poll.OptionIds[0]);
        Assert.Equal(2, winner.GetProperty("voteCount").GetInt32());
        Assert.Equal(66.7, winner.GetProperty("percentage").GetDouble(), 1);
        Assert.True(winner.GetProperty("isWinner").GetBoolean());

        var loser = ResultOf(results, poll.OptionIds[1]);
        Assert.Equal(1, loser.GetProperty("voteCount").GetInt32());
        Assert.Equal(33.3, loser.GetProperty("percentage").GetDouble(), 1);
        Assert.False(loser.GetProperty("isWinner").GetBoolean());

        var first = results.Json.GetProperty("results").EnumerateArray().First();
        Assert.Equal(poll.OptionIds[0], first.GetProperty("pollOptionId").GetGuid());
    }

    [Fact(DisplayName = "Ничья: победителями считаются оба варианта")]
    public async Task Tie_marks_both_options_as_winners()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(1);
        var poll = await Make.Poll(owner, clubId);
        await Make.Vote(owner, poll, 0);
        await Make.Vote(members[0], poll, 1);
        await Make.Close(owner, poll);

        var results = (await Results(owner, poll)).ShouldBe(200);

        Assert.True(ResultOf(results, poll.OptionIds[0]).GetProperty("isWinner").GetBoolean());
        Assert.True(ResultOf(results, poll.OptionIds[1]).GetProperty("isWinner").GetBoolean());
        Assert.Equal(50.0, ResultOf(results, poll.OptionIds[0]).GetProperty("percentage").GetDouble(), 1);
    }

    [Fact(DisplayName = "Отбор закрыт без голосов: всё по нулям, победителя нет, деления на ноль нет")]
    public async Task Poll_without_votes_has_no_winner()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));
        await Make.Close(owner, poll);

        var results = (await Results(owner, poll)).ShouldBe(200);

        Assert.Equal(0, results.Int("totalVotes"));
        foreach (var option in results.Json.GetProperty("results").EnumerateArray())
        {
            Assert.Equal(0, option.GetProperty("voteCount").GetInt32());
            Assert.Equal(0.0, option.GetProperty("percentage").GetDouble());
            Assert.False(option.GetProperty("isWinner").GetBoolean());
        }
    }

    [Fact(DisplayName = "Результаты без входа не отдаются: 401; несуществующего отбора: 404")]
    public async Task Results_need_login_and_an_existing_poll()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));

        (await Api.Get($"api/polls/{poll.Id}/results")).ShouldBe(401);
        (await Api.Get($"api/polls/{Guid.NewGuid()}/results", owner.Token)).ShouldBe(404);
    }

    // ---------- закрытие ----------

    [Fact(DisplayName = "Админ клуба закрывает отбор досрочно: 200, голосовать больше нельзя")]
    public async Task Club_admin_can_close_the_poll()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(1);
        var poll = await Make.Poll(owner, clubId);

        (await Api.Post($"api/polls/{poll.Id}/close", null, owner.Token)).ShouldBe(200);

        Assert.True((await Api.Get("api/polls/" + poll.Id, owner.Token)).ShouldBe(200).Bool("isClosed"));
        (await Vote(members[0], poll, 0)).ShouldBe(400);
    }

    [Fact(DisplayName = "Закрыть отбор не может обычный участник и посторонний: 403")]
    public async Task Only_club_admin_can_close()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(1);
        var poll = await Make.Poll(owner, clubId);
        var outsider = await TestUser.Create();

        (await Api.Post($"api/polls/{poll.Id}/close", null, members[0].Token)).ShouldBe(403);
        (await Api.Post($"api/polls/{poll.Id}/close", null, outsider.Token)).ShouldBe(403);
        Assert.False((await Api.Get("api/polls/" + poll.Id, owner.Token)).ShouldBe(200).Bool("isClosed"));
    }

    [Fact(DisplayName = "Закрытие без входа: 401; несуществующего отбора: 404; повторное закрытие: 409")]
    public async Task Closing_edge_cases()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));

        (await Api.Post($"api/polls/{poll.Id}/close")).ShouldBe(401);
        (await Api.Post($"api/polls/{Guid.NewGuid()}/close", null, owner.Token)).ShouldBe(404);

        await Make.Close(owner, poll);
        (await Api.Post($"api/polls/{poll.Id}/close", null, owner.Token)).ShouldBe(409);
    }

    [Fact(DisplayName = "Админ сайта, не состоящий в клубе, не может закрыть чужой отбор")]
    public async Task Site_admin_is_not_automatically_a_club_admin()
    {
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));
        var siteAdmin = await TestUser.CreateAdmin();

        (await Api.Post($"api/polls/{poll.Id}/close", null, siteAdmin.Token)).ShouldBe(403);
    }

    // ---------- время вышло ----------

    [Fact(DisplayName = "Когда время вышло, отбор считается закрытым: результаты открыты, голосовать нельзя, закрыть ещё раз нельзя")]
    public async Task Expired_poll_behaves_like_a_closed_one()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(1);
        var poll = await Make.Poll(owner, clubId);
        await Make.Vote(owner, poll, 0);
        await Expire(poll);

        Assert.True((await Api.Get("api/polls/" + poll.Id, owner.Token)).ShouldBe(200).Bool("isClosed"));
        Assert.False((await Api.Get("api/clubs/" + clubId)).ShouldBe(200).Bool("hasActivePoll"));

        var results = (await Results(members[0], poll)).ShouldBe(200);
        Assert.True(results.Bool("isClosed"));
        Assert.Equal(1, results.Int("totalVotes"));

        (await Vote(members[0], poll, 0)).ShouldBe(400);
        (await Api.Post($"api/polls/{poll.Id}/close", null, owner.Token)).ShouldBe(409);
    }

    // ---------- тайна голоса ----------

    [Fact(DisplayName = "В таблице голосов нет столбца с пользователем: связать голос с человеком нельзя даже по базе")]
    public async Task Votes_table_has_no_user_column()
    {
        var columns = await Db.Column(
            "SELECT column_name FROM information_schema.columns WHERE table_name = 'Votes' AND table_schema = 'public'");

        Assert.NotEmpty(columns);
        Assert.DoesNotContain(columns, c => c.Contains("User", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "Ни в карточке отбора, ни в результатах нет номеров и имён проголосовавших")]
    public async Task Responses_do_not_leak_who_voted()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(1);
        var poll = await Make.Poll(owner, clubId);
        await Make.Vote(owner, poll, 0);
        await Make.Vote(members[0], poll, 1);
        await Make.Close(owner, poll);

        var bodies = new[]
        {
            (await Api.Get("api/polls/" + poll.Id, owner.Token)).ShouldBe(200).Body,
            (await Results(owner, poll)).ShouldBe(200).Body
        };

        foreach (var voter in new[] { owner, members[0] })
        {
            foreach (var body in bodies)
            {
                Assert.DoesNotContain(voter.Id.ToString(), body, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(voter.Username, body);
            }
        }
    }

    [Fact(DisplayName = "Голос меняет счёт на выбранном варианте и только на нём")]
    public async Task Vote_lands_on_the_chosen_option_only()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(1);
        var poll = await Make.Poll(owner, clubId, books: 3);
        await Make.Vote(members[0], poll, 2);
        await Make.Close(owner, poll);

        var results = (await Results(owner, poll)).ShouldBe(200);

        Assert.Equal(0, ResultOf(results, poll.OptionIds[0]).GetProperty("voteCount").GetInt32());
        Assert.Equal(0, ResultOf(results, poll.OptionIds[1]).GetProperty("voteCount").GetInt32());
        Assert.Equal(1, ResultOf(results, poll.OptionIds[2]).GetProperty("voteCount").GetInt32());
        Assert.Equal(100.0, ResultOf(results, poll.OptionIds[2]).GetProperty("percentage").GetDouble(), 1);
    }
}
