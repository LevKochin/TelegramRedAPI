namespace WebhookApp.Models;
using Enums;

public class Action(ActionEnum type)
{
    public ActionEnum Type { get; } = type;
    public bool Act { get; set; }
}