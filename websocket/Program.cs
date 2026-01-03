using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// ========== SERVER INITIALIZATION ==========
HttpListener listener = new HttpListener();
listener.Prefixes.Add("http://localhost:5000/ws/");
listener.Start();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine(@"==========================================================
   _   _            _____ _           _   
  | \ | |          / ____| |         | |  
  |  \| | _____  _| |    | |__   __ _| |_ 
  | . ` |/ _ \ \/ / |    | '_ \ / _` | __|
  | |\  |  __/>  <| |____| | | | (_| | |_ 
  |_| \_|\___/_/\_\\_____|_| |_|\__,_|\__|
                                          
                NEXCHAT SERVER v2.0
==========================================================");
Console.ResetColor();

Console.WriteLine($"SERVER STARTED: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"ENDPOINT: ws://localhost:5000/ws/");
Console.WriteLine(new string('=', 60));

// ========== DATA STRUCTURES ==========
var activeClients = new Dictionary<string, WebSocket>();
var userLastSeen = new Dictionary<string, DateTime>();
var groups = new Dictionary<string, GroupInfo>();
var serverLock = new object();
var messageSequences = new Dictionary<string, long>();
var deliveryAcks = new Dictionary<string, DateTime>();
var readReceipts = new Dictionary<string, DateTime>();
var messageQueues = new Dictionary<string, Queue<Packet>>();
var userConnections = new Dictionary<string, DateTime>(); // Track all user connections

// ========== FAULT DETECTION SETTINGS ==========
const int HEARTBEAT_TIMEOUT_SECONDS = 30;
const int HEARTBEAT_INTERVAL_MS = 15000;

// ========== PERSISTENT HISTORY SETTINGS ==========
const string HISTORY_DIRECTORY = "ChatHistory";
const int MAX_MESSAGES_PER_CHAT = 1000;

// ========== START HISTORY SERVICE ==========
InitializeHistoryService();

// ========== START FAULT DETECTION ==========
_ = Task.Run(FaultDetectionService);

// ========== MAIN SERVER LOOP ==========
while (true)
{
    try
    {
        var context = await listener.GetContextAsync();
        if (context.Request.IsWebSocketRequest)
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            _ = Task.Run(() => HandleClientConnection(wsContext.WebSocket));
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] Server error: {ex.Message}");
        Console.ResetColor();
    }
}

// ========== INITIALIZATION METHODS ==========
void InitializeHistoryService()
{
    try
    {
        if (!Directory.Exists(HISTORY_DIRECTORY))
        {
            Directory.CreateDirectory(HISTORY_DIRECTORY);
            Console.WriteLine($"[HISTORY] Created directory: {HISTORY_DIRECTORY}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] History init failed: {ex.Message}");
    }
}

// ========== FAULT DETECTION SERVICE ==========
async Task FaultDetectionService()
{
    while (true)
    {
        try
        {
            await Task.Delay(HEARTBEAT_INTERVAL_MS);

            List<string> deadClients = new List<string>();
            DateTime cutoff = DateTime.Now.AddSeconds(-HEARTBEAT_TIMEOUT_SECONDS);

            lock (serverLock)
            {
                foreach (var client in userLastSeen)
                {
                    if (client.Value < cutoff && activeClients.ContainsKey(client.Key))
                    {
                        deadClients.Add(client.Key);
                    }
                }
            }

            foreach (var deadClient in deadClients)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[FAULT DETECTION] {deadClient} disconnected (heartbeat timeout)");
                Console.ResetColor();
                await HandleClientDisconnect(deadClient);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Fault detection error: {ex.Message}");
        }
    }
}

// ========== CLIENT CONNECTION HANDLER ==========
async Task HandleClientConnection(WebSocket socket)
{
    string clientId = "";
    var buffer = new byte[8192];

    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var packet = JsonSerializer.Deserialize<Packet>(json);

            if (packet == null) continue;

            clientId = await ProcessPacket(packet, socket, clientId);

            lock (serverLock)
            {
                if (!string.IsNullOrEmpty(clientId) && userLastSeen.ContainsKey(clientId))
                {
                    userLastSeen[clientId] = DateTime.Now;
                }
            }
        }
    }
    catch (WebSocketException)
    {
        Console.WriteLine($"[INFO] {clientId} disconnected");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {clientId}: {ex.Message}");
        Console.ResetColor();
    }
    finally
    {
        if (!string.IsNullOrEmpty(clientId))
            await HandleClientDisconnect(clientId);
    }
}

// ========== PACKET PROCESSOR ==========
async Task<string> ProcessPacket(Packet packet, WebSocket socket, string currentClientId)
{
    switch (packet.Type)
    {
        case "CONN":
            return await HandleUserConnection(packet.From, socket);

        case "HEARTBEAT":
            return currentClientId;

        case "GET_STATUS":
            await SendSyncData(packet.From);
            break;

        case "GRP_CREATE":
            await HandleCreateGroup(packet.From, packet.Content);
            break;

        case "GRP_JOIN":
            await HandleJoinGroup(packet.From, packet.To);
            break;

        case "GRP_LEAVE":
            await HandleLeaveGroup(packet.From, packet.To);
            break;

        case "GRP_KICK":
            await HandleKickMember(packet.From, packet.To, packet.Content);
            break;

        case "GRP_SET_ADMIN":
            await HandleSetAdmin(packet.From, packet.To, packet.Content);
            break;

        case "GRP_DELETE":
            await HandleDeleteGroup(packet.From, packet.To);
            break;

        case "GRP_MSG":
            await HandleGroupMessage(packet);
            break;

        case "PRIV":
            await HandlePrivateMessage(packet);
            break;

        case "GROUP_PRIV":  // NEW: Private message to specific group member
            await HandleGroupPrivateMessage(packet);
            break;

        case "CLEAR_CHAT":
            await HandleClearChat(packet.From, packet.To, packet.Content);
            break;

        case "MSG_DELIVERED":
            await HandleMessageDelivered(packet.From, packet.Content);
            break;

        case "MSG_READ":
            await HandleMessageRead(packet.From, packet.Content);
            break;

        case "GET_HISTORY":
            await HandleGetHistory(packet.From, packet.Content);
            break;

        case "TYPING":
            await HandleTypingIndicator(packet);
            break;

        case "GET_GROUP_MEMBERS":  // NEW: Get members of a specific group
            await HandleGetGroupMembers(packet.From, packet.Content);
            break;
    }

    return currentClientId;
}

// ========== NEW: GET GROUP MEMBERS HANDLER ==========
async Task HandleGetGroupMembers(string username, string groupName)
{
    List<string> members = new List<string>();

    lock (serverLock)
    {
        if (groups.ContainsKey(groupName))
        {
            members = groups[groupName].Members.ToList();
        }
    }

    // Send members list back to requester
    await SendToImmediate(username, new Packet
    {
        Type = "GROUP_MEMBERS",
        From = "SYSTEM",
        Content = groupName,
        GroupList = string.Join(",", members),
        Time = DateTime.Now
    });
}

// ========== NEW: GROUP PRIVATE MESSAGE HANDLER ==========
async Task HandleGroupPrivateMessage(Packet packet)
{
    // packet.Content format: "groupName|targetUser|message"
    var parts = packet.Content.Split('|', 3);
    if (parts.Length != 3) return;

    string groupName = parts[0];
    string targetUser = parts[1];
    string message = parts[2];

    // Verify sender is in the group
    bool senderInGroup = false;
    lock (serverLock)
    {
        if (groups.ContainsKey(groupName))
        {
            senderInGroup = groups[groupName].Members.Contains(packet.From);
        }
    }

    if (!senderInGroup)
    {
        await SendToImmediate(packet.From, new Packet
        {
            Type = "ERROR",
            From = "SYSTEM",
            Content = $"You are not a member of group '{groupName}'",
            Time = DateTime.Now
        });
        return;
    }

    // Create private message packet
    var privatePacket = new Packet
    {
        Type = "PRIV",
        From = packet.From,
        To = targetUser,
        Content = $"[Group: {groupName}] {message}",
        MessageId = $"{packet.From}_{DateTime.Now.Ticks}_{Guid.NewGuid().ToString("N")}",
        SentTime = DateTime.Now
    };

    // Handle as regular private message
    await HandlePrivateMessage(privatePacket);
}

// ========== MESSAGE DELIVERED HANDLER ==========
async Task HandleMessageDelivered(string username, string messageId)
{
    var parts = messageId.Split('_');
    if (parts.Length >= 2)
    {
        string sender = parts[0];
        if (activeClients.ContainsKey(sender))
        {
            await SendToImmediate(sender, new Packet
            {
                Type = "MSG_DELIVERED",
                From = "SYSTEM",
                Content = messageId,
                Time = DateTime.Now
            });
        }
    }

    lock (serverLock)
    {
        if (deliveryAcks.ContainsKey(messageId))
        {
            deliveryAcks[messageId] = DateTime.Now;
        }
    }
}

// ========== MESSAGE READ HANDLER ==========
async Task HandleMessageRead(string username, string messageId)
{
    var parts = messageId.Split('_');
    if (parts.Length >= 2)
    {
        string sender = parts[0];
        if (activeClients.ContainsKey(sender))
        {
            await SendToImmediate(sender, new Packet
            {
                Type = "MSG_READ",
                From = "SYSTEM",
                Content = messageId,
                Time = DateTime.Now
            });
        }
    }

    lock (serverLock)
    {
        readReceipts[messageId] = DateTime.Now;
    }
}

// ========== TYPING INDICATOR HANDLER ==========
async Task HandleTypingIndicator(Packet packet)
{
    if (activeClients.ContainsKey(packet.To))
    {
        await SendToImmediate(packet.To, packet);
    }
}

// ========== HISTORY RETRIEVAL HANDLER ==========
async Task HandleGetHistory(string username, string chatInfo)
{
    string history = LoadChatHistory(chatInfo);

    await SendToImmediate(username, new Packet
    {
        Type = "HISTORY_DATA",
        From = "SYSTEM",
        Content = history,
        Time = DateTime.Now
    });
}

// ========== CONNECTION HANDLER WITH JOIN NOTIFICATION ==========
async Task<string> HandleUserConnection(string username, WebSocket socket)
{
    bool isNewConnection = false;

    lock (serverLock)
    {
        isNewConnection = !userConnections.ContainsKey(username);
        userConnections[username] = DateTime.Now;
        activeClients[username] = socket;
        userLastSeen[username] = DateTime.Now;

        if (!messageQueues.ContainsKey(username))
        {
            messageQueues[username] = new Queue<Packet>();
        }
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[CONNECT] {username} connected");
    Console.ResetColor();

    // Process any queued messages immediately
    await ProcessMessageQueue(username);

    await SendSyncData(username);

    // Notify all users about new connection (except the connecting user)
    if (isNewConnection)
    {
        await BroadcastSystemAlertImmediate($"{username} has joined the chat");
    }
    else
    {
        await BroadcastSystemAlertImmediate($"{username} is back online");
    }

    return username;
}

// ========== MESSAGE QUEUE PROCESSING ==========
async Task ProcessMessageQueue(string username)
{
    List<Packet> messagesToSend = new List<Packet>();

    lock (serverLock)
    {
        if (messageQueues.ContainsKey(username))
        {
            while (messageQueues[username].Count > 0)
            {
                messagesToSend.Add(messageQueues[username].Dequeue());
            }
        }
    }

    foreach (var packet in messagesToSend.OrderBy(p => p.Sequence))
    {
        await SendToImmediate(username, packet);
    }
}

// ========== GROUP CREATION ==========
async Task HandleCreateGroup(string creator, string groupName)
{
    if (string.IsNullOrWhiteSpace(groupName))
        return;

    lock (serverLock)
    {
        if (!groups.ContainsKey(groupName))
        {
            groups[groupName] = new GroupInfo
            {
                Name = groupName,
                Admin = creator,
                Members = new List<string> { creator },
                CreatedDate = DateTime.Now,
                LastSequence = 0
            };

            messageSequences[$"group_{groupName}"] = 0;
        }
    }

    Console.WriteLine($"[GROUP CREATE] {creator} created: {groupName}");

    SaveGroupInfo(groupName);

    await SendSyncData(creator);
}

// ========== GROUP JOIN WITH NOTIFICATION ==========
async Task HandleJoinGroup(string joiner, string groupName)
{
    if (!groups.ContainsKey(groupName))
        return;

    bool wasMember = false;
    lock (serverLock)
    {
        var group = groups[groupName];
        wasMember = group.Members.Contains(joiner);
        if (!wasMember)
        {
            group.Members.Add(joiner);
        }
    }

    if (!wasMember)
    {
        Console.WriteLine($"[GROUP JOIN] {joiner} joined: {groupName}");

        // Send welcome message to joiner INSTANTLY
        await SendToImmediate(joiner, new Packet
        {
            Type = "GRP_MSG",
            From = "SYSTEM",
            To = groupName,
            Content = $"Welcome to '{groupName}'!",
            Time = DateTime.Now,
            Sequence = GetNextSequence($"group_{groupName}")
        });

        // Notify all group members INSTANTLY with join notification
        await BroadcastToGroupImmediate(groupName, $"{joiner} has joined the group", "SYSTEM");

        await UpdateAllClientsSync();
    }
}

// ========== GROUP LEAVE WITH NOTIFICATION ==========
async Task HandleLeaveGroup(string leaver, string groupName)
{
    if (!groups.ContainsKey(groupName))
        return;

    bool wasMember = false;
    lock (serverLock)
    {
        var group = groups[groupName];
        wasMember = group.Members.Remove(leaver);

        if (group.Members.Count == 0)
        {
            groups.Remove(groupName);
        }
        else if (group.Admin == leaver)
        {
            group.Admin = group.Members[0];
        }
    }

    if (wasMember)
    {
        Console.WriteLine($"[GROUP LEAVE] {leaver} left: {groupName}");

        // Notify group members INSTANTLY with leave notification
        await BroadcastToGroupImmediate(groupName, $"{leaver} has left the group", "SYSTEM");
        await UpdateAllClientsSync();
    }
}

// ========== KICK MEMBER WITH NOTIFICATION ==========
async Task HandleKickMember(string admin, string groupName, string target)
{
    if (!groups.ContainsKey(groupName) || groups[groupName].Admin != admin)
        return;

    lock (serverLock)
    {
        var group = groups[groupName];
        if (group.Members.Remove(target))
        {
            Console.WriteLine($"[GROUP KICK] {admin} kicked {target} from: {groupName}");

            // Notify kicked user INSTANTLY
            _ = SendToImmediate(target, new Packet
            {
                Type = "KICKED",
                To = groupName,
                Content = $"You were kicked from '{groupName}' by {admin}",
                Time = DateTime.Now
            });

            // Notify group INSTANTLY with kick notification
            _ = BroadcastToGroupImmediate(groupName, $"{target} was kicked by {admin}", "SYSTEM");
        }
    }

    await UpdateAllClientsSync();
}

// ========== SET ADMIN ==========
async Task HandleSetAdmin(string currentAdmin, string groupName, string newAdmin)
{
    if (!groups.ContainsKey(groupName) || groups[groupName].Admin != currentAdmin)
        return;

    lock (serverLock)
    {
        groups[groupName].Admin = newAdmin;
    }

    Console.WriteLine($"[GROUP ADMIN] {newAdmin} promoted in: {groupName}");

    await BroadcastToGroupImmediate(groupName, $"{newAdmin} is now the admin", "SYSTEM");
    await UpdateAllClientsSync();
}

// ========== DELETE GROUP ==========
async Task HandleDeleteGroup(string admin, string groupName)
{
    if (!groups.ContainsKey(groupName) || groups[groupName].Admin != admin)
        return;

    List<string> members;
    lock (serverLock)
    {
        members = groups[groupName].Members.ToList();
        groups.Remove(groupName);
    }

    Console.WriteLine($"[GROUP DELETE] {admin} deleted: {groupName}");

    // Notify all members INSTANTLY
    foreach (var member in members)
    {
        await SendToImmediate(member, new Packet
        {
            Type = "KICKED",
            To = groupName,
            Content = $"Group '{groupName}' was deleted by {admin}",
            Time = DateTime.Now
        });
    }

    await UpdateAllClientsSync();
}

// ========== GROUP MESSAGE HANDLER WITH INSTANT DELIVERY ==========
async Task HandleGroupMessage(Packet packet)
{
    if (!groups.ContainsKey(packet.To))
        return;

    // Assign sequence number for ordering
    long sequence = GetNextSequence($"group_{packet.To}");
    packet.Sequence = sequence;
    packet.MessageId = $"{packet.From}_{DateTime.Now.Ticks}_{Guid.NewGuid().ToString("N")}";
    packet.SentTime = DateTime.Now;

    // Save message to history
    SaveChatMessage(packet.To, packet.From, packet.Content, "group", packet.MessageId, packet.SentTime);

    // Send to ALL group members INSTANTLY
    var group = groups[packet.To];
    var sendTasks = new List<Task>();

    foreach (var member in group.Members)
    {
        if (member != packet.From) // Don't send to sender (they already see it instantly)
        {
            if (activeClients.ContainsKey(member))
            {
                sendTasks.Add(SendToImmediate(member, packet));
            }
            else
            {
                QueueMessageForUser(member, packet);
            }
        }
    }

    await Task.WhenAll(sendTasks);

    lock (serverLock)
    {
        deliveryAcks[packet.MessageId] = DateTime.Now;
    }
}

// ========== PRIVATE MESSAGE HANDLER WITH INSTANT DELIVERY ==========
async Task HandlePrivateMessage(Packet packet)
{
    packet.MessageId = $"{packet.From}_{DateTime.Now.Ticks}_{Guid.NewGuid().ToString("N")}";
    packet.SentTime = DateTime.Now;

    // Save to history
    string chatKey = $"{packet.From}_{packet.To}";
    if (string.Compare(packet.From, packet.To) > 0)
    {
        chatKey = $"{packet.To}_{packet.From}";
    }
    SaveChatMessage(chatKey, packet.From, packet.Content, "private", packet.MessageId, packet.SentTime);

    // Send to recipient INSTANTLY
    if (activeClients.ContainsKey(packet.To))
    {
        await SendToImmediate(packet.To, packet);
    }
    else
    {
        QueueMessageForUser(packet.To, packet);
    }

    // Send immediate delivery confirmation to sender
    await SendToImmediate(packet.From, new Packet
    {
        Type = "MSG_SENT",
        From = "SYSTEM",
        Content = packet.MessageId,
        Time = DateTime.Now
    });

    lock (serverLock)
    {
        deliveryAcks[packet.MessageId] = DateTime.Now;
    }
}

// ========== CLEAR CHAT HANDLER ==========
async Task HandleClearChat(string from, string to, string chatType)
{
    if (chatType == "private")
    {
        await SendToImmediate(from, new Packet
        {
            Type = "CLEAR_CHAT",
            From = "SYSTEM",
            To = to,
            Content = "clear",
            Time = DateTime.Now
        });

        if (from != to)
        {
            await SendToImmediate(to, new Packet
            {
                Type = "CLEAR_CHAT",
                From = "SYSTEM",
                To = from,
                Content = "clear",
                Time = DateTime.Now
            });
        }
    }
    else if (chatType == "group")
    {
        if (groups.ContainsKey(to))
        {
            foreach (var member in groups[to].Members)
            {
                if (activeClients.ContainsKey(member))
                {
                    await SendToImmediate(member, new Packet
                    {
                        Type = "CLEAR_CHAT",
                        From = "SYSTEM",
                        To = to,
                        Content = "clear",
                        Time = DateTime.Now
                    });
                }
            }
        }
    }
}

// ========== QUEUE MESSAGE FOR OFFLINE USER ==========
void QueueMessageForUser(string username, Packet packet)
{
    lock (serverLock)
    {
        if (!messageQueues.ContainsKey(username))
        {
            messageQueues[username] = new Queue<Packet>();
        }

        messageQueues[username].Enqueue(packet);

        while (messageQueues[username].Count > 100)
        {
            messageQueues[username].Dequeue();
        }
    }
}

// ========== SEQUENCE NUMBER GENERATION ==========
long GetNextSequence(string key)
{
    lock (serverLock)
    {
        if (!messageSequences.ContainsKey(key))
        {
            messageSequences[key] = 0;
        }

        messageSequences[key]++;
        return messageSequences[key];
    }
}

// ========== HISTORY MANAGEMENT ==========
void SaveChatMessage(string chatKey, string sender, string content, string chatType, string messageId, DateTime sentTime)
{
    try
    {
        string historyFile = Path.Combine(HISTORY_DIRECTORY, $"{chatKey}.json");
        List<ChatMessage> messages = new List<ChatMessage>();

        if (File.Exists(historyFile))
        {
            string json = File.ReadAllText(historyFile);
            messages = JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
        }

        messages.Add(new ChatMessage
        {
            MessageId = messageId,
            Sender = sender,
            Content = content,
            Timestamp = sentTime,
            Type = chatType
        });

        if (messages.Count > MAX_MESSAGES_PER_CHAT)
        {
            messages = messages.Skip(messages.Count - MAX_MESSAGES_PER_CHAT).ToList();
        }

        string newJson = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(historyFile, newJson);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HISTORY ERROR] {ex.Message}");
    }
}

string LoadChatHistory(string chatKey)
{
    try
    {
        string historyFile = Path.Combine(HISTORY_DIRECTORY, $"{chatKey}.json");

        if (!File.Exists(historyFile))
            return "";

        string json = File.ReadAllText(historyFile);
        var messages = JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();

        return string.Join("|", messages.Select(m =>
            $"[{m.Timestamp:HH:mm:ss}] [{m.Sender}] {m.Content}|{m.MessageId}"));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HISTORY ERROR] {ex.Message}");
        return "";
    }
}

