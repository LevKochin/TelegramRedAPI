namespace WebhookApp.Models;

public class UserMessage
{
    public int Id { get; init; }
    public object ChatId { get; init; }
    public int GroupId { get; init; }
}