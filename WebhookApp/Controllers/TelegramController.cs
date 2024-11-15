namespace WebhookApp.Controllers;

using Enums;
using Services;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class TelegramController(
    ILogger<TelegramController> logger,
    UserActionService actionService) : ControllerBase
{
    [HttpGet]
    [Route("/")]
    public IActionResult CheckApp() => Ok("Telegram App v01");

    [HttpPost]
    [Route("/update")]
    public async Task GetUpdate([FromBody] JsonElement update, CancellationToken cancellationToken)
    {
        try
        {
            bool hasMessage = update.TryGetProperty("message", out JsonElement messageProp);
            bool hasCallback = update.TryGetProperty("callback_query", out JsonElement callbackProp);
            if (!hasMessage && !hasCallback)
            {
                Console.WriteLine();
                Console.WriteLine("Сообщение не найдено: содержимое ответа от чата: {0}", update);
                return;
            }

            JsonElement fromMessageProp = default;
            JsonElement fromCallbackProp = default;
            bool hasFromMessage = hasMessage &&
                                  messageProp.TryGetProperty("from", out fromMessageProp);
            bool hasFromCallback = hasCallback &&
                                   callbackProp.TryGetProperty("from", out fromCallbackProp);
            if (!hasFromMessage && !hasFromCallback)
            {
                Console.WriteLine();
                Console.WriteLine("Сообщение не содержит отправителя: содержимое ответа от чата: {0}", update);
                return;
            }

            JsonElement chatMessageProp = default;
            JsonElement chatCallbackProp = default;
            bool hasMessageChat = hasMessage &&
                                  messageProp.TryGetProperty("chat", out chatMessageProp);
            bool hasCallbackChat = hasCallback &&
                                   callbackProp.GetProperty("message").TryGetProperty("chat", out chatCallbackProp);
            if (!hasMessageChat && !hasCallbackChat)
            {
                Console.WriteLine();
                Console.WriteLine("Сообщение не содержит чат отправителя: содержимое ответа от чата: {0}", update);
                return;
            }

            long userId = hasFromCallback
                ? fromCallbackProp.GetProperty("id").GetInt64()
                : fromMessageProp.GetProperty("id").GetInt64();
            long chatId = hasCallbackChat
                ? chatCallbackProp.GetProperty("id").GetInt64()
                : chatMessageProp.GetProperty("id").GetInt64();
            string message = hasCallbackChat
                ? callbackProp.GetProperty("message").GetProperty("text").ToString().Trim()
                : messageProp.GetProperty("text").ToString().Trim();
            int messageId = hasCallbackChat 
                ? callbackProp.GetProperty("message").GetProperty("message_id").GetInt32()
                : messageProp.GetProperty("message_id").GetInt32();
            ActionEnum action = await actionService.DefineActiveActionType(userId, chatId);
            if (hasCallback &&
                Enum.TryParse(callbackProp.GetProperty("data").ToString(), true, out action))
            {
                await actionService.ExecuteActionFromType(action, chatId, userId, message, messageId);
                return;
            }

            if (action is not ActionEnum.None)
            {
                await actionService.ExecuteActionFromType(action, chatId, userId, message, messageId);
                return;
            }

            bool hasBotCommand = messageProp.TryGetProperty("bot_command", out JsonElement botCommandProp);
            switch (hasBotCommand)
            {
                case true:
                    await actionService.ExecuteActionFromType(ActionEnum.BackToMain, chatId, userId, message, messageId);
                    return;
                case false:
                    await actionService.RemoveCurrentUselessMessage(chatId, messageId);
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }
}