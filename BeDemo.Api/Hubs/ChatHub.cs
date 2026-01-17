/*
 * ChatHub.cs - SignalR Hub for real-time chat communication
 * 
 * This hub provides WebSocket endpoint for real-time communication.
 * All methods require authentication ([Authorize] attribute).
 * 
 * Endpoint: wss://localhost:8001/hubs/chat?access_token=<JWT_TOKEN>
 * 
 * Methods:
 * - SendMessage: Sends message to all connected clients
 * - SendPrivateMessage: Sends private message to specific user
 * 
 * Events:
 * - OnConnectedAsync: Invoked when client connects
 * - OnDisconnectedAsync: Invoked when client disconnects
 * 
 * Callbacks (client can listen):
 * - ReceiveMessage: Receives message from all clients
 * - ReceivePrivateMessage: Receives private message
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BeDemo.Api.Hubs;

/// <summary>
/// SignalR Hub for chat communication
/// [Authorize] ensures that only authenticated users can access the hub
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Invoked automatically when client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        // Gets User ID from JWT token
        // Context.UserIdentifier contains value from ClaimTypes.NameIdentifier claim in JWT token
        // If UserIdentifier is not available, tries to get it directly from claims
        var userId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        _logger.LogInformation("User {UserId} connected to SignalR hub", userId);
        
        // Adds user to group "user_{userId}"
        // Groups allow sending messages to specific users or groups of users
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        
        // Calls base implementation
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Invoked automatically when client disconnects from the hub
    /// </summary>
    /// <param name="exception">Exception if disconnection was caused by error, otherwise null</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Gets User ID
        var userId = Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        // Logs disconnection (with error information if exists)
        if (exception != null)
        {
            _logger.LogWarning(exception, "User {UserId} disconnected from SignalR hub with error", userId);
        }
        else
        {
            _logger.LogInformation("User {UserId} disconnected from SignalR hub", userId);
        }
        
        // Removes user from group
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        
        // Calls base implementation
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sends message to all connected clients
    /// 
    /// This method can be called from client using:
    /// await connection.InvokeAsync("SendMessage", "Username", "Message text");
    /// </summary>
    /// <param name="user">Name of user sending the message</param>
    /// <param name="message">Message text</param>
    public async Task SendMessage(string user, string message)
    {
        // Gets sender User ID
        var userId = Context.User?.Identity?.Name ?? Context.UserIdentifier;
        
        _logger.LogInformation("User {UserId} sent message: {Message}", userId, message);
        
        // Sends message to all connected clients
        // Clients can listen on "ReceiveMessage" callback:
        // connection.On("ReceiveMessage", (string user, string message) => { ... });
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    /// <summary>
    /// Sends private message to specific user
    /// 
    /// This method can be called from client using:
    /// await connection.InvokeAsync("SendPrivateMessage", "targetUserId", "Message text");
    /// </summary>
    /// <param name="targetUserId">User ID of message recipient (from JWT token - ClaimTypes.NameIdentifier)</param>
    /// <param name="message">Message text</param>
    public async Task SendPrivateMessage(string targetUserId, string message)
    {
        // Gets sender User ID
        var userId = Context.User?.Identity?.Name ?? Context.UserIdentifier;
        
        _logger.LogInformation("User {UserId} sent private message to {TargetUserId}", userId, targetUserId);
        
        // Sends message only to specific user
        // Clients.User() finds all connections of given user and sends message to all of them
        // Client can listen on "ReceivePrivateMessage" callback:
        // connection.On("ReceivePrivateMessage", (string sender, string message) => { ... });
        await Clients.User(targetUserId).SendAsync("ReceivePrivateMessage", userId, message);
    }
}
