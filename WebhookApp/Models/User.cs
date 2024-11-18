namespace WebhookApp.Models;

using Enums;

public class User(long userId)
{
    public long UserId { get; } = userId;
    
    /*
     TODO: Определить - будут ли сохраняться все сообщения для их
     последующего удаления по очереди, где каждое удаление - последнее
     */
    public List<int> LastMessagesIds { get; set; } = [];

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
        new(ActionEnum.ProcessingForwardByLink),
        new(ActionEnum.EndForward),
        new(ActionEnum.StartDelete),
        new(ActionEnum.StartDeleteLast),
        new(ActionEnum.StartDeleteByLink),
        new(ActionEnum.EndDeleteLast),
        new(ActionEnum.EndDeleteByLink),
        new(ActionEnum.StartAddUser),
        new(ActionEnum.ProcessingAddUser),
        new(ActionEnum.EndAddUser),
        new(ActionEnum.StartDeleteUser),
        new(ActionEnum.GetUserSettings),
        new(ActionEnum.BackToMain),
        new(ActionEnum.Help),
    ];
}