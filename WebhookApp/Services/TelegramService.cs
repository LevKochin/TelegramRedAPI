namespace WebhookApp.Services;

using Enums;
using Models;
using System.Text;
using System.Net;
using System.Net.Mime;
using System.Text.Json;

public class TelegramService(IHttpClientFactory clientFactory)
{
    private readonly object[][] _adminInlineKeyboard =
    [
        [
            new { text = "📚 Публикация", callback_data = "startPublication" },
            new { text = "🚀 Репост", callback_data = "startForward" }
        ],
        [
            new { text = "🗑️ Удаление", callback_data = "startDelete" },
            new { text = "🔧 Настройки", callback_data = "getUserSettings" },
            new { text = "❓ Помощь", callback_data = "help" }
        ]
    ];

    private readonly object[][] _userInlineKeyboard =
    [
        [
            new { text = "📚 Публикация", callback_data = "startPublication" },
            new { text = "🚀 Репост", callback_data = "startForward" }
        ],
        [
            new { text = "🗑️ Удаление", callback_data = "startDelete" },
            new { text = "❓ Помощь", callback_data = "help" }
        ]
    ];

    private readonly object[] _userBotCommands =
    [
        new
        {
            command = "/init_menu",
            description = "Инициализация меню"
        }
    ];

    private readonly HttpClient _httpClient = clientFactory.CreateClient("TelegramApiClient");

    private readonly string _apiSignatureUrl =
        "https://api.telegram.org/bot" + Environment.GetEnvironmentVariable("token");

    private readonly List<string>? _chatIds = Environment.GetEnvironmentVariable("chatIds")?.Split(",").ToList();

