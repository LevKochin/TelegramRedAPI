using WebhookApp.Models;
using WebhookApp.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpClient("TelegramApiClient");
builder.Services.AddSingleton<TelegramService>();
builder.Services.AddSingleton(sp =>
{
    TelegramService telegramService = sp.GetRequiredService<TelegramService>();
    ICollection<string> userConfig = File.ReadLines("users.config").ToList();
    ICollection<User> users = userConfig.Select(uc => new User(Convert.ToInt64(uc))).ToList();
    return new UserActionService(users, telegramService);
});

WebApplication app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();