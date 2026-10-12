namespace BookClub.Tests.Rest;

/// <summary>Клубы: создание, вступление, список участников.</summary>
[Trait("Category", "Api")]
public class ClubsTests
{
    [Fact(DisplayName = "Список клубов открыт без входа")]
    public async Task Club_list_is_public()
    {
        (await Api.Get("api/clubs")).ShouldBe(200);
    }

    [Fact(DisplayName = "Создать клуб без входа нельзя: 401")]
    public async Task Creating_a_club_requires_login()
    {
        (await Api.Post("api/clubs", new { name = "Без входа", description = "" })).ShouldBe(401);
    }

    [Fact(DisplayName = "Создание клуба: 201, создатель становится первым участником и админом клуба")]
    public async Task Creator_becomes_club_admin()
    {
        var owner = await TestUser.Create();
        var name = "Клуб " + Make.Tag();

        var created = (await Api.Post("api/clubs", new { name = "  " + name + "  ", description = " про книги " }, owner.Token))
            .ShouldBe(201);

        Assert.Equal(name, created.Str("name"));
        Assert.Equal("про книги", created.Str("description"));
        Assert.Equal(1, created.Int("membersCount"));
        Assert.False(created.Bool("hasActivePoll"));

        var members = (await Api.Get($"api/clubs/{created.GuidOf("id")}/members", owner.Token)).ShouldBe(200).Items();
        var only = Assert.Single(members);
        Assert.Equal(owner.Id, only.GetProperty("userId").GetGuid());
        Assert.Equal(owner.Username, only.GetProperty("username").GetString());
        Assert.Equal("Admin", only.GetProperty("role").GetString());
    }

    [Theory(DisplayName = "Неверные данные клуба отклоняются: 400")]
    [InlineData("", "ок")]
    [InlineData("я", "ок")]
    public async Task Invalid_club_is_rejected(string name, string description)
    {
        var owner = await TestUser.Create();

        (await Api.Post("api/clubs", new { name, description }, owner.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Слишком длинные название (больше 60) и описание (больше 300) отклоняются")]
    public async Task Too_long_club_fields_are_rejected()
    {
        var owner = await TestUser.Create();

        (await Api.Post("api/clubs", new { name = new string('к', 61), description = "" }, owner.Token)).ShouldBe(400);
        (await Api.Post("api/clubs", new { name = "Нормальное " + Make.Tag(), description = new string('о', 301) }, owner.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Описание клуба можно не писать")]
    public async Task Description_is_optional()
    {
        var owner = await TestUser.Create();

        (await Api.Post("api/clubs", new { name = "Без описания " + Make.Tag() }, owner.Token)).ShouldBe(201);
    }

    [Fact(DisplayName = "Клуб можно открыть без входа; несуществующий клуб: 404")]
    public async Task Club_details_are_public()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);

        var club = (await Api.Get("api/clubs/" + clubId)).ShouldBe(200);
        Assert.Equal(clubId, club.GuidOf("id"));

        (await Api.Get("api/clubs/" + Guid.NewGuid())).ShouldBe(404);
    }

    [Fact(DisplayName = "Вступление в клуб: 200, участников становится больше, роль Member")]
    public async Task Joining_adds_a_member()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        var guest = await TestUser.Create();

        (await Api.Post($"api/clubs/{clubId}/join", null, guest.Token)).ShouldBe(200);

        var club = (await Api.Get("api/clubs/" + clubId)).ShouldBe(200);
        Assert.Equal(2, club.Int("membersCount"));

        var members = (await Api.Get($"api/clubs/{clubId}/members", owner.Token)).ShouldBe(200).Items();
        var joined = members.Single(m => m.GetProperty("userId").GetGuid() == guest.Id);
        Assert.Equal("Member", joined.GetProperty("role").GetString());
    }

    [Fact(DisplayName = "Участники идут в порядке вступления: сначала создатель")]
    public async Task Members_are_listed_in_joining_order()
    {
        var (clubId, owner, others) = await Make.ClubWithMembers(2);

        var members = (await Api.Get($"api/clubs/{clubId}/members", owner.Token)).ShouldBe(200).Items();
        var ids = members.Select(m => m.GetProperty("userId").GetGuid()).ToList();

        Assert.Equal(new[] { owner.Id, others[0].Id, others[1].Id }, ids);
    }

    [Fact(DisplayName = "Второе вступление в тот же клуб: 409")]
    public async Task Joining_twice_is_a_conflict()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);

        (await Api.Post($"api/clubs/{clubId}/join", null, owner.Token)).ShouldBe(409);
    }

    [Fact(DisplayName = "Вступить без входа нельзя: 401; в несуществующий клуб: 404")]
    public async Task Joining_needs_login_and_an_existing_club()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);

