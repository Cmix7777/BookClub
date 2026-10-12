using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BookClub.Tests.Support;

/// <summary>
/// Тонкая обёртка над HttpClient: отправляет запрос на сайт и возвращает <see cref="Reply"/>.
/// Тесты общаются с сайтом только так, как это делает браузер: по HTTP, без доступа к коду сайта.
/// </summary>
public static class Api
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri(Settings.SiteUrl + "/"),
        Timeout = TimeSpan.FromSeconds(60)
    };

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
