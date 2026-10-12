using Npgsql;

namespace BookClub.Tests.Support;

/// <summary>
/// Прямой доступ к ТЕСТОВОЙ базе. Нужен только там, где через сайт сделать нельзя:
/// выдать первому пользователю роль админа, «состарить» голосование, заглянуть в устройство таблиц.
/// </summary>
public static class Db
{
    public static async Task<int> Exec(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(Settings.DbConnection);
        await connection.OpenAsync();
        await using var command = Build(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    public static async Task<T?> Scalar<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(Settings.DbConnection);
        await connection.OpenAsync();
        await using var command = Build(connection, sql, parameters);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)result;
    }

    public static async Task<List<string>> Column(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(Settings.DbConnection);
        await connection.OpenAsync();
        await using var command = Build(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
            values.Add(reader.GetString(0));
        return values;
    }

    private static NpgsqlCommand Build(NpgsqlConnection connection, string sql, (string Name, object Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return command;
    }
}