        (await Api.Post($"api/clubs/{clubId}/join")).ShouldBe(401);
        (await Api.Post($"api/clubs/{Guid.NewGuid()}/join", null, owner.Token)).ShouldBe(404);
    }

    [Fact(DisplayName = "Список участников несуществующего клуба: 404")]
    public async Task Members_of_unknown_club_are_not_found()
    {
        var user = await TestUser.Create();

        (await Api.Get($"api/clubs/{Guid.NewGuid()}/members", user.Token)).ShouldBe(404);
    }

    [Fact(DisplayName = "Без входа список участников и отборов клуба не показывается: 401")]
    public async Task Members_and_polls_need_login()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);

        (await Api.Get($"api/clubs/{clubId}/members")).ShouldBe(401);
        (await Api.Get($"api/clubs/{clubId}/polls")).ShouldBe(401);
    }

    [Fact(DisplayName = "Новые клубы идут в списке первыми")]
    public async Task Newest_clubs_come_first()
    {
        var owner = await TestUser.Create();
        var tag = Make.Tag();
        await Make.Club(owner, name: "Первый " + tag);
        await Task.Delay(50);
        await Make.Club(owner, name: "Второй " + tag);

        var list = (await Api.Get("api/clubs")).ShouldBe(200).Items();
        var names = list.Select(c => c.GetProperty("name").GetString()!).Where(n => n.EndsWith(tag)).ToList();

        Assert.Equal(new[] { "Второй " + tag, "Первый " + tag }, names);
    }

    [Fact(DisplayName = "У нового клуба нет активного отбора, после создания отбора появляется")]
    public async Task Active_poll_flag_follows_the_poll()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        Assert.False((await Api.Get("api/clubs/" + clubId)).ShouldBe(200).Bool("hasActivePoll"));

        await Make.Poll(owner, clubId);

        Assert.True((await Api.Get("api/clubs/" + clubId)).ShouldBe(200).Bool("hasActivePoll"));
    }

    [Fact(DisplayName = "В общем списке клубов флаг «идёт отбор» следует за отбором: нет, есть, снова нет после закрытия")]
    public async Task Active_poll_flag_in_the_list_follows_the_poll()
    {
        var owner = await TestUser.Create();
        var clubId = await Make.Club(owner);
        Assert.False(FlagInList(await Api.Get("api/clubs"), clubId));

        var poll = await Make.Poll(owner, clubId);
        Assert.True(FlagInList(await Api.Get("api/clubs"), clubId));

        await Make.Close(owner, poll);
        Assert.False(FlagInList(await Api.Get("api/clubs"), clubId));
    }

    private static bool FlagInList(Reply list, Guid clubId) =>
        list.ShouldBe(200).Items()
            .Single(c => c.GetProperty("id").GetGuid() == clubId)
            .GetProperty("hasActivePoll").GetBoolean();

    [Fact(DisplayName = "Отборы несуществующего клуба: 404")]
    public async Task Polls_of_unknown_club_are_not_found()
    {
        var user = await TestUser.Create();

        (await Api.Get($"api/clubs/{Guid.NewGuid()}/polls", user.Token)).ShouldBe(404);
    }
}
