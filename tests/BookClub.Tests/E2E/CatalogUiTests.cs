using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace BookClub.Tests.E2E;

/// <summary>Каталог, полка, рецензии и безопасность показа чужого текста.</summary>
[Collection("browser")]
[Trait("Category", "E2E")]
public class CatalogUiTests(BrowserFixture browser)
{
    [Fact(DisplayName = "Добавление книги через окно: она появляется в каталоге с автором, годом и числом страниц")]
    public async Task Added_book_appears_in_the_catalog()
    {
        var user = await TestUser.Create();
        var title = "Роман " + Make.Tag();
        await using var visit = await SiteVisit.Open(browser, user);

        await visit.Check(async page =>
        {
            await visit.Go("catalog");
            await page.Locator("#newBookBtn").ClickAsync();
            await page.Locator("#f-t").FillAsync(title);
            await page.Locator("#f-a").FillAsync("Тестовый Автор");
            await page.Locator("#f-g").FillAsync("Фантастика");
            await page.Locator("#f-y").FillAsync("1999");
            await page.Locator("#f-p").FillAsync("321");
            await page.Locator("#mOk").ClickAsync();

            await Expect(page.Locator("#toast")).ToContainTextAsync("Книга добавлена");
            var card = page.Locator("#catGrid .bk", new PageLocatorOptions { HasText = title });
            await Expect(card).ToBeVisibleAsync();
            await Expect(card).ToContainTextAsync("Тестовый Автор");
            await Expect(card).ToContainTextAsync("1999");
            await Expect(card).ToContainTextAsync("321 стр.");
        });
    }

    [Fact(DisplayName = "Книга без названия: окно остаётся открытым, причина показана")]
    public async Task Book_form_complains_about_a_missing_title()
    {
        var user = await TestUser.Create();
        await using var visit = await SiteVisit.Open(browser, user);

        await visit.Check(async page =>
        {
            await visit.Go("catalog");
            await page.Locator("#newBookBtn").ClickAsync();
            await page.Locator("#f-a").FillAsync("Автор без книги");
            await page.Locator("#mOk").ClickAsync();

            await Expect(page.Locator("#toast")).ToContainTextAsync("Название обязательно");
            await Expect(page.Locator("#modalBg")).ToHaveClassAsync(new Regex("open"));
        });
    }

    [Fact(DisplayName = "Гость жмёт «На полку»: его отправляют на вход")]
    public async Task Guest_cannot_use_the_shelf()
    {
        var user = await TestUser.Create();
        var title = "Для гостя " + Make.Tag();
        await Make.Book(user, title: title);
        await using var visit = await SiteVisit.Open(browser);

        await visit.Check(async page =>
        {
            await visit.Go("catalog");
            await page.Locator("#catGrid .bk", new PageLocatorOptions { HasText = title }).Locator("[data-add]").ClickAsync();

            await Expect(page.Locator("#page-auth")).ToHaveClassAsync(new Regex("active"));
        });
    }

    [Fact(DisplayName = "Полка: положить книгу, отметить «Прочёл», поставить оценку — счётчик прочитанного вырос")]
    public async Task Shelf_flow_from_catalog_to_rating()
    {
        var user = await TestUser.Create();
        var title = "Для полки " + Make.Tag();
        await Make.Book(user, title: title);
        await using var visit = await SiteVisit.Open(browser, user);

        await visit.Check(async page =>
        {
            await visit.Go("catalog");
            var catalogCard = page.Locator("#catGrid .bk", new PageLocatorOptions { HasText = title });
            await catalogCard.Locator("[data-add]").ClickAsync();
            await Expect(page.Locator("#toast")).ToContainTextAsync("Добавлено на полку");
            await Expect(catalogCard.Locator("[data-add]")).ToHaveTextAsync("На полке");

            await visit.Go("profile");
            var shelfCard = page.Locator("#shelfGrid .bk", new PageLocatorOptions { HasText = title });
            await Expect(shelfCard).ToBeVisibleAsync();
            await shelfCard.Locator("[data-st='Finished']").ClickAsync();
            await Expect(page.Locator("#toast")).ToContainTextAsync("Статус: Прочитано");

            await shelfCard.Locator("[data-rate='4']").ClickAsync();
            await Expect(page.Locator("#toast")).ToContainTextAsync("Оценка сохранена");
            await Expect(shelfCard.Locator("[data-rate='4']")).ToHaveClassAsync(new Regex(@"\bon\b"));
            await Expect(page.Locator(".prof-stats .stat .n").First).ToHaveTextAsync("1");
        });

        var shelf = (await Api.Get("api/me/books", user.Token)).ShouldBe(200).Items();
        Assert.Equal(4, Assert.Single(shelf).GetProperty("rating").GetInt32());
    }