    public async Task<int> InitializedUserChatBot(long chatId)
    {
        string requestBody = JsonSerializer.Serialize(new
        {
            chat_id = chatId,
            text = "Выберите опцию:",
            reply_markup = new
            {
                inline_keyboard = _userInlineKeyboard
            }
        });
        StringContent content = new(requestBody, Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/sendMessage", content);
        response.EnsureSuccessStatusCode();
        string responseContent = await response.Content.ReadAsStringAsync();
        JsonElement responseAsJson = JsonDocument.Parse(responseContent).RootElement;
        await SetCommandMenu(ConvertToJson(new
        {
            commands = _userBotCommands
        }));
        return GetIdFromMessage(responseAsJson);
    }

    public async Task<int> InitializedAdminChatBot(long chatId)
    {
        string requestBody = JsonSerializer.Serialize(new
        {
            chat_id = chatId,
            text = "Выберите опцию:",
            reply_markup = new
            {
                inline_keyboard = _adminInlineKeyboard
            }
        });
        StringContent content = new(requestBody, Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/sendMessage", content);
        response.EnsureSuccessStatusCode();
        string responseContent = await response.Content.ReadAsStringAsync();
        JsonElement responseAsJson = JsonDocument.Parse(responseContent).RootElement;
        await SetCommandMenu(ConvertToJson(new
        {
            commands = _userBotCommands
        }));
        return GetIdFromMessage(responseAsJson);
    }

    public async Task BrowsUserIdentifier(long chatId, long userId) =>
        await SendMessage(ConvertToJson(new
        {
            chat_id = chatId,
            text = userId.ToString(),
        }));

    public async Task<int> StartPublication(long chatId, int messageId)
    {
        await ChangeMessage(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
            text =
                "Вы начали формирование сообщения для рассылки по каналам. Пожалуйста создайте сообщение для рассылки",
        }));
        return await EditButtons(ConvertToJson(new
        {
            chat_id = chatId,
            message_id = messageId,
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "🚫 Отменить", callback_data = "backToMain" }
                    }
                }
            }
        }));
    }

    public async Task<int> PublicationNextStep(long chatId, string text) =>
        await SendMessage(ConvertToJson(new
        {
            chat_id = chatId,
            text,
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "👈 В главное меню", callback_data = "rejectPublication" },
                        new { text = "📨 Опубликовать", callback_data = "endPublication" }
                    }
                }
            }
        }));


    public async Task<int> ShareNextStep(long chatId, string text) =>
        await SendMessage(ConvertToJson(new
        {
            chat_id = chatId,
            text,
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "👈 В главное меню", callback_data = "rejectSharing" },
                        new { text = "📨 Опубликовать", callback_data = "endForward" }
                    }
                }
            }
        }));


    public async IAsyncEnumerable<UserMessage> PostMessages(ICollection<Interaction> interactions, long chatId,
        int groupId)
    {
        foreach (Interaction interaction in interactions.Where(i => i.InteractionType == InteractionEnum.Post))
        {
            foreach (string toChatId in _chatIds)
            {
                yield return new UserMessage
                {
                    Id = await CopyMessage(ConvertToJson(new
                    {
                        chat_id = toChatId,
                        from_chat_id = chatId,
                        message_id = interaction.MessageId,
                    })),
                    ChatId = toChatId,
                    GroupId = groupId
                };
            }
        }
    }


    public async IAsyncEnumerable<UserMessage> ForwardMessages(ICollection<Interaction> interactions, long chatId,
        int groupId)
    {
        foreach (Interaction interaction in interactions.Where(i => i.InteractionType == InteractionEnum.Post))
        {
            foreach (string toChatId in _chatIds)
            {
                yield return new UserMessage
                {
                    Id = await ForwardMessage(ConvertToJson(new
                    {
                        chat_id = toChatId,
                        from_chat_id = chatId,
                        message_id = interaction.MessageId,
                    })),
                    ChatId = toChatId,
                    GroupId = groupId
                };
            }
        }
    }


    public async Task<int> ForwardIntoChat(object fromChatId, int messageId, long chatId) =>
        await ForwardMessage(ConvertToJson(new
        {
            chat_id = chatId,
            from_chat_id = fromChatId,
            message_id = messageId,
        }));

    public async Task<int> StartForward(long chatId, int messageId) =>
        await ChangeMessage(ConvertToJson(new
        {
            chat_id = chatId,
            message_id = messageId,
            text = "Выберите способ рассылки по каналам: ",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "📨 Share", callback_data = "startShare" },
                        new { text = "📤 Ссылка", callback_data = "startForwardByLink" }
                    }
                }
            }
        }));


    public async Task<int> StartForwardByLink(long chatId, int messageId) =>
        await ChangeMessage(ConvertToJson(new
        {
            chat_id = chatId,
            message_id = messageId,
            text = "Начат процесс публикации сообщения по ссылке. Вставьте актуальную ссылку или в главное меню: ",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "👈 В главное меню", callback_data = "backToMain" }
                    }
                }
            }
        }));

    public async Task<int> StartShare(long chatId, int messageId) =>
        await ChangeMessage(ConvertToJson(new
        {
            chat_id = chatId,
            message_id = messageId,
            text =
                "Начата рассылка, пожалуйста выберете сообщение в любом канале и нажми стрелку Share и передайте чат-боту: ",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "👈 В главное меню", callback_data = "backToMain" }
                    }
                }
            }
        }));

    public async Task<int> StartAddUser(long chatId, int messageId) =>
        await ChangeMessage(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
            text = "Вы начали процесс добавления нового пользователя: пожалуйста введите id",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "🚫 Завершить", callback_data = "endAddUser" }
                    }
                }
            }
        }));

    public async Task<int> BrowsWrongUserId(long chatId) =>
        await SendMessage(ConvertToJson(new
        {
            chat_id = chatId,
            text = "Неверный формат, пожалуйста, введите корректное значение или завершить действие ",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "🚫 Завершить", callback_data = "endAddUser" }
                    }
                }
            }
        }));

    public async Task<int> BrowsUserIdExists(long chatId, int messageId) =>
        await ChangeMessage(ConvertToJson(new
        {
            chat_id = chatId,
            message_id = messageId,
            text = "Такой пользователь уже существует. Попробуйте ещё раз или завершите действие.",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "🚫 Завершить", callback_data = "endAddUser" }
                    }
                }
            }
        }));


    public async Task<int> BrowsSuccessAddingUser(long chatId, string userId, int messageId) =>
        await ChangeMessage(ConvertToJson(new
        {
            text = "Пользователь: " + userId +
                   " успешно добавлен, продолжайте добавление пользователей или завершите процесс",
            message_id = messageId,
            chat_id = chatId,
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "🚫 Завершить", callback_data = "endAddUser" }
                    }
                }
            }
        }));

    public async Task<int> GetSettingsCommand(long chatId, long messageId) =>
        await EditButtons(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
        }));

    public async Task<int> BrowsMainMenu(long chatId, ICollection<Interaction> interactions, bool isAdmin,
        string text = "Выберите опцию:")
    {
        Interaction lastInteraction = interactions.Last();
        if (lastInteraction.InteractionType is InteractionEnum.InlineKeyboard)
        {
            await ChangeMessage(ConvertToJson(new
            {
                message_id = lastInteraction.MessageId,
                chat_id = chatId,
                text
            }));
            if (isAdmin)
            {
                return await ChangeLastInlineToAdminMainMenu(chatId, lastInteraction.MessageId);
            }

            return await ChangeLastInlineToUserMainMenu(chatId, lastInteraction.MessageId);
        }
        
        if (isAdmin)
        {
            return await BrowsAdminMainMenu(chatId);
        }

        return await BrowsUserMainMenu(chatId);
    }


    public async Task<int> BrowsRemoveMenu(long chatId, int messageId) =>
        await EditButtons(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "👈 Назад", callback_data = "backToMain" }
                    },
                    new[]
                    {
                        new { text = "📛 Удаление по ссылке", callback_data = "startDeleteByLink" },
                        new { text = "📤 Удаление последних сообщений", callback_data = "startDeleteLast" }
                    }
                }
            }
        }));


    public async Task<int> BrowsIncorrectUrl(long chatId, int messageId) =>
        await ChangeMessage(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
            text = "Формат введённой ссылке не верный, пожалуйста, повторите попытку",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "👈 В главное меню", callback_data = "backToMain" },
                    }
                }
            }
        }));

    public async Task<int> BrowsError(long chatId, int messageId, string text) =>
        await ChangeMessage(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
            text,
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "👈 В главное меню", callback_data = "backToMain" },
                    }
                }
            }
        }));


    public async Task<int> StartDeleteLastPublication(long chatId, int messageId) =>
        await ChangeMessage(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
            text = "Подтвердите удаление последней публикации",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "🚫 Отмена", callback_data = "backToMain" },
                        new { text = "✅ Подтверждаю", callback_data = "endDeleteLast" }
                    }
                }
            }
        }));

    public async Task<int> StartDeletePublicationByLink(long chatId, int messageId) =>
        await ChangeMessage(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
            text = "Вы начали процесс удаления постов с помощью ссылки. Пожалуйста вставьте ссылку на пост",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "👈 В главное меню", callback_data = "backToMain" },
                    }
                }
            }
        }));

    public async Task DeleteLastMessage(IEnumerable<UserMessage> messages)
    {
        foreach (UserMessage message in messages)
        {
            await RemoveMessage(message.ChatId, message.Id);
        }
    }

    public async Task<int> RemoveKeyboard(long chatId, int messageId) =>
        await EditButtons(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
        }));

    public async Task RemoveMessage(long chatId, int messageId)
    {
        StringContent content = new(ConvertToJson(new
        {
            chat_id = chatId,
            message_id = messageId
        }), Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/deleteMessage", content);
        string responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseContent);
    }

    public async Task<JsonElement> RemoveMessage(object chatId, int messageId)
    {
        StringContent content = new(ConvertToJson(new
        {
            chat_id = chatId,
            message_id = messageId
        }), Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/deleteMessage", content);
        string responseContent = await response.Content.ReadAsStringAsync();
        JsonElement jsonContent = JsonDocument.Parse(responseContent).RootElement;
        return jsonContent;
    }

    private async Task<int> ChangeLastInlineToAdminMainMenu(long chatId, long messageId) =>
        await EditButtons(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
            reply_markup = new
            {
                inline_keyboard = _adminInlineKeyboard
            }
        }));

    private async Task<int> ChangeLastInlineToUserMainMenu(long chatId, long messageId) =>
        await EditButtons(ConvertToJson(new
        {
            message_id = messageId,
            chat_id = chatId,
            reply_markup = new
            {
                inline_keyboard = _userInlineKeyboard
            }
        }));
    
    
    public async Task<int> SendPreviewForwardMessage(long chatId) =>
        await SendMessage(ConvertToJson(new
        {
            chat_id = chatId,
            text = "Предпросмотр сообщения, которое вы хотите отправить, выберите опцию:",
            reply_markup = new
            {
                inline_keyboard = new []
                {
                    new[]
                    {
                        new { text = "👈 В главное меню", callback_data = "rejectForwardByLink" },
                        new { text = "📨 Опубликовать", callback_data = "endForwardByLink" }
                    }
                }
            }
        }));
    
    
    public async Task<int> SendPreviewPublicationMessage(long chatId, int messageId, string parseMode) =>
        await CopyMessage(ConvertToJson(new
        {
            chat_id = chatId,
            from_chat_id = chatId,
            message_id = messageId,
            parse_mode = parseMode,
        }));

    private async Task<int> BrowsUserMainMenu(long chatId) =>
        await SendMessage(ConvertToJson(new
        {
            chat_id = chatId,
            text = "Выберите опцию:",
            reply_markup = new
            {
                inline_keyboard = _userInlineKeyboard
            }
        }));

    private async Task<int> BrowsAdminMainMenu(long chatId) =>
        await SendMessage(ConvertToJson(new
        {
            chat_id = chatId,
            text = "Выберите опцию:",
            reply_markup = new
            {
                inline_keyboard = _userInlineKeyboard
            }
        }));

    private async Task<int> SendMessage(string message)
    {
        StringContent content = new(message, Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/sendMessage", content);
        response.EnsureSuccessStatusCode();
        string responseContent = await response.Content.ReadAsStringAsync();
        JsonElement responseAsJson = JsonDocument.Parse(responseContent).RootElement;
        return GetIdFromMessage(responseAsJson);
    }

    private async Task<int> EditButtons(string message)
    {
        StringContent content = new(message, Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response =
            await _httpClient.PostAsync(_apiSignatureUrl + "/editMessageReplyMarkup", content);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            return 0;
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        JsonElement responseAsJson = JsonDocument.Parse(responseContent).RootElement;
        return GetIdFromMessage(responseAsJson);
    }

    private async Task RemoveMessages(string message)
    {
        StringContent content = new StringContent(message, Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/deleteMessages", content);
        response.EnsureSuccessStatusCode();
    }

    private async Task SetCommandMenu(string message)
    {
        StringContent content = new(message, Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/setMyCommands", content);
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> CopyMessage(string message)
    {
        StringContent content = new(message, Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/copyMessage", content);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        JsonElement responseAsJson = JsonDocument.Parse(responseContent).RootElement;
        return GetIdFromMessage(responseAsJson);
    }

    private async Task<int> ForwardMessage(string message)
    {
        StringContent content = new(message, Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/forwardMessage", content);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            return 0;
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        JsonElement responseAsJson = JsonDocument.Parse(responseContent).RootElement;
        return GetIdFromMessage(responseAsJson);
    }

    private async Task<int> ChangeMessage(string message)
    {
        StringContent content = new(message, Encoding.UTF8, MediaTypeNames.Application.Json);
        HttpResponseMessage response = await _httpClient.PostAsync(_apiSignatureUrl + "/editMessageText", content);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            return 0;
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        JsonElement responseAsJson = JsonDocument.Parse(responseContent).RootElement;
        return GetIdFromMessage(responseAsJson);
    }

    private string ConvertToJson(object model) => JsonSerializer.Serialize(model);

    private int GetIdFromMessage(JsonElement message)
    {
        if (message.TryGetProperty("result", out JsonElement resultProp))
            return resultProp.TryGetProperty("message_id", out JsonElement messageIdProp)
                ? messageIdProp.GetInt32()
                : 0;

        return 0;
    }

}