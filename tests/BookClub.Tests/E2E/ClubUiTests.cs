using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace BookClub.Tests.E2E;

/// <summary>Клубы и голосование: путь человека от создания клуба до вскрытого конверта.</summary>
[Collection("browser")]
[Trait("Category", "E2E")]
public class ClubUiTests(BrowserFixture browser)
{
    [Fact(DisplayName = "Создание клуба через окно: клуб открывается, создатель — «Админ», отборов ещё не было")]
    public async Task Creating_a_club_opens_its_room()
    {
        var owner = await TestUser.Create();
        var name = "Клуб UI " + Make.Tag();
        await using var visit = await SiteVisit.Open(browser, owner);

        await visit.Check(async page =>
        {
            await visit.Go("clubs");
            await page.Locator("#newClubBtn").ClickAsync();
            await page.Locator("#f-cname").FillAsync(name);
            await page.Locator("#f-cdesc").FillAsync("Читаем по вечерам");
            await page.Locator("#mOk").ClickAsync();

            await Expect(page.Locator("#clubName")).ToHaveTextAsync(name);
            await Expect(page.Locator("#clubDesc")).ToHaveTextAsync("Читаем по вечерам");
            await Expect(page.Locator("#joinBtn")).ToHaveTextAsync("Вы администратор клуба");
            await Expect(page.Locator("#joinBtn")).ToBeDisabledAsync();
            await Expect(page.Locator(".mem .nm")).ToHaveTextAsync(owner.Username);
            await Expect(page.Locator(".mem .rl")).ToHaveTextAsync("Админ");
            await Expect(page.Locator("#pollArea")).ToContainTextAsync("В клубе ещё не было отборов");
        });
    }

    [Fact(DisplayName = "Пустое название клуба: окно остаётся открытым, причина показана")]
    public async Task Club_form_explains_what_is_wrong()
    {
        var owner = await TestUser.Create();
        await using var visit = await SiteVisit.Open(browser, owner);

        await visit.Check(async page =>
        {
            await visit.Go("clubs");
            await page.Locator("#newClubBtn").ClickAsync();
            await page.Locator("#mOk").ClickAsync();

            await Expect(page.Locator("#toast")).ToContainTextAsync("Название");
            await Expect(page.Locator("#modalBg")).ToHaveClassAsync(new Regex("open"));
        });
    }

    [Fact(DisplayName = "Вступление в чужой клуб: кнопка меняется на «Вы участник», в списке уже двое")]
    public async Task Joining_a_club_changes_the_button()
    {
        var owner = await TestUser.Create();
        var name = "Открытый " + Make.Tag();
        await Make.Club(owner, name: name);
        var guest = await TestUser.Create();
        await using var visit = await SiteVisit.Open(browser, guest);

        await visit.Check(async page =>
        {
            await visit.Go("clubs");
            await page.Locator(".club", new PageLocatorOptions { HasText = name }).ClickAsync();
            await Expect(page.Locator("#joinBtn")).ToHaveTextAsync("Вступить в клуб");

            await page.Locator("#joinBtn").ClickAsync();

            await Expect(page.Locator("#joinBtn")).ToHaveTextAsync("Вы участник");
            await Expect(page.Locator("#joinBtn")).ToBeDisabledAsync();
            await Expect(page.Locator(".mem")).ToHaveCountAsync(2);
            await Expect(page.Locator("#newPollBtn")).ToBeEnabledAsync();
        });
    }

    [Fact(DisplayName = "Не участник видит приглашение вступить и не может создать отбор")]
    public async Task Outsider_cannot_start_a_poll_from_the_ui()
    {
        var owner = await TestUser.Create();
        var name = "Чужой " + Make.Tag();
        await Make.Club(owner, name: name);
        var guest = await TestUser.Create();
        await using var visit = await SiteVisit.Open(browser, guest);

        await visit.Check(async page =>
        {
            await visit.Go("clubs");
            await page.Locator(".club", new PageLocatorOptions { HasText = name }).ClickAsync();

            await Expect(page.Locator("#newPollBtn")).ToBeDisabledAsync();
        });
    }

