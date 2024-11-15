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

    public async Task ExecuteActionFromType(ActionEnum action, long chatId, long userId, string? message, int currentMessageId)
    {
        User? actor = GetCurrentActor(userId);
        Interaction? previousInteraction = actor?.Interactions.LastOrDefault();
        switch (action)
        {
            case ActionEnum.None:
                return;
            case ActionEnum.StartPost:
                ActivateAction(ActionEnum.ProcessingPost, userId);
                await telegramService.SendStartPost(chatId);
                return;
            case ActionEnum.ProcessingPost:
                break;
            case ActionEnum.StartDeleteByLink:
                break;
            case ActionEnum.StartAddUser:
            {
                int messageId = await telegramService.StartAddUser(chatId, previousInteraction!.MessageId);
                SetInteraction(previousInteraction, InteractionEnum.InlineKeyboard, messageId);
                ActivateAction(ActionEnum.ProcessingAddUser, userId);
                break;
            }
            case ActionEnum.ProcessingAddUser:
            {
                int messageId = actor!.Interactions.Last().MessageId;
                await RemoveCurrentUselessMessage(chatId, (int)currentMessageId!);
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
                SetInteraction(previousInteraction!, InteractionEnum.Menu, messageId);
                DeactivateAction(ActionEnum.ProcessingAddUser, userId);
                break;
            }
            case ActionEnum.GetUserId:
                await telegramService.BrowsUserIdentifier(chatId, userId);
                return;
            case ActionEnum.EndPost:
                break;
            case ActionEnum.StartForward:
                ActivateAction(ActionEnum.ProcessingPost, userId);
                await telegramService.StartForward(chatId);
                break;
            case ActionEnum.EndForward:
                break;
            case ActionEnum.GetUserSettings:
                await telegramService.GetSettingsCommand(chatId, previousInteraction!.MessageId);
                break;
            case ActionEnum.BackToMain:
                await telegramService.BrowsMainMenu(chatId, actor!.Interactions, IsActorAdmin(userId));
                DisableAllActiveActions(actor);
                return;
            case ActionEnum.Help:
                return;
        }

        if (action is (ActionEnum.ProcessingPost or
            ActionEnum.ProcessingShare or
            ActionEnum.ProcessingAddUser or
            ActionEnum.ProcessingForwardByLink or
            ActionEnum.ProcessingDeleteUser))
        {
            return;
        }

        DeactivateAction(action, userId);
    }

    public async Task RemoveCurrentUselessMessage(long chatId, int messageId) => await telegramService.RemoveMessage(chatId, messageId);

    private void SetInteraction(Interaction interaction, InteractionEnum interactionType, int messageId)
    {
        interaction.MessageId = messageId;
        interaction.InteractionType = interactionType;
    }

    private void AddInteraction(User actor, InteractionEnum interactionType, int messageId) =>
        actor.Interactions.Add(new Interaction(messageId, interactionType));

    private void ClearInteractions(User actor)
    {
        actor.Interactions.Clear();
        actor.Interactions.Add(new Interaction(0, InteractionEnum.None));
    }

    private void ActivateAction(ActionEnum action, long userId) => EnableAction(GetCurrentActor(userId)!, action);
    private void DeactivateAction(ActionEnum action, long userId) => DisableAction(GetCurrentActor(userId)!, action);
    private void EnableAction(User actor, ActionEnum action) => actor.Actions.First(a => a.Type == action).Act = true;
    private void DisableAction(User actor, ActionEnum action) => actor.Actions.First(a => a.Type == action).Act = false;

    private void DisableAllActiveActions(User actor)
    {
        foreach (Action action in actor.Actions)
        {
            if (action.Act)
            {
                action.Act = false;
            }
        }
    }

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