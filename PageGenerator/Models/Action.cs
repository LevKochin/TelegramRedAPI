namespace PageGenerator.Models;
using Enums;

public class Action(ActionEnum type)
{
    public ActionEnum Type { get; } = type;
    public byte IsProcess { get; set; } = 0;
}