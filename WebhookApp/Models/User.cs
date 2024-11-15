namespace WebhookApp.Models;

using Enums;

public class User(long userId)
{
    public long UserId { get; } = userId;

    public ICollection<Interaction> Interactions { get; } =
    [
        new Interaction(0, InteractionEnum.None)
    ];

    public IEnumerable<Action> Actions { get; } =
    [
        new Action(ActionEnum.StartPost),
        new Action(ActionEnum.ProcessingPost),
        new Action(ActionEnum.EndPost),
        new Action(ActionEnum.StartForward),
        new Action(ActionEnum.StartShare),
        new Action(ActionEnum.StartForwardByLink),
        new Action(ActionEnum.ProcessingShare),
        new Action(ActionEnum.ProcessingForwardByLink),
        new Action(ActionEnum.EndForward),
        new Action(ActionEnum.StartDelete),
        new Action(ActionEnum.StartDeleteLast),
        new Action(ActionEnum.StartDeleteByLink),
        new Action(ActionEnum.StartAddUser),
        new Action(ActionEnum.ProcessingAddUser),
        new Action(ActionEnum.EndAddUser),
        new Action(ActionEnum.StartDeleteUser),
        new Action(ActionEnum.GetUserSettings),
        new Action(ActionEnum.BackToMain),
        new Action(ActionEnum.Help),
    ];
}