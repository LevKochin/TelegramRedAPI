namespace WebhookApp.Services;

using Models;
using Enums;

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
                ActivateAction(ActionEnum.ProcessingPublication, userId);
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
                actor.LastMessagesIds.Add(currentMessageId);
                AddInteraction(actor!, InteractionEnum.Post, currentMessageId);
                AddInteraction(actor!, InteractionEnum.InlineKeyboard, nextStepMessageId);
                break;
            }
            case ActionEnum.RejectPublication:
            {
                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId));
                ClearInteractions(actor!);
                DeactivateAction(ActionEnum.ProcessingPublication, userId);
                AddInteraction(actor!, InteractionEnum.Menu, messageId);
                return;
            }
            case ActionEnum.EndPublication:
            {
                await telegramService.PublicateMessages(actor!.Interactions, chatId);
                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId),
                    "Процесс публикации ваших сообщений успешно завершён. Выбериете опцию:");
                ClearInteractions(actor!);
                DeactivateAction(ActionEnum.ProcessingPublication, userId);
                AddInteraction(actor!, InteractionEnum.Menu, messageId);
                break;
            }
            case ActionEnum.StartDelete:
            {
                int messageId = await telegramService.BrowsRemoveMenu(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                break;
            }
            case ActionEnum.StartDeleteLast:
            {
                int messageId = await telegramService.StartDeleteLastPublication(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                break;
            }
            case ActionEnum.EndDeleteLast:
            {
                await telegramService.DeleteLastMessage(actor!.LastMessagesIds);
                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId),
                    "Вы успешно удалили последнее сообщение с каналов. Выберите опцию");
                SetInteraction(lastInteraction!, InteractionEnum.Menu, messageId);
                break;
            }
            case ActionEnum.StartDeleteByLink:
            {
                int messageId = await telegramService.StartDeletePublicationByLink(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                break;
            }
            case ActionEnum.GetUserId:
            {
                await telegramService.BrowsUserIdentifier(chatId, userId);
                return;
            }
            case ActionEnum.StartForward:
            {
                ActivateAction(ActionEnum.ProcessingPublication, userId);
                await telegramService.StartForward(chatId);
                break;
            }
            case ActionEnum.EndForward:
            {
                break;
            }
            case ActionEnum.GetUserSettings:
            {
                await telegramService.GetSettingsCommand(chatId, lastInteraction!.MessageId);
                break;
            }
            case ActionEnum.StartAddUser:
            {
                int messageId = await telegramService.StartAddUser(chatId, lastInteraction!.MessageId);
                SetInteraction(lastInteraction, InteractionEnum.InlineKeyboard, messageId);
                ActivateAction(ActionEnum.ProcessingAddUser, userId);
                break;
            }
            case ActionEnum.ProcessingAddUser:
            {
                int messageId = actor!.Interactions.Last().MessageId;
                await RemoveCurrentUselessMessage(chatId, currentMessageId!);
                if (long.TryParse(message, out long newUserId))
                {
                    int result = SaveActor(newUserId);
                    if (result == 0)
                    {
                        await telegramService.BrowsUserIdExists(chatId, messageId);
                        break;
                    }

                    if (!ActorExists(newUserId)) InitActor(newUserId);

                    await telegramService.BrowsSuccessAddingUser(chatId, message, messageId);
                    break;
                }

                await telegramService.BrowsWrongUserId(chatId);
                return;
            }
            case ActionEnum.EndAddUser:
            {
                int messageId = await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId));
                SetInteraction(lastInteraction!, InteractionEnum.Menu, messageId);
                DeactivateAction(ActionEnum.ProcessingAddUser, userId);
                break;
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

        if (action is (ActionEnum.ProcessingPublication or
            ActionEnum.ProcessingShare or
            ActionEnum.ProcessingAddUser or
            ActionEnum.ProcessingForwardByLink or
            ActionEnum.ProcessingDeleteUser))
        {
            return;
        }

        DeactivateAction(action, userId);
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
    private void ActivateAction(ActionEnum action, long userId) => EnableAction(GetCurrentActor(userId)!, action);
    private void DeactivateAction(ActionEnum action, long userId) => DisableAction(GetCurrentActor(userId)!, action);
    private void EnableAction(User actor, ActionEnum action) => actor.Actions.First(a => a.Type == action).Act = true;
    private void DisableAction(User actor, ActionEnum action) => actor.Actions.First(a => a.Type == action).Act = false;

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