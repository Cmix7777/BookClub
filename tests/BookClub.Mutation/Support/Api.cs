using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BookClub.Tests.Support;

/// <summary>
/// Та же обёртка над HttpClient, что в основных тестах (те же методы Get, Post, Put, Delete и тот же Reply),
/// но сайт здесь не отдельный процесс, а живёт внутри тестового процесса. Запросы до него доходят
/// без сети, зато Stryker может подменять код сайта прямо во время прогона.
/// </summary>
public static class Api
{
    /// <summary>
    /// Сайт, запущенный внутри теста. В угловых скобках указан любой публичный класс сайта (AuthController):
    /// по нему фабрика находит сборку BookClub и её точку входа. Настройки (строка подключения и секрет токенов)
    /// сайт берёт из тех же переменных окружения, что и при обычном запуске.
    /// </summary>
    private sealed class InProcessSite : WebApplicationFactory<BookClub.Controllers.AuthController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // фабрика по умолчанию включает режим Development, а обычный запуск в CI идёт как Production
            builder.UseEnvironment("Production");
        }
    }

    private static readonly InProcessSite Site = new();

    private static readonly HttpClient Http = Site.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task<Reply> Get(string path, string? token = null) => Send(HttpMethod.Get, path, null, token);

    public static Task<Reply> Post(string path, object? body = null, string? token = null) =>
        Send(HttpMethod.Post, path, body, token);

    public static Task<Reply> Put(string path, object? body = null, string? token = null) =>
        Send(HttpMethod.Put, path, body, token);

    public static Task<Reply> Delete(string path, string? token = null) => Send(HttpMethod.Delete, path, null, token);

    /// <summary>Если body — строка, она уходит как есть (так можно отправить заведомо битый JSON).</summary>
    private static async Task<Reply> Send(HttpMethod method, string path, object? body, string? token)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            var json = body is string text ? text : JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await Http.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return new Reply(response.StatusCode, content, response.Headers.Location?.ToString());
    }
}
