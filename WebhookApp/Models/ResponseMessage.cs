namespace WebhookApp.Models;
using System.Text.Json.Serialization;

public record ResponseMessage(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")]
    string Message);