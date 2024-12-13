using PageGenerator.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<TelegramService>();
builder.Services.AddScoped<UserActionService>();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
