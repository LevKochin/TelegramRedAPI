namespace PageGenerator.Services;

using Enums;
using Models;

public class UserActionService(TelegramService telegramService, DataService dataService)
{
    public async Task<ActionEnum> DefineUserAction(long userId, long chatId) => await dataService.DefineUserAction(userId, chatId);
    public async Task RedefineAction(long userId, ActionEnum actionType) => await dataService.UpdateUserAction(userId, actionType);
    
    // Сейчас держится в голове, что есть процессы с сохранением состояния, а есть преходящие, без сохранения состояния процессы
    // Первые сохраняются в базу со структурой связей, а вторые не сохраняются, потому что перетекают в процессе в другие процессы
    public async Task ExecuteActionFromType(ActionEnum action, long chatId, long userId, string? message,
        int currentMessageId)
    {
        switch (action)
        {
            case ActionEnum.NoChat:
            {
                // Вывести кнопки, которые будут ссылаться на пользователей, чтобы с ними связаться
                return;
            }
            case ActionEnum.StartGenerationPage:
            {
                int interactionMessageId = await dataService.GetInteractionMessageId(userId, chatId);
                int messageId = await telegramService.StartPageGenerating(chatId, interactionMessageId);
                return;
            }
            case ActionEnum.BackToMainMenu:
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
}