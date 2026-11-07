using Microsoft.AspNetCore.SignalR;

namespace lan_box_party_pack.server.Hubs
{
    public class PartyHub : Hub
    {
        public async Task JoinRoom(string roomCode, string playerName)
        {   
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            await Clients.Group(roomCode).SendAsync("PlayerJoined", playerName);
        }

        public async Task PressButton(string roomCode, string playerName)
        {
            await Clients.Group(roomCode).SendAsync("PlayerPressed", playerName);
        }
    }
}