    [Fact(DisplayName = "Весь путь отбора: создать, проголосовать, увидеть запечатанный конверт, вскрыть и увидеть победителя")]
    public async Task Poll_from_creation_to_the_opened_envelope()
    {
        var (clubId, owner, members) = await Make.ClubWithMembers(1);
        var other = members[0];
        var clubName = (await Api.Get("api/clubs/" + clubId)).ShouldBe(200).Str("name");
        var tag = Make.Tag();
        var winner = "Победитель " + tag;
        var loser = "Проигравший " + tag;
        await Make.Book(owner, title: winner, author: "Автор П");
        await Make.Book(owner, title: loser, author: "Автор Л");
        var question = "Что читаем " + tag + "?";
        await using var visit = await SiteVisit.Open(browser, owner);

        await visit.Check(async page =>
        {
            // 1. создаём отбор
            await visit.Go("clubs");
            await page.Locator(".club", new PageLocatorOptions { HasText = clubName }).ClickAsync();
            await page.Locator("#newPollBtn").ClickAsync();
            await page.Locator("#f-q").FillAsync(question);
            await page.Locator("#f-dur").SelectOptionAsync("60");
            await page.Locator("#pkSearch").FillAsync(tag);
            await page.Locator("#pkList label", new PageLocatorOptions { HasText = winner }).Locator("input.pb").CheckAsync();
            await page.Locator("#pkList label", new PageLocatorOptions { HasText = loser }).Locator("input.pb").CheckAsync();
            await Expect(page.Locator("#pkCount")).ToContainTextAsync("Выбрано 2 из 10");
            await page.Locator("#mOk").ClickAsync();

            await Expect(page.Locator("#toast")).ToContainTextAsync("Отбор создан");
            await Expect(page.Locator(".env-q")).ToHaveTextAsync(question);
            await Expect(page.Locator(".eopt")).ToHaveCountAsync(2);
            await Expect(page.Locator("#clock")).ToBeVisibleAsync();

            // 2. голосуем за «победителя»
            await page.Locator(".eopt", new PageLocatorOptions { HasText = winner }).ClickAsync();
            await page.Locator("#envVote").ClickAsync();
            await Expect(page.Locator("#toast")).ToContainTextAsync("Голос принят");
            await Expect(page.Locator(".waitbox")).ToContainTextAsync("Ваш голос запечатан");
            await Expect(page.Locator(".count-big")).ToHaveTextAsync("1");

            // 3. пока конверт запечатан, процентов нет
            foreach (var option in await page.Locator(".eopt").AllAsync())
                await Expect(option).ToHaveAttributeAsync("data-pct", "0");
        });

        // второй участник голосует за того же через API (в браузере второго окна не нужно)
        var polls = (await Api.Get($"api/clubs/{clubId}/polls", owner.Token)).ShouldBe(200).Items();
        var pollId = polls[0].GetProperty("id").GetGuid();
        var options = (await Api.Get("api/polls/" + pollId, owner.Token)).ShouldBe(200).Json.GetProperty("options").EnumerateArray();
        var winnerOption = options.Single(o => o.GetProperty("title").GetString() == winner).GetProperty("id").GetGuid();
        (await Api.Post($"api/polls/{pollId}/vote", new { pollOptionId = winnerOption }, other.Token)).ShouldBe(200);

        await visit.Check(async page =>
        {
            // 4. админ вскрывает конверт досрочно
            await page.ReloadAsync();
            await visit.Go("clubs");
            await page.Locator(".club", new PageLocatorOptions { HasText = clubName }).ClickAsync();
            await Expect(page.Locator(".count-big")).ToHaveTextAsync("2");
            await page.Locator("#envClose").ClickAsync();

            await Expect(page.Locator(".winline")).ToContainTextAsync("Клуб выбрал " + winner);
            await Expect(page.Locator(".winline")).ToContainTextAsync("всего голосов: 2");
            await Expect(page.Locator(".eopt[data-win='1']")).ToHaveCountAsync(1);
            await Expect(page.Locator(".eopt[data-win='1'] .pct")).ToHaveTextAsync("100%");
            await Expect(page.Locator("#envVote")).ToHaveCountAsync(0);
        });
    }
}
