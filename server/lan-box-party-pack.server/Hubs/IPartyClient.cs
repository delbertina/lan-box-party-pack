using lan_box_party_pack.server.Game;

namespace lan_box_party_pack.server.Hubs;

public interface IPartyClient
{
    [SignalRServerEvent("Caller for WatchRoom; main-room group for room/game updates.")]
    Task RoomUpdated(RoomSnapshot snapshot);

    [SignalRServerEvent("Only the connection that invoked SubmitAnswer.")]
    Task AnswerResult(AnswerSubmission submission);

    [SignalRServerEvent("host-screen group; connections join this group by invoking WatchRoom.")]
    Task PlayerAnswerSubmitted(AnswerSubmission submission);

    [SignalRServerEvent("Only the player being kicked, timed out, or banned.")]
    Task PlayerDisconnected(string reason);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class SignalRServerEventAttribute(string delivery) : Attribute
{
    public string Delivery { get; } = delivery;
}
