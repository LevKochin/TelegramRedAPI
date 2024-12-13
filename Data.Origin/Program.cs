using Microsoft.Data.Sqlite;

string connectionString = "Data Source=./page_generator.db;Version=3;";
await using SqliteConnection connection = new(connectionString);
await using SqliteTransaction transaction = connection.BeginTransaction();
await connection.OpenAsync();
await using SqliteCommand command = connection.CreateCommand();
command.Transaction = transaction;
command.CommandText = """
                      CREATE TABLE IF NOT EXISTS users (
                          id BIGINT PRIMARY KEY,
                          chat_id BIGINT NOT NULL,
                          role INTEGER NOT NULL,
                          action INTEGER NOT NULL DEFAULT 0,
                          interaction_message_id BIGINT NOT NULL DEFAULT 0 
                      );
                      """;
await command.ExecuteNonQueryAsync();
command.CommandText = """
                      CREATE TABLE IF NOT EXISTS methadata (
                          id BIGINT PRIMARY KEY,
                          type VARCHAR(20) NOT NULL,
                          name VARCHAR(50) NOT NULL
                      )
                      """;
await command.ExecuteNonQueryAsync();
command.CommandText = """
                      INSERT INTO methadata (id, type, name)
                      VALUES (
                             (1, 'title', 'Заголовок сайта')
                           , (2, 'keywords', 'Ключевые слова')
                           , (3, 'description', 'Описание')
                           , (4, 'category', 'Категории')
                           , (5, 'date', 'Дата публикации')
                           , (6, 'featured_image', 'Картинка в шапке')
                           );
                      """;

command.CommandText = """
                      CREATE TABLE IF NOT EXISTS messages (
                          id BIGINT PRIMARY KEY,
                          index INTEGER NOT NULL DEFAULT 0,
                          page_id BIGINT NOT NULL
                      )
                      """;

command.CommandText = """
                      CREATE TABLE IF NOT EXISTS pages (
                          id BIGINT PRIMARY KEY,
                          
                      )
                      """;


command.CommandText = """
                      CREATE TABLE IF NOT EXISTS pages_messages (
                          id BIGINT PRIMARY KEY,
                          
                      )
                      """;
                      