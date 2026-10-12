using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace BookClub.Tests.E2E;

/// <summary>Админка в браузере: кто её видит и что в ней можно сделать.</summary>
[Collection("browser")]
[Trait("Category", "E2E")]
public class AdminUiTests(BrowserFixture browser)
{
    [Fact(DisplayName = "Пункт «Админка» виден только админу")]
    public async Task Admin_menu_item_is_visible_only_to_admins()
    {
        var regular = await TestUser.Create();
        var admin = await TestUser.CreateAdmin();

        await using (var visit = await SiteVisit.Open(browser, regular))
        {
            await visit.Check(async page =>
            {
                await Expect(page.Locator("#who .uname")).ToHaveTextAsync(regular.Username);
                await Expect(page.Locator("#navAdmin")).ToBeHiddenAsync();
            }, "Admin_menu_hidden_for_regular_user");
        }

        await using (var visit = await SiteVisit.Open(browser, admin))
        {
            await visit.Check(async page =>
            {
                await Expect(page.Locator("#navAdmin")).ToBeVisibleAsync();
                await visit.Go("admin");
                await Expect(page.Locator("#adminBody")).ToContainTextAsync("Панель управления");
                await Expect(page.Locator("#adminBody table.adm tbody tr", new PageLocatorOptions { HasText = admin.Username }))
                    .ToContainTextAsync("вы");
            }, "Admin_menu_visible_for_admin");
        }
    }

    [Fact(DisplayName = "Админ блокирует пользователя кнопкой: в таблице статус «заблокирован», войти этот человек больше не может")]
    public async Task Admin_can_block_a_user_from_the_panel()
    {
        var admin = await TestUser.CreateAdmin();
        var victim = await TestUser.Create();
        await using var visit = await SiteVisit.Open(browser, admin);

        await visit.Check(async page =>
        {
            await visit.Go("admin");
            var row = page.Locator("#adminBody table.adm tbody tr", new PageLocatorOptions { HasText = victim.Username });
            await row.Locator("[data-block]").ClickAsync();

            await Expect(page.Locator("#toast")).ToContainTextAsync("заблокирован");
            await Expect(row).ToContainTextAsync("заблокирован");
            await Expect(row.Locator("[data-unblock]")).ToBeVisibleAsync();
        });

        (await Api.Post("api/auth/login", new { login = victim.Username, password = victim.Password })).ShouldBe(403);
    }
}
