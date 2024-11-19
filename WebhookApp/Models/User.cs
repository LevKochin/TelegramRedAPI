namespace WebhookApp.Models;

using Enums;

public class User(long userId)
{
    public long UserId { get; } = userId;
    
    /*
     TODO: Определить - будут ли сохраняться все сообщения для их
     последующего удаления по очереди, где каждое удаление - последнее
     */
    public List<UserMessage> LastMessages { get; set; } = [];

    public ICollection<Interaction> Interactions { get; } =
    [
        new(0, InteractionEnum.None)
    ];

    public IEnumerable<Action> Actions { get; } =
    [
        new(ActionEnum.StartPublication),
        new(ActionEnum.ProcessingPublication),
        new(ActionEnum.EndPublication),
        new(ActionEnum.StartForward),
        new(ActionEnum.StartShare),
        new(ActionEnum.StartForwardByLink),
        new(ActionEnum.ProcessingShare),
        new(ActionEnum.RejectSharing),
        new(ActionEnum.ProcessingForwardByLink),
        new(ActionEnum.EndSharing),
        new(ActionEnum.StartDelete),
        new(ActionEnum.StartDeleteLast),
        new(ActionEnum.StartDeleteByLink),
        new(ActionEnum.ProcessingDeleteByLink),
        new(ActionEnum.EndDeleteLast),
        new(ActionEnum.GetUserSettings),
        new(ActionEnum.StartAddUser),
        new(ActionEnum.ProcessingAddUser),
        new(ActionEnum.EndAddUser),
        new(ActionEnum.StartDeleteUser),
        new(ActionEnum.ProcessingDeleteUser),
        new(ActionEnum.BackToMain),
        new(ActionEnum.Help),
    ];
}