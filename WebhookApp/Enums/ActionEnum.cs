namespace WebhookApp.Enums;

public enum ActionEnum
{
    None = 0,
    StartPost = 1,
    ProcessingPost = 10,
    EndPost = 11,
    StartForward = 2,
    StartShare = 20,
    ProcessingShare = 21,
    StartForwardByLink = 22,
    ProcessingForwardByLink = 23,
    EndForward = 24,
    StartDelete = 3,
    StartDeleteLast = 30,
    StartDeleteByLink = 31,
    StartAddUser = 4,
    ProcessingAddUser = 40,
    EndAddUser = 41,
    StartDeleteUser = 5,
    ProcessingDeleteUser = 50,
    GetUserSettings = 6,
    GetUserId = 7,
    BackToMain = 8,
    Help = 9
}