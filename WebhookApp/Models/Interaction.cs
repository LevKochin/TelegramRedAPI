using WebhookApp.Enums;

namespace WebhookApp.Models;

public class Interaction(int messageId, InteractionEnum interactionType)
{
    public int MessageId { get; set; } = messageId;
    public InteractionEnum InteractionType { get; set; } = interactionType;
}