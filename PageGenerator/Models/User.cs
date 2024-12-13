namespace PageGenerator.Models;
using Enums;

public class User(long userId)
{
    public long UserId { get; } = userId;
    public long ChatId { get; set; }
    public int Role { get; set; }
    public ActionEnum Action { get; set; }
    public ICollection<UserMessage> LastMessages { get; set; } = [];
    public ICollection<Interaction> Interactions { get; } =
    [
        new(0, InteractionEnum.None)
    ];
}