using Microsoft.OpenApi.Models;
using WebhookApp.Models;
using WebhookApp.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Red Telegram API",
        Version = "v1",
        Description = "Webhooks are visible in the documentation but cannot be executed via Swagger UI."
    });
});

builder.Services.AddControllers();
builder.Services.AddHttpClient("TelegramApiClient");
builder.Services.AddSingleton<TelegramService>();
builder.Services.AddSingleton(sp =>
{
    TelegramService telegramService = sp.GetRequiredService<TelegramService>();
    _ = telegramService.SetWebhook();
    ICollection<string> userConfig = File.ReadLines("users.config").ToList();
    ICollection<User> users = userConfig.Select(uc => new User(Convert.ToInt64(uc))).ToList();
    return new UserActionService(users, telegramService);
});

WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});
app.UseHttpsRedirection();
app.MapControllers();

app.Run();