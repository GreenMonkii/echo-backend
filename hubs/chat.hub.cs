using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace SignalRChat.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> GroupsPasscodes = new();
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<(string User, string Message, DateTime SentAt)>> GroupMessages = new();
        private const int MaxMessagesPerGroup = 100; // Limit message history per group
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        public async Task AddToGroup(string group, string passcode)
        {
            try
            {
                // Input validation
                if (string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(passcode))
                {
                    await Clients.Caller.SendAsync("Error", "Group name and passcode cannot be empty.");
                    return;
                }

                if (GroupsPasscodes.TryGetValue(group, out var storedPasscode) && storedPasscode == passcode)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, group);
                    _logger.LogInformation("User {ConnectionId} joined group {Group}", Context.ConnectionId, group);
                    await Clients.Group(group).SendAsync("Notification", $"{Context.ConnectionId} has joined the group {group}.");
                }
                else
                {
                    _logger.LogWarning("Failed join attempt for group {Group} from {ConnectionId}", group, Context.ConnectionId);
                    await Clients.Caller.SendAsync("Error", "Invalid passcode for the group.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {ConnectionId} to group {Group}", Context.ConnectionId, group);
                await Clients.Caller.SendAsync("Error", $"An error occurred: {ex.Message}");
            }
        }

        public async Task RemoveFromGroup(string group)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(group))
                {
                    await Clients.Caller.SendAsync("Error", "Group name cannot be empty.");
                    return;
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
                _logger.LogInformation("User {ConnectionId} left group {Group}", Context.ConnectionId, group);
                await Clients.Group(group).SendAsync("Notification", $"{Context.ConnectionId} has left the group {group}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {ConnectionId} from group {Group}", Context.ConnectionId, group);
                await Clients.Caller.SendAsync("Error", $"An error occurred: {ex.Message}");
            }
        }

        public async Task SendMessageToGroup(string group, string message)
        {
            try
            {
                // Input validation
                if (string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(message))
                {
                    await Clients.Caller.SendAsync("Error", "Group name and message cannot be empty.");
                    return;
                }

                // Message length limit
                if (message.Length > 1000)
                {
                    await Clients.Caller.SendAsync("Error", "Message too long. Maximum 1000 characters allowed.");
                    return;
                }

                var user = Context.ConnectionId;
                var sentAt = DateTime.UtcNow;

                // Store the message in the group's message queue
                var groupMessages = GroupMessages.GetOrAdd(group, new ConcurrentQueue<(string User, string Message, DateTime SentAt)>());
                groupMessages.Enqueue((user, message.Trim(), sentAt));

                // Limit message history to prevent memory issues
                while (groupMessages.Count > MaxMessagesPerGroup)
                {
                    groupMessages.TryDequeue(out _);
                }

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
                // Input validation
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(passcode))
                {
                    return Clients.Caller.SendAsync("Error", "Group ID and passcode cannot be empty.");
                }

                // Length validation
                if (id.Length > 50 || passcode.Length > 50)
                {
                    return Clients.Caller.SendAsync("Error", "Group ID and passcode must be 50 characters or less.");
                }

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
                if (string.IsNullOrWhiteSpace(group))
                {
                    await Clients.Caller.SendAsync("Error", "Group name cannot be empty.");
                    return;
                }

                if (GroupMessages.TryGetValue(group, out var groupMessages))
                {
                    var messages = groupMessages.ToArray();
                    _logger.LogInformation("Retrieved {Count} messages for group {Group}", messages.Length, group);
                    await Clients.Caller.SendAsync("GroupMessages", messages);
                }
                else
                {
                    await Clients.Caller.SendAsync("GroupMessages", Array.Empty<(string User, string Message, DateTime SentAt)>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages for group {Group}", group);
                await Clients.Caller.SendAsync("Error", $"An error occurred: {ex.Message}");
            }
        }

        // Connection lifecycle management
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client {ConnectionId} connected from {UserAgent}",
                Context.ConnectionId, Context.GetHttpContext()?.Request.Headers.UserAgent);

            // Send initial connection confirmation
            await Clients.Caller.SendAsync("Connected", new
            {
                ConnectionId = Context.ConnectionId,
                ServerTime = DateTime.UtcNow
            });

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error: {ErrorMessage}",
                    Context.ConnectionId, exception.Message);
            }
            else
            {
                _logger.LogInformation("Client {ConnectionId} disconnected gracefully", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Add a ping method to help with connection health
        public async Task Ping()
        {
            await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
        }
    }
}