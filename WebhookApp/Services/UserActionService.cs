using System.Text.Json;

namespace WebhookApp.Services;

using Models;
using Enums;
using System.Linq;

public class UserActionService(ICollection<User> users, TelegramService telegramService)
{
    private const string UsersConfigFileName = "users.config";

    private readonly long _adminId =
        Convert.ToInt64(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("admin_id"))
            ? 0
            : Environment.GetEnvironmentVariable("admin_id"));

    public async Task<ActionEnum> DefineActiveActionType(long userId, long chatId)
    {
        if (!ActorExists(userId) && !IsActorAdmin(userId))
        {
            return ActionEnum.GetUserId;
        }

        int messageId = 0;
        if (IsActorAdmin(userId) && !IsAdminInitialized())
        {
            messageId = await InitAdmin(chatId, userId);
        }

        if (!IsUserInitialized(userId) && !IsActorAdmin(userId))
        {
            messageId = await InitUser(chatId, userId);
        }

        User? actor = GetCurrentActor(userId);
        if (messageId != 0)
        {
            SetInteraction(actor!.Interactions.First(), InteractionEnum.Menu, messageId);
        }

        Action? action = actor!.Actions.FirstOrDefault(action => action.Act);
        return action?.Type ?? ActionEnum.None;
    }

    public async Task ExecuteActionFromType(ActionEnum action, long chatId, long userId, string? message,
        int currentMessageId)
    {
        User? actor = GetCurrentActor(userId);
        Interaction? lastInteraction = actor?.Interactions.LastOrDefault();
        switch (action)
        {
            case ActionEnum.None:
            {
                return;
            }
            case ActionEnum.StartPublication:
            {
                int messageId = await telegramService.StartPublication(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                ActivateAction(actor!, ActionEnum.ProcessingPublication);
                return;
            }
            case ActionEnum.ProcessingPublication:
            {
                Interaction lastKeyboardInteraction =
                    actor!.Interactions.Last(i => i.InteractionType == InteractionEnum.InlineKeyboard);
                await telegramService.RemoveKeyboard(chatId, lastKeyboardInteraction.MessageId);
                SetInteraction(lastKeyboardInteraction, InteractionEnum.Text, lastKeyboardInteraction.MessageId);
                int publicationCount = actor!.Interactions.Count(i => i.InteractionType == InteractionEnum.Post) + 1;
                string text = "Вы подготовили: " + publicationCount + " сообщение. Выберите дальнейшее действие.";
                int nextStepMessageId = await telegramService.PublicationNextStep(chatId, text);
                AddInteraction(actor!, InteractionEnum.Post, currentMessageId);
                AddInteraction(actor!, InteractionEnum.InlineKeyboard, nextStepMessageId);
                return;
            }
            case ActionEnum.RejectPublication:
            {
                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId));
                ClearInteractions(actor!);
                DeactivateAction(actor!, ActionEnum.ProcessingPublication);
                AddInteraction(actor!, InteractionEnum.Menu, messageId);
                return;
            }
            case ActionEnum.EndPublication:
            {
                int groupId = (actor!.LastMessages.LastOrDefault()?.GroupId ?? 0) + 1;
                IAsyncEnumerable<UserMessage> messages =
                    telegramService.PostMessages(actor!.Interactions, chatId, groupId);
                await foreach (UserMessage publicationMessage in messages)
                {
                    actor.LastMessages.Add(publicationMessage);
                }

                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId),
                    "Процесс публикации ваших сообщений успешно завершён. Выберете опцию:");
                ClearInteractions(actor!);
                DeactivateAction(actor, ActionEnum.ProcessingPublication);
                AddInteraction(actor!, InteractionEnum.Menu, messageId);
                return;
            }
            case ActionEnum.StartDelete:
            {
                int messageId = await telegramService.BrowsRemoveMenu(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                return;
            }
            case ActionEnum.StartDeleteLast:
            {
                int messageId = await telegramService.StartDeleteLastPublication(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                return;
            }
            case ActionEnum.EndDeleteLast:
            {
                int groupId = actor!.LastMessages.Last().GroupId;
                await telegramService.DeleteLastMessage(actor!.LastMessages.Where(m => m.GroupId == groupId));
                actor.LastMessages = actor!.LastMessages.Where(m => m.GroupId != groupId).ToList();
                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId),
                    "Вы успешно удалили последнее сообщение с каналов. Выберите опцию");
                SetInteraction(lastInteraction!, InteractionEnum.Menu, messageId);
                return;
            }
            case ActionEnum.StartDeleteByLink:
            {
                int messageId = await telegramService.StartDeletePublicationByLink(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                ActivateAction(actor!, ActionEnum.ProcessingDeleteByLink);
                break;
            }
            case ActionEnum.ProcessingDeleteByLink:
            {
                await RemoveCurrentUselessMessage(chatId, currentMessageId);
                if (!message!.Contains("https://t.me/"))
                {
                    int firstIncorrectMessageId =
                        await telegramService.BrowsIncorrectUrl(chatId, lastInteraction!.MessageId);
                    if (firstIncorrectMessageId == 0)
                    {
                        return;
                    }

                    SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, firstIncorrectMessageId);
                    return;
                }

                string[] urlParts = message.Replace("https://t.me/", "").Split('/');
                if (urlParts.Length != 2)
                {
                    int secondIncorrectMessageId =
                        await telegramService.BrowsIncorrectUrl(chatId, lastInteraction!.MessageId);
                    if (secondIncorrectMessageId == 0)
                    {
                        return;
                    }

                    SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, secondIncorrectMessageId);
                    return;
                }

                object chatIdObj = "@" + urlParts[0];
                int deletionMessageId = Convert.ToInt32(urlParts[1]);
                JsonElement result = await telegramService.RemoveMessage(chatIdObj, deletionMessageId);
                result.TryGetProperty("ok", out JsonElement statusElement);
                if (!statusElement.GetBoolean())
                {
                    JsonElement errorMessage = result.GetProperty("description");
                    int secondIncorrectMessageId =
                        await telegramService.BrowsFailedRemove(chatId, lastInteraction!.MessageId,
                            "Не удалось удалить сообщение, ответ: " + errorMessage);
                    SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, secondIncorrectMessageId);
                    return;
                }

                actor!.LastMessages = actor.LastMessages
                    .Where(m => !(m.Id == deletionMessageId && m.ChatId.Equals(chatIdObj)))
                    .ToList();
                int menuMessageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions,
                    IsActorAdmin(userId),
                    $"Сообщение из чата: {chatIdObj}, id = {deletionMessageId} было успешно удалено. Выберете опцию: ");
                SetInteraction(lastInteraction!, InteractionEnum.Menu, menuMessageId);
                DeactivateAction(actor, ActionEnum.ProcessingDeleteByLink);
                return;
            }
            case ActionEnum.GetUserId:
            {
                await telegramService.BrowsUserIdentifier(chatId, userId);
                return;
            }
            case ActionEnum.StartForward:
            {
                int messageId = await telegramService.StartForward(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                break;
            }
            case ActionEnum.StartShare:
            {
                ActivateAction(actor!, ActionEnum.ProcessingShare);
                int messageId = await telegramService.StartShare(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                return;
            }
            case ActionEnum.ProcessingShare:
            {
                Interaction lastKeyboardInteraction =
                    actor!.Interactions.Last(i =>
                        i.InteractionType is InteractionEnum.InlineKeyboard or InteractionEnum.Menu);
                await telegramService.RemoveKeyboard(chatId, lastKeyboardInteraction.MessageId);
                SetInteraction(lastKeyboardInteraction, InteractionEnum.Text, lastKeyboardInteraction.MessageId);
                int forwardCount = actor!.Interactions.Count(i => i.InteractionType == InteractionEnum.Post) + 1;
                string text = "Вы подготовили: " + forwardCount +
                              " сообщение. Выберите дальнейшее действие или продолжите создавать коллекцию.";
                int nextStepMessageId = await telegramService.ShareNextStep(chatId, text);
                AddInteraction(actor!, InteractionEnum.Post, currentMessageId);
                AddInteraction(actor!, InteractionEnum.InlineKeyboard, nextStepMessageId);
                return;
            }
            case ActionEnum.RejectSharing:
            {
                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId));
                ClearInteractions(actor!);
                DeactivateAction(actor!, ActionEnum.ProcessingShare);
                AddInteraction(actor!, InteractionEnum.Menu, messageId);
                return;
            }
            case ActionEnum.EndSharing:
            {
                int groupId = (actor!.LastMessages.LastOrDefault()?.GroupId ?? 0) + 1;
                IAsyncEnumerable<UserMessage> messages =
                    telegramService.ForwardMessages(actor!.Interactions, chatId, groupId);
                await foreach (UserMessage publicationMessage in messages)
                {
                    actor.LastMessages.Add(publicationMessage);
                }

                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId),
                    "Процесс публикации ваших сообщений успешно завершён. Выберете опцию:");
                ClearInteractions(actor!);
                DeactivateAction(actor, ActionEnum.ProcessingPublication);
                AddInteraction(actor!, InteractionEnum.Menu, messageId);
                return;
            }
            case ActionEnum.StartForwardByLink:
            {
                ActivateAction(actor!, ActionEnum.ProcessingForwardByLink);
                int messageId = await telegramService.StartForwardByLink(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                return;
            }
            case ActionEnum.ProcessingForwardByLink:
            {
                await RemoveCurrentUselessMessage(chatId, currentMessageId);
                Interaction keyboardLastInteraction =
                    actor!.Interactions.Last(i => i.InteractionType == InteractionEnum.InlineKeyboard);
                await telegramService.RemoveKeyboard(chatId, keyboardLastInteraction.MessageId);
                SetInteraction(keyboardLastInteraction, InteractionEnum.Text, keyboardLastInteraction.MessageId);
                if (!message!.Contains("https://t.me/"))
                {
                    int firstIncorrectMessageId =
                        await telegramService.BrowsIncorrectUrl(chatId, lastInteraction!.MessageId);
                    if (firstIncorrectMessageId == 0)
                    {
                        return;
                    }

                    SetInteraction(keyboardLastInteraction, InteractionEnum.InlineKeyboard, firstIncorrectMessageId);
                    return;
                }

                string[] urlParts = message.Replace("https://t.me/", "").Split('/');
                if (urlParts.Length != 2)
                {
                    int secondIncorrectMessageId =
                        await telegramService.BrowsIncorrectUrl(chatId, lastInteraction!.MessageId);
                    if (secondIncorrectMessageId == 0)
                    {
                        return;
                    }

                    SetInteraction(keyboardLastInteraction, InteractionEnum.InlineKeyboard, secondIncorrectMessageId);
                    return;
                }

                object fromChatId = "@" + urlParts[0];
                int messageId = Convert.ToInt32(urlParts[1]);
                int forwardedMessageId = await telegramService.ForwardIntoChat(fromChatId, messageId, currentMessageId);
                AddInteraction(actor!, InteractionEnum.Post, forwardedMessageId);
                int previewMessageId = await telegramService.SendPreviewMessage(chatId);
                AddInteraction(actor!, InteractionEnum.InlineKeyboard, previewMessageId);
                return;
            }
            case ActionEnum.EndForwardByLink:
            {
                break;
            }
            case ActionEnum.GetUserSettings:
            {
                int messageId = await telegramService.GetSettingsCommand(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction!, InteractionEnum.InlineKeyboard, messageId);
                return;
            }
            case ActionEnum.StartAddUser:
            {
                int messageId = await telegramService.StartAddUser(chatId, lastInteraction!.MessageId);
                if (messageId == 0)
                {
                    return;
                }

                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                ActivateAction(actor!, ActionEnum.ProcessingAddUser);
                DeactivateAction(actor!, action);
                return;
            }
            case ActionEnum.ProcessingAddUser:
            {
                int messageId = lastInteraction!.MessageId;
                await RemoveCurrentUselessMessage(chatId, currentMessageId!);
                if (long.TryParse(message, out long newUserId))
                {
                    int result = SaveActor(newUserId);
                    if (result == 0)
                    {
                        await telegramService.BrowsUserIdExists(chatId, messageId);
                        return;
                    }

                    if (!ActorExists(newUserId)) InitActor(newUserId);

                    await telegramService.BrowsSuccessAddingUser(chatId, message, messageId);
                    return;
                }

                await telegramService.BrowsWrongUserId(chatId);
                return;
            }
            case ActionEnum.EndAddUser:
            {
                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId));
                SetInteraction(lastInteraction!, InteractionEnum.Menu, messageId);
                DeactivateAction(actor, ActionEnum.ProcessingAddUser);
                return;
            }
            case ActionEnum.BackToMain:
            {
                await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId));
                DisableAllActiveActions(actor);
                return;
            }
            case ActionEnum.Help:
            {
                return;
            }
        }
    }

    public async Task RemoveCurrentUselessMessage(long chatId, int messageId) =>
        await telegramService.RemoveMessage(chatId, messageId);

    private void SetInteraction(Interaction interaction, InteractionEnum interactionType, int messageId)
    {
        interaction.MessageId = messageId;
        interaction.InteractionType = interactionType;
    }

    private void AddInteraction(User actor, InteractionEnum interactionType, int messageId) =>
        actor.Interactions.Add(new Interaction(messageId, interactionType));

    private void ClearInteractions(User actor) => actor.Interactions.Clear();
    private void ActivateAction(User actor, ActionEnum action) => actor.Actions.First(a => a.Type == action).Act = true;

    private void DeactivateAction(User actor, ActionEnum action) =>
        actor.Actions.First(a => a.Type == action).Act = false;

    private void DisableAllActiveActions(User actor) =>
        actor.Actions.Where(action => action.Act).ToList().ForEach(action => action.Act = false);

    private bool ActorExists(long userId) => users.Any(actor => actor.UserId == userId);
    private bool IsActorAdmin(long userId) => userId == _adminId;
    private bool IsAdminInitialized() => users.Any(actor => actor.UserId == _adminId);
    private bool IsUserInitialized(long userId) => users.Any(actor => actor.UserId == userId);
    private User? GetCurrentActor(long userId) => users.FirstOrDefault(actor => actor.UserId == userId);
    private void InitActor(long userId) => users.Add(new User(userId));

    private byte SaveActor(long userId)
    {
        if (ActorExists(userId))
            return 0;

        File.AppendAllLines(UsersConfigFileName, [userId.ToString()]);
        return 1;
    }

    private async Task<int> InitUser(long chatId, long userId)
    {
        if (!ActorExists(userId))
        {
            InitActor(userId);
        }

        return await telegramService.InitializedUserChatBot(chatId);
    }

    private async Task<int> InitAdmin(long chatId, long userId)
    {
        if (!ActorExists(userId))
        {
            InitActor(userId);
        }

        return await telegramService.InitializedAdminChatBot(chatId);
    }
}