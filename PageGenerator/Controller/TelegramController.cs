namespace PageGenerator.Controller;

using Enums;
using Models;
using Services;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class TelegramController(UserActionService actionService) : ControllerBase
{
    
    [HttpGet]
    [Route("/check")]
    public IActionResult CheckApp() => Ok("Telegram App v01");

    [HttpPost]
    [Route("/set_webhook")]
    public async Task<IActionResult> SetWebhook(
        [FromServices] TelegramService telegramService,
        [FromQuery] string? url) => Ok(await telegramService.UpdateWebhook(url));

    [HttpPost]
    [Route("/update")]
    public async Task<IActionResult> GetUpdate([FromBody] JsonElement update, CancellationToken cancellationToken)
    {
        string referer = HttpContext.Request.Headers.Referer.ToString();
        if (referer.Contains("swagger", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ResponseMessage(-5, "Вы не можете обращаться к API через swagger"));
        }

        try
        {
            bool hasMessage = update.TryGetProperty("message", out JsonElement messageProp);
            bool hasCallback = update.TryGetProperty("callback_query", out JsonElement callbackProp);
            if (!hasMessage && !hasCallback)
            {
                return BadRequest(new ResponseMessage(-4,
                    $"Сообщение не найдено: содержимое ответа от чата: {update}"));
            }

            JsonElement fromMessageProp = default;
            JsonElement fromCallbackProp = default;
            bool hasFromMessage = hasMessage &&
                                  messageProp.TryGetProperty("from", out fromMessageProp);
            bool hasFromCallback = hasCallback &&
                                   callbackProp.TryGetProperty("from", out fromCallbackProp);
            if (!hasFromMessage && !hasFromCallback)
            {
                return BadRequest(new ResponseMessage(-4,
                    $"Сообщение не содержит отправителя: содержимое ответа от чата:  {update}"));
            }

            JsonElement chatMessageProp = default;
            JsonElement chatCallbackProp = default;
            bool hasMessageChat = hasMessage &&
                                  messageProp.TryGetProperty("chat", out chatMessageProp);
            bool hasCallbackChat = hasCallback &&
                                   callbackProp.GetProperty("message").TryGetProperty("chat", out chatCallbackProp);
            if (!hasMessageChat && !hasCallbackChat)
            {
                return BadRequest(new ResponseMessage(-4,
                    $"Сообщение не содержит чат отправителя: содержимое ответа от чата: {update}"));
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
            ActionEnum action = await actionService.DefineUserAction(userId, chatId);
            if (hasCallback &&
                Enum.TryParse(callbackProp.GetProperty("data").ToString(), true, out action))
            {
                await actionService.RedefineAction(userId, action);
                await actionService.ExecuteActionFromType(action, chatId, userId, message, messageId);
                return Ok();
            }

            if (action is not ActionEnum.None)
            {
                await actionService.ExecuteActionFromType(action, chatId, userId, message, messageId);
                return Ok();
            }

            bool hasBotForward =
                messageProp.TryGetProperty("forward_from_message_id", out JsonElement forwardFromMessageProp);
            if (hasBotForward)
            {
                // await actionService.ExecuteActionFromType(ActionEnum.ProcessingShare, chatId, userId, message,
                //     messageId);
                return Ok();
            }

            bool hasBotCommand = messageProp.TryGetProperty("bot_command", out JsonElement botCommandProp);
            switch (hasBotCommand)
            {
                case true:
                    // await actionService.ExecuteActionFromType(ActionEnum.BackToMain, chatId, userId, message,
                    //     messageId);
                    return Ok();
                case false:
                    await actionService.RemoveCurrentUselessMessage(chatId, messageId);
                    return Ok();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            return BadRequest();
        }
    }
}