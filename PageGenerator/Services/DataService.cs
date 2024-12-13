namespace PageGenerator.Services;

using Microsoft.Data.Sqlite;
using Models;
using Enums;

public class DataService(IConfiguration configuration)
{
    private readonly string? _connectionString = configuration.GetConnectionString("DbConnection");

    public async Task<User> DefineUser(long userId, long chatId)
    {
        try
        {
            await using SqliteConnection connection = new(_connectionString);
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT chat_id
                                       , role
                                       , action
                                  FROM users
                                  WHERE id = @id 
                                    AND chat_id = @chatId
                                  """;
            command.Parameters.AddWithValue("@id", userId);
            command.Parameters.AddWithValue("@chatId", chatId);
            await using SqliteDataReader userReader = await command.ExecuteReaderAsync();
            User user = new(userId);
            if (!userReader.Read())
            {
                user.Action = ActionEnum.NoChat;
                return user;
            }
            
            user.ChatId = userReader.GetInt32(0);
            user.Role = userReader.GetInt32(1);
            user.Action = (ActionEnum)userReader.GetInt32(2);
            return user;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    public async Task<ActionEnum> DefineUserAction(long userId, long chatId)
    {
        try
        {
            await using SqliteConnection connection = new(_connectionString);
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT action
                                  FROM users
                                  WHERE id = @id 
                                    AND chat_id = @chatId;
                                  """;
            command.Parameters.AddWithValue("@id", userId);
            command.Parameters.AddWithValue("@chatId", chatId);
            return (ActionEnum)(await command.ExecuteScalarAsync() ?? 0);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task UpdateUserAction(long userId, ActionEnum actionType)
    {
        try
        {
            await using SqliteConnection connection = new(_connectionString);
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE users SET action = @action WHERE id = @id";
            command.Parameters.AddWithValue("@id", userId);
            command.Parameters.AddWithValue("@action", (int)actionType);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<int> GetInteractionMessageId(long userId, long chatId)
    {
        try
        {
            await using SqliteConnection connection = new(_connectionString);
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT interaction_message_id
                                  FROM users
                                  WHERE id = @id 
                                    AND chat_id = @chatId;
                                  """;
            command.Parameters.AddWithValue("@id", userId);
            command.Parameters.AddWithValue("@chatId", chatId);
            return (int)(await command.ExecuteScalarAsync() ?? 0);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}