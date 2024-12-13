using System.Text.Json.Serialization;

namespace PageGenerator.Models;

public record ResponseMessage(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")]
    string Message);