    [Fact(DisplayName = "Анонимная рецензия: автор видит пометки «Анонимно» и «ваша», а другой читатель имени автора не видит вовсе")]
    public async Task Anonymous_review_hides_the_name_in_the_ui()
    {
        var author = await TestUser.Create();
        var reader = await TestUser.Create();
        var title = "Для рецензии " + Make.Tag();
        await Make.Book(author, title: title);

        await using (var visit = await SiteVisit.Open(browser, author))
        {
            await visit.Check(async page =>
            {
                await visit.Go("catalog");
                await page.Locator("#catGrid .bk .t", new PageLocatorOptions { HasText = title }).ClickAsync();
                await page.Locator("#writeReviewBtn").ClickAsync();
                await page.Locator("#rvStars .star[data-r='5']").ClickAsync();
                await page.Locator("#rvText").FillAsync("Очень понравилось");
                await page.Locator("#rvAnon").CheckAsync();
                await page.Locator("#mOk").ClickAsync();
                await Expect(page.Locator("#toast")).ToContainTextAsync("Рецензия опубликована");

                await page.Locator("#catGrid .bk .t", new PageLocatorOptions { HasText = title }).ClickAsync();
                await Expect(page.Locator("#modalBox .rev")).ToHaveCountAsync(1);
                await Expect(page.Locator("#modalBox .rev .anon")).ToHaveTextAsync("Анонимно");
                await Expect(page.Locator("#modalBox .mine-tag")).ToHaveTextAsync("ваша");
                await Expect(page.Locator("#modalBox")).ToContainTextAsync("Очень понравилось");
                await Expect(page.Locator("#modalBox")).Not.ToContainTextAsync(author.Username);
            });
        }

        await using (var visit = await SiteVisit.Open(browser, reader))
        {
            await visit.Check(async page =>
            {
                await visit.Go("catalog");
                await page.Locator("#catGrid .bk .t", new PageLocatorOptions { HasText = title }).ClickAsync();

                await Expect(page.Locator("#modalBox .rev .anon")).ToHaveTextAsync("Анонимно");
                await Expect(page.Locator("#modalBox .mine-tag")).ToHaveCountAsync(0);
                await Expect(page.Locator("#modalBox")).Not.ToContainTextAsync(author.Username);
            }, "Anonymous_review_hides_the_name_in_the_ui_reader");
        }
    }

    [Fact(DisplayName = "Чужой текст показывается как текст: теги и скрипты в названиях не выполняются")]
    public async Task Html_in_user_text_is_shown_as_plain_text()
    {
        var user = await TestUser.Create();
        var tag = Make.Tag();
        var bookTitle = "<b>Жирное</b><img src=x onerror=\"window.__xss=1\"> " + tag;
        var clubName = "<i>Курсив</i><img src=x onerror=\"window.__xss=2\"> " + tag;
        await Make.Book(user, title: bookTitle);
        await Make.Club(user, name: clubName);
        await using var visit = await SiteVisit.Open(browser, user);

        await visit.Check(async page =>
        {
            await visit.Go("catalog");
            await Expect(page.Locator("#catGrid .bk .t", new PageLocatorOptions { HasText = tag })).ToContainTextAsync("<b>Жирное</b>");
            await Expect(page.Locator("#catGrid .bk .t b")).ToHaveCountAsync(0);

            await visit.Go("clubs");
            await Expect(page.Locator("#clubsGrid .club h3", new PageLocatorOptions { HasText = tag })).ToContainTextAsync("<i>Курсив</i>");
            await Expect(page.Locator("#clubsGrid .club h3 i")).ToHaveCountAsync(0);

            Assert.Null(await page.EvaluateAsync<object?>("() => window.__xss ?? null"));
        });
    }
}
