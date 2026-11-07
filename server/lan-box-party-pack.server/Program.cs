using lan_box_party_pack.server.Hubs;
using lan_box_party_pack.server.Game;
using lan_box_party_pack.server.Players;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()
    );
});

builder.Services.AddSingleton<PlayerManager>();
builder.Services.AddSingleton<GameManager>();

var app = builder.Build();
    
app.MapHub<PartyHub>("/partyHub");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "lan-box-party-pack server is running.");
app.MapGet("/livez", () => Results.Ok());
app.MapGet("/readyz", () => Results.Ok());

app.Run();
