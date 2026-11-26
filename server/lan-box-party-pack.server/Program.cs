using lan_box_party_pack.server.Game;
using lan_box_party_pack.server.Hubs;
using lan_box_party_pack.server.Players;
using lan_box_party_pack.server.Questions;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "lan-box-party-pack SignalR contract",
        Version = "v1",
        Description = "SignalR hub invocation documentation for /partyHub. These generated operations describe SignalR client invocations; they are not HTTP endpoints. Connect with a SignalR client and invoke the listed hub methods. Server-to-client events are RoomUpdated (RoomSnapshot), AnswerResult (AnswerSubmission, caller only), and PlayerAnswerSubmitted (AnswerSubmission, host-screen group)."
    });
    options.AddSignalRSwaggerGen(signalR => signalR.ScanAssemblies(typeof(PartyHub).Assembly));
    options.DocumentFilter<SignalRServerEventsDocumentFilter>();
});
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()
    );
});

builder.Services.AddSingleton<PlayerManager>();
builder.Services.AddSingleton<GameManager>();
builder.Services.AddSingleton<QuestionRepository>();

var app = builder.Build();
    
app.MapHub<PartyHub>("/partyHub");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SupportedSubmitMethods());
}

app.UseHttpsRedirection();

app.MapGet("/", () => "lan-box-party-pack server is running.").ExcludeFromDescription();
app.MapGet("/livez", () => Results.Ok()).ExcludeFromDescription();
app.MapGet("/readyz", () => Results.Ok()).ExcludeFromDescription();

app.Run();
