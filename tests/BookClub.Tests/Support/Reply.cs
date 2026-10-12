using System.Net;
using System.Text.Json;
using Xunit.Sdk;

namespace BookClub.Tests.Support;

/// <summary>Ответ сервера: код, тело и удобные способы достать из тела нужное.</summary>
public sealed class Reply
{
    private JsonElement? _json;

    public Reply(HttpStatusCode status, string body, string? location)
    {
        Status = status;
        Body = body;
        Location = location;
    }

    public HttpStatusCode Status { get; }

    public int Code => (int)Status;

    /// <summary>Тело ответа как есть. Для текстовых ответов сервера это просто строка без кавычек.</summary>
    public string Body { get; }

    /// <summary>Заголовок Location (адрес созданной записи).</summary>
    public string? Location { get; }

    /// <summary>Тело как JSON. Падает с понятным сообщением, если там не JSON.</summary>
    public JsonElement Json
    {
        get
        {
            if (_json is null)
            {
                try
                {
                    _json = JsonSerializer.Deserialize<JsonElement>(Body);
                }
                catch (JsonException)
                {
                    throw new XunitException($"Ожидали JSON, получили: {Short(Body)} (HTTP {Code})");
                }
            }

            return _json.Value;
        }
    }

    /// <summary>Проверяет код ответа. При ошибке в сообщении будет и код, и тело: сразу видно, что не так.</summary>
    public Reply ShouldBe(int expected)
    {
        if (Code != expected)
            throw new XunitException($"Ожидали HTTP {expected}, получили {Code}. Ответ сервера: {Short(Body)}");
        return this;
    }

    public string Str(string property) => Json.GetProperty(property).GetString()!;

    public Guid GuidOf(string property) => Json.GetProperty(property).GetGuid();

    public int Int(string property) => Json.GetProperty(property).GetInt32();

    public bool Bool(string property) => Json.GetProperty(property).GetBoolean();

    /// <summary>Есть ли у JSON-объекта поле с таким именем (без учёта регистра).</summary>
    public bool Has(string property) =>
        Json.ValueKind == JsonValueKind.Object &&
        Json.EnumerateObject().Any(p => string.Equals(p.Name, property, StringComparison.OrdinalIgnoreCase));

    /// <summary>Элементы JSON-массива.</summary>
    public List<JsonElement> Items() => Json.EnumerateArray().ToList();

    private static string Short(string text) => text.Length <= 300 ? text : text[..300] + "...";
}
