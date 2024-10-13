using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace SignalRChat.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> GroupsPasscodes = new();
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<(string User, string Message, DateTime SentAt)>> GroupMessages = new();

        public async Task AddToGroup(string group, string passcode)
        {
            try
            {
                if (GroupsPasscodes.TryGetValue(group, out var storedPasscode) && storedPasscode == passcode)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, group);
                    await Clients.Group(group).SendAsync("Notification", $"{Context.ConnectionId} has joined the group {group}.");
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "Invalid passcode for the group.");
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"An error occurred: {ex.Message}");
            }
        }

        public async Task RemoveFromGroup(string group)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
                await Clients.Group(group).SendAsync("Notification", $"{Context.ConnectionId} has left the group {group}.");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"An error occurred: {ex.Message}");
            }
        }

        public async Task SendMessageToGroup(string group, string message)
        {
            try
            {
                var user = Context.ConnectionId;
                var sentAt = DateTime.UtcNow;

                // Store the message in the group's message queue
                var groupMessages = GroupMessages.GetOrAdd(group, new ConcurrentQueue<(string User, string Message, DateTime SentAt)>());
                groupMessages.Enqueue((user, message, sentAt));

                await Clients.Group(group).SendAsync("ReceiveMessage", user, message, sentAt);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"An error occurred: {ex.Message}");
            }
        }

        public Task CreateGroup(string id, string passcode)
        {
            try
            {
                if (GroupsPasscodes.TryAdd(id, passcode))
                {
                    return Clients.Caller.SendAsync("Notification", $"Group {id} created successfully.");
                }
                else
                {
                    return Clients.Caller.SendAsync("Error", $"Group {id} already exists.");
                }
            }
            catch (Exception ex)
            {
                return Clients.Caller.SendAsync("Error", $"An error occurred: {ex.Message}");
            }
        }

        public async Task GetGroupMessages(string group)
        {
            try
            {
                if (GroupMessages.TryGetValue(group, out var groupMessages))
                {
                    await Clients.Caller.SendAsync("GroupMessages", groupMessages.ToArray());
                }
                else
                {
                    await Clients.Caller.SendAsync("GroupMessages", Array.Empty<(string User, string Message, DateTime SentAt)>());
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"An error occurred: {ex.Message}");
            }
        }
    }
}