void SaveGroupInfo(string groupName)
{
    try
    {
        if (groups.ContainsKey(groupName))
        {
            var group = groups[groupName];
            string groupFile = Path.Combine(HISTORY_DIRECTORY, $"group_{groupName}.json");

            string json = JsonSerializer.Serialize(group, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(groupFile, json);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[GROUP SAVE ERROR] {ex.Message}");
    }
}

// ========== CLIENT DISCONNECT HANDLER WITH NOTIFICATION ==========
async Task HandleClientDisconnect(string clientId)
{
    Console.WriteLine($"[INFO] {clientId} disconnected");

    bool wasActive = false;
    lock (serverLock)
    {
        wasActive = activeClients.ContainsKey(clientId);
        activeClients.Remove(clientId);
        userLastSeen.Remove(clientId);

        // Remove from groups
        foreach (var group in groups.Values)
        {
            if (group.Members.Contains(clientId))
            {
                group.Members.Remove(clientId);

                if (group.Members.Count == 0)
                {
                    groups.Remove(group.Name);
                }
                else if (group.Admin == clientId)
                {
                    group.Admin = group.Members[0];
                }
            }
        }
    }

    // Broadcast real-time disconnect alert INSTANTLY
    if (wasActive)
    {
        await BroadcastSystemAlertImmediate($"{clientId} has disconnected");
    }

    await UpdateAllClientsSync();
}

// ========== UPDATE ALL CLIENTS ==========
async Task UpdateAllClientsSync()
{
    var clients = activeClients.Keys.ToList();
    foreach (var client in clients)
    {
        await SendSyncData(client);
    }
}

// ========== BROADCAST TO GROUP INSTANTLY ==========
async Task BroadcastToGroupImmediate(string groupName, string message, string sender = "SYSTEM")
{
    if (!groups.ContainsKey(groupName))
        return;

    var packet = new Packet
    {
        Type = "GRP_MSG",
        From = sender,
        To = groupName,
        Content = message,
        Time = DateTime.Now,
        Sequence = GetNextSequence($"group_{groupName}")
    };

    var sendTasks = new List<Task>();
    foreach (var member in groups[groupName].Members)
    {
        if (activeClients.ContainsKey(member))
        {
            sendTasks.Add(SendToImmediate(member, packet));
        }
    }

    await Task.WhenAll(sendTasks);
}

// ========== BROADCAST SYSTEM ALERT INSTANTLY ==========
async Task BroadcastSystemAlertImmediate(string message)
{
    var packet = new Packet
    {
        Type = "ALERT",
        From = "SYSTEM",
        Content = message,
        Time = DateTime.Now
    };

    var sendTasks = new List<Task>();
    foreach (var client in activeClients)
    {
        sendTasks.Add(SendToImmediate(client.Key, packet));
    }

    await Task.WhenAll(sendTasks);
}

// ========== SYNC DATA ==========
async Task SendSyncData(string username)
{
    if (!activeClients.ContainsKey(username))
        return;

    var userList = new List<string>();
    foreach (var user in userLastSeen.Keys)
    {
        string status = activeClients.ContainsKey(user) ? "online" : "offline";
        userList.Add($"{user}|{status}");
    }

    var myGroupsList = new List<string>();
    foreach (var group in groups.Values)
    {
        if (group.Members.Contains(username))
        {
            int total = group.Members.Count;
            int active = group.Members.Count(m => activeClients.ContainsKey(m));
            string role = group.Admin == username ? "admin" : "member";
            myGroupsList.Add($"{group.Name}|{role}|{active}|{total}");
        }
    }

    var allGroupsList = groups.Values
        .Select(g => $"{g.Name}|{g.CreatedDate:yyyy-MM-dd HH:mm}")
        .ToList();

    var syncPacket = new Packet
    {
        Type = "SYNC_DATA",
        Content = string.Join(",", userList),
        GroupList = string.Join(",", myGroupsList),
        AllGroups = string.Join(",", allGroupsList),
        Time = DateTime.Now
    };

    await SendToImmediate(username, syncPacket);
}

// ========== SEND PACKET TO CLIENT INSTANTLY ==========
async Task SendToImmediate(string recipient, Packet packet)
{
    if (activeClients.TryGetValue(recipient, out var socket) &&
        socket.State == WebSocketState.Open)
    {
        try
        {
            string json = JsonSerializer.Serialize(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch
        {
            QueueMessageForUser(recipient, packet);
        }
    }
    else
    {
        QueueMessageForUser(recipient, packet);
    }
}

// ========== DATA CLASSES ==========
class Packet
{
    public string Type { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Content { get; set; } = "";
    public string ReplyRef { get; set; } = "";
    public string GroupList { get; set; } = "";
    public string AllGroups { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
    public DateTime SentTime { get; set; } = DateTime.Now;
    public long Sequence { get; set; } = 0;
    public string MessageId { get; set; } = "";
}

class GroupInfo
{
    public string Name { get; set; } = "";
    public string Admin { get; set; } = "";
    public List<string> Members { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public long LastSequence { get; set; }
}

class ChatMessage
{
    public string MessageId { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = "";
}