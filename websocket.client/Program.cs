using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// ========== STARTUP SCREEN ==========
Console.Clear();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine(@"==========================================================
   _   _            _____ _           _   
  | \ | |          / ____| |         | |  
  |  \| | _____  _| |    | |__   __ _| |_ 
  | . ` |/ _ \ \/ / |    | '_ \ / _` | __|
  | |\  |  __/>  <| |____| | | | (_| | |_ 
  |_| \_|\___/_/\_\\_____|_| | |\__,_|\__|
                                          
                NEXCHAT CLIENT v2.4
==========================================================");
Console.ResetColor();

// ========== USER SETUP ==========
Console.Write("\n Enter your username: ");
string myName = Console.ReadLine()?.Trim() ?? $"User_{new Random().Next(1000, 9999)}";
if (string.IsNullOrWhiteSpace(myName))
{
    myName = $"User_{new Random().Next(1000, 9999)}";
    Console.WriteLine($" Assigned username: {myName}");
    await Task.Delay(1000);
}

// ========== CONNECTION ==========
Console.WriteLine("\n Connecting to server...");

ClientWebSocket socket = new ClientWebSocket();
try
{
    await socket.ConnectAsync(new Uri("ws://localhost:5000/ws/"), CancellationToken.None);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($" Connected as: {myName}");
    Console.ResetColor();
    await Task.Delay(800);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($" Connection failed: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
    return;
}

// ========== DATA STORAGE ==========
List<string> userStatusList = new();
List<string> myGroups = new();
List<string> discoverableGroups = new();
Dictionary<string, List<ChatMessage>> chatHistory = new();
Dictionary<string, int> unreadCounts = new();
Dictionary<string, string> messageStatus = new();
string currentWindow = "";
string alertMessage = "";
bool refreshDisplay = false;
DateTime lastTypingSent = DateTime.MinValue;
List<string> systemAlerts = new List<string>();

// Load chat history from files
LoadPersistentHistory();

// ========== BACKGROUND TASKS ==========
_ = Task.Run(ReceiveMessages);
_ = Task.Run(SendHeartbeats);
await SendPacket("CONN", "", "");
await Task.Delay(500);

// ========== HEARTBEAT SENDER ==========
async Task SendHeartbeats()
{
    while (socket.State == WebSocketState.Open)
    {
        try
        {
            await SendPacket("HEARTBEAT", "", "");
            await Task.Delay(15000);
        }
        catch
        {
            break;
        }
    }
}

// ========== PERSISTENT HISTORY ==========
void LoadPersistentHistory()
{
    try
    {
        string historyFile = $"{myName}_history.json";
        if (File.Exists(historyFile))
        {
            string json = File.ReadAllText(historyFile);
            var history = JsonSerializer.Deserialize<Dictionary<string, List<ChatMessage>>>(json);
            if (history != null)
            {
                chatHistory = history;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HISTORY ERROR] {ex.Message}");
    }
}

void SavePersistentHistory()
{
    try
    {
        string historyFile = $"{myName}_history.json";
        string json = JsonSerializer.Serialize(chatHistory, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(historyFile, json);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HISTORY ERROR] {ex.Message}");
    }
}

void AddToHistory(string key, ChatMessage message)
{
    if (!chatHistory.ContainsKey(key))
        chatHistory[key] = new List<ChatMessage>();

    var existingMsg = chatHistory[key].FirstOrDefault(m => m.MessageId == message.MessageId);
    if (existingMsg != null)
    {
        existingMsg.Status = message.Status;
    }
    else
    {
        chatHistory[key].Add(message);
    }

    chatHistory[key] = chatHistory[key]
        .OrderBy(m => m.Timestamp)
        .ThenBy(m => m.Sequence)
        .ToList();

    if (chatHistory[key].Count > 1000)
    {
        chatHistory[key] = chatHistory[key].Skip(chatHistory[key].Count - 1000).ToList();
    }

    if (chatHistory[key].Count % 20 == 0)
    {
        SavePersistentHistory();
    }
}

void UpdateMessageStatus(string messageId, string status)
{
    messageStatus[messageId] = status;

    foreach (var chat in chatHistory)
    {
        var msg = chat.Value.FirstOrDefault(m => m.MessageId == messageId);
        if (msg != null)
        {
            msg.Status = status;
        }
    }

    refreshDisplay = true;
}

// ========== MAIN APPLICATION LOOP ==========
while (true)
{
    Console.Clear();
    DisplayHeader();

    Console.WriteLine("\n MAIN MENU");
    Console.WriteLine("\n    1. Private Messages");
    Console.WriteLine("    2. My Groups");
    Console.WriteLine("    3. Create New Group");
    Console.WriteLine("    4. Join a Group");
    Console.WriteLine("    5. Group Management");
    Console.WriteLine("    6. View Message History");
    Console.WriteLine("    7. View System Alerts");
    Console.WriteLine("    8. Exit");

    Console.WriteLine("\n" + new string('-', 58));
    DisplayStatusBar();
    DisplayRecentAlerts();
    Console.WriteLine(new string('-', 58));

    Console.Write("\n Select option (1-8): ");
    string choice = Console.ReadLine()?.Trim() ?? "";

    switch (choice)
    {
        case "1": await OpenPrivateChats(); break;
        case "2": await OpenMyGroups(); break;
        case "3": await CreateGroup(); break;
        case "4": await JoinGroup(); break;
        case "5": await GroupManagement(); break;
        case "6": await ViewHistory(); break;
        case "7": await ViewSystemAlerts(); break;
        case "8": await ExitApp(); return;
        default:
            ShowMessage("Invalid option", ConsoleColor.Red);
            await Task.Delay(1500);
            break;
    }
}

// ========== UI HELPER METHODS ==========
void DisplayHeader()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(@"==========================================================
                NEXCHAT v2.4
==========================================================");
    Console.ResetColor();
}

void DisplayStatusBar()
{
    int onlineCount = userStatusList.Count(u => u.Contains("|online"));

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($" Status: Online ({onlineCount}) | User: {myName}");

    if (!string.IsNullOrEmpty(alertMessage))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($" | Alert: {alertMessage}");
        alertMessage = "";
    }
    Console.ResetColor();
}

void DisplayRecentAlerts()
{
    if (systemAlerts.Count > 0)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n Recent Alerts:");
        Console.ForegroundColor = ConsoleColor.Gray;

        foreach (var alert in systemAlerts.TakeLast(3))
        {
            Console.WriteLine($"   • {alert}");
        }
        Console.ResetColor();
    }
}

void AddSystemAlert(string alert)
{
    systemAlerts.Add($"[{DateTime.Now:HH:mm:ss}] {alert}");

    if (systemAlerts.Count > 50)
    {
        systemAlerts.RemoveAt(0);
    }

    if (string.IsNullOrEmpty(currentWindow))
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n [!] {alert}");
        Console.ResetColor();
        refreshDisplay = true;
    }
}

void ShowMessage(string message, ConsoleColor color = ConsoleColor.White)
{
    Console.ForegroundColor = color;
    Console.WriteLine($"\n {message}");
    Console.ResetColor();
}

string GetStatusIcon(string status)
{
    return status switch
    {
        "Delivered" => "✓",
        "Read" => "✓✓",
        _ => ""
    };
}

ConsoleColor GetStatusColor(string status)
{
    return status switch
    {
        "Delivered" => ConsoleColor.Yellow,
        "Read" => ConsoleColor.Green,
        _ => ConsoleColor.Gray
    };
}

// ========== MENU METHODS ==========
async Task OpenPrivateChats()
{
    while (true)
    {
        await SendPacket("GET_STATUS", "", "");
        await Task.Delay(500);

        Console.Clear();
        DisplayHeader();
        Console.WriteLine("\n PRIVATE CHATS");
        Console.WriteLine(new string('=', 58));

        var otherUsers = userStatusList
            .Where(u => !u.StartsWith(myName + "|"))
            .OrderBy(u => u)
            .ToList();

        if (otherUsers.Count == 0)
        {
            Console.WriteLine("\n No other users online.");
        }
        else
        {
            Console.WriteLine("\n Online Users:");
            Console.WriteLine(new string('-', 40));

            for (int i = 0; i < otherUsers.Count; i++)
            {
                var parts = otherUsers[i].Split('|');
                string userName = parts[0];
                string status = parts[1];
                int unread = unreadCounts.GetValueOrDefault(userName, 0);
                string badge = unread > 0 ? $" [NEW: {unread}]" : "";

                Console.ForegroundColor = status == "online" ? ConsoleColor.Green : ConsoleColor.Gray;
                Console.WriteLine($"   {i + 1}. {userName} ({status}){badge}");
                Console.ResetColor();
            }
        }

        Console.WriteLine("\n" + new string('=', 58));
        Console.WriteLine(" Commands: [number] - Chat | B - Back | R - Refresh");
        Console.WriteLine(new string('=', 58));
        Console.Write("\n Choice: ");

        string input = Console.ReadLine()?.Trim().ToUpper() ?? "";

        if (input == "B") return;
        if (input == "R") continue;

        if (int.TryParse(input, out int idx) && idx >= 1 && idx <= otherUsers.Count)
        {
            string targetUser = otherUsers[idx - 1].Split('|')[0];
            await RealTimePrivateChat(targetUser);
        }
        else
        {
            ShowMessage("Invalid selection", ConsoleColor.Red);
            await Task.Delay(1000);
        }
    }
}

async Task OpenMyGroups()
{
    while (true)
    {
        await SendPacket("GET_STATUS", "", "");
        await Task.Delay(500);

        Console.Clear();
        DisplayHeader();
        Console.WriteLine("\n MY GROUPS");
        Console.WriteLine(new string('=', 58));

        if (myGroups.Count == 0)
        {
            Console.WriteLine("\n You are not in any groups.");
        }
        else
        {
            Console.WriteLine("\n Your Groups:");
            Console.WriteLine(new string('-', 40));

            for (int i = 0; i < myGroups.Count; i++)
            {
                var parts = myGroups[i].Split('|');
                string groupName = parts[0];
                string role = parts[1] == "admin" ? "[ADMIN]" : "[MEMBER]";
                string online = parts[2];
                string total = parts[3];
                int unread = unreadCounts.GetValueOrDefault(groupName, 0);
                string badge = unread > 0 ? $" [NEW: {unread}]" : "";

                Console.WriteLine($"   {i + 1}. {groupName} {role} [{online}/{total} online]{badge}");
            }
        }

        Console.WriteLine("\n" + new string('=', 58));
        Console.WriteLine(" Commands: [number] - Open | M - Message Member | B - Back | R - Refresh");
        Console.WriteLine(new string('=', 58));
        Console.Write("\n Choice: ");

        string input = Console.ReadLine()?.Trim().ToUpper() ?? "";

        if (input == "B") return;
        if (input == "R") continue;
        if (input == "M")
        {
            await MessageGroupMember();
            continue;
        }

        if (int.TryParse(input, out int idx) && idx >= 1 && idx <= myGroups.Count)
        {
            var parts = myGroups[idx - 1].Split('|');
            string groupName = parts[0];
            bool isAdmin = parts[1] == "admin";
            string active = parts[2];
            string total = parts[3];

            await RealTimeGroupChat(groupName, isAdmin, active, total);
        }
        else
        {
            ShowMessage("Invalid selection", ConsoleColor.Red);
            await Task.Delay(1000);
        }
    }
}

// ========== NEW: MESSAGE GROUP MEMBER ==========
async Task MessageGroupMember()
{
    if (myGroups.Count == 0)
    {
        ShowMessage("You are not in any groups", ConsoleColor.Red);
        await Task.Delay(1500);
        return;
    }

    Console.Clear();
    DisplayHeader();
    Console.WriteLine("\n MESSAGE GROUP MEMBER");
    Console.WriteLine(new string('=', 58));

    for (int i = 0; i < myGroups.Count; i++)
    {
        var parts = myGroups[i].Split('|');
        Console.WriteLine($"   {i + 1}. {parts[0]}");
    }

    Console.Write("\n Select group: ");
    if (!int.TryParse(Console.ReadLine(), out int groupIdx) || groupIdx < 1 || groupIdx > myGroups.Count)
    {
        ShowMessage("Invalid selection", ConsoleColor.Red);
        await Task.Delay(1500);
        return;
    }

    string groupName = myGroups[groupIdx - 1].Split('|')[0];

    // Get group members from server
    await SendPacket("GET_GROUP_MEMBERS", "", groupName);
    ShowMessage("Loading group members...", ConsoleColor.Cyan);
    await Task.Delay(1000);

    // Group members will be received via GROUP_MEMBERS packet
    // For now, show all online users except yourself
    var availableUsers = userStatusList
        .Where(u => !u.StartsWith(myName + "|") && u.Contains("|online"))
        .OrderBy(u => u)
        .ToList();

    if (availableUsers.Count == 0)
    {
        ShowMessage("No other users online in this group", ConsoleColor.Red);
        await Task.Delay(1500);
        return;
    }

    Console.WriteLine($"\n Online members in '{groupName}':");
    Console.WriteLine(new string('-', 40));

    for (int i = 0; i < availableUsers.Count; i++)
    {
        var parts = availableUsers[i].Split('|');
        Console.WriteLine($"   {i + 1}. {parts[0]}");
    }

    Console.Write("\n Select user to message: ");
    if (int.TryParse(Console.ReadLine(), out int userIdx) && userIdx >= 1 && userIdx <= availableUsers.Count)
    {
        string targetUser = availableUsers[userIdx - 1].Split('|')[0];

        Console.Write($"\n Private message to {targetUser}: ");
        string message = Console.ReadLine()?.Trim() ?? "";

        if (!string.IsNullOrEmpty(message))
        {
            // Send group private message
            await SendPacket("GROUP_PRIV", "", $"{groupName}|{targetUser}|{message}");
            ShowMessage($"Private message sent to {targetUser}", ConsoleColor.Green);
            await Task.Delay(1500);
        }
    }
    else
    {
        ShowMessage("Invalid selection", ConsoleColor.Red);
        await Task.Delay(1500);
    }
}

async Task CreateGroup()
{
    Console.Clear();
    DisplayHeader();
    Console.WriteLine("\n CREATE NEW GROUP");
    Console.WriteLine(new string('=', 58));

    Console.Write("\n Enter group name: ");
    string groupName = Console.ReadLine()?.Trim() ?? "";

    if (string.IsNullOrWhiteSpace(groupName))
    {
        ShowMessage("Group name cannot be empty", ConsoleColor.Red);
        await Task.Delay(1500);
        return;
    }

    ShowMessage($"Creating group '{groupName}'...", ConsoleColor.Cyan);
    await SendPacket("GRP_CREATE", "", groupName);

    await Task.Delay(800);
    await SendPacket("GET_STATUS", "", "");
    await Task.Delay(500);

    if (myGroups.Any(g => g.StartsWith(groupName + "|")))
    {
        ShowMessage($"Group '{groupName}' created successfully!", ConsoleColor.Green);
        AddSystemAlert($"You created group '{groupName}'");
    }
    else
    {
        ShowMessage($"Could not create group '{groupName}'", ConsoleColor.Red);
    }

    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey();
}

async Task JoinGroup()
{
    while (true)
    {
        Console.Clear();
        DisplayHeader();
        Console.WriteLine("\n JOIN GROUP");
        Console.WriteLine(new string('=', 58));

        ShowMessage("Loading available groups...", ConsoleColor.Cyan);
        await SendPacket("GET_STATUS", "", "");
        await Task.Delay(800);

        if (discoverableGroups.Count == 0)
        {
            Console.WriteLine("\n No groups available to join.");
        }
        else
        {
            Console.WriteLine($"\n Available Groups ({discoverableGroups.Count}):");
            Console.WriteLine(new string('-', 40));

            for (int i = 0; i < discoverableGroups.Count; i++)
            {
                var parts = discoverableGroups[i].Split('|');
                string groupName = parts[0];
                string created = parts.Length > 1 ? parts[1] : "Unknown";

                bool isMember = myGroups.Any(g => g.StartsWith(groupName + "|"));
                string status = isMember ? " [ALREADY MEMBER]" : " [AVAILABLE]";

                Console.WriteLine($"   {i + 1}. {groupName}");
                Console.WriteLine($"       Created: {created}{status}");
            }
        }

        Console.WriteLine("\n" + new string('=', 58));
        Console.WriteLine(" Commands: [number] - Join | B - Back | R - Refresh");
        Console.WriteLine(new string('=', 58));
        Console.Write("\n Choice: ");

        string input = Console.ReadLine()?.Trim().ToUpper() ?? "";

        if (input == "B") return;
        if (input == "R") continue;

        if (int.TryParse(input, out int idx) && idx >= 1 && idx <= discoverableGroups.Count)
        {
            string groupName = discoverableGroups[idx - 1].Split('|')[0];

            if (myGroups.Any(g => g.StartsWith(groupName + "|")))
            {
                ShowMessage($"You are already a member of '{groupName}'", ConsoleColor.Yellow);
                await Task.Delay(1500);
                continue;
            }

            Console.Write($"\n Join group '{groupName}'? (Y/N): ");
            string confirm = Console.ReadLine()?.Trim().ToUpper() ?? "";

            if (confirm == "Y" || confirm == "YES")
            {
                ShowMessage($"Joining group '{groupName}'...", ConsoleColor.Cyan);
                await SendPacket("GRP_JOIN", groupName, "");

                await Task.Delay(1500);
                await SendPacket("GET_STATUS", "", "");
                await Task.Delay(500);

                if (myGroups.Any(g => g.StartsWith(groupName + "|")))
                {
                    ShowMessage($"Successfully joined '{groupName}'!", ConsoleColor.Green);
                    AddSystemAlert($"You joined group '{groupName}'");
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                    return;
                }
                else
                {
                    ShowMessage($"Failed to join '{groupName}'", ConsoleColor.Red);
                }
            }
        }
        else
        {
            ShowMessage("Invalid selection", ConsoleColor.Red);
        }

        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
    }
}

async Task GroupManagement()
{
    while (true)
    {
        await SendPacket("GET_STATUS", "", "");
        await Task.Delay(500);

        Console.Clear();
        DisplayHeader();
        Console.WriteLine("\n GROUP MANAGEMENT");
        Console.WriteLine(new string('=', 58));

        var adminGroups = myGroups
            .Where(g => g.Split('|')[1] == "admin")
            .Select(g => g.Split('|')[0])
            .ToList();

        if (adminGroups.Count == 0)
        {
            Console.WriteLine("\n You are not an admin of any groups.");
        }
        else
        {
            Console.WriteLine("\n Your Admin Groups:");
            Console.WriteLine(new string('-', 40));

            for (int i = 0; i < adminGroups.Count; i++)
            {
                Console.WriteLine($"   {i + 1}. {adminGroups[i]}");
            }
        }

        Console.WriteLine("\n" + new string('=', 58));
        Console.WriteLine(" Commands: [number] - Manage | B - Back | R - Refresh");
        Console.WriteLine(new string('=', 58));
        Console.Write("\n Choice: ");

        string input = Console.ReadLine()?.Trim().ToUpper() ?? "";

        if (input == "B") return;
        if (input == "R") continue;

        if (int.TryParse(input, out int idx) && idx >= 1 && idx <= adminGroups.Count)
        {
            string groupName = adminGroups[idx - 1];
            await ManageGroupOptions(groupName);
        }
        else
        {
            ShowMessage("Invalid selection", ConsoleColor.Red);
            await Task.Delay(1000);
        }
    }
}

async Task ManageGroupOptions(string groupName)
{
    while (true)
    {
        Console.Clear();
        DisplayHeader();
        Console.WriteLine($"\n MANAGE GROUP: {groupName}");
        Console.WriteLine(new string('=', 58));

        Console.WriteLine("\n   1. Kick Member");
        Console.WriteLine("   2. Promote to Admin");
        Console.WriteLine("   3. Delete Group");
        Console.WriteLine("   4. Leave Group");
        Console.WriteLine("   5. Back");

        Console.WriteLine("\n" + new string('=', 58));
        Console.Write(" Choice: ");

        string choice = Console.ReadLine()?.Trim() ?? "";

        switch (choice)
        {
            case "1":
                Console.Write("   Username to kick: ");
                string target = Console.ReadLine()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(target))
                {
                    await SendPacket("GRP_KICK", groupName, target);
                    ShowMessage($"Kicking {target}...", ConsoleColor.Yellow);
                    AddSystemAlert($"You kicked {target} from {groupName}");
                    await Task.Delay(1000);
                }
                break;

            case "2":
                Console.Write("   Username to promote: ");
                string promote = Console.ReadLine()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(promote))
                {
                    await SendPacket("GRP_SET_ADMIN", groupName, promote);
                    ShowMessage($"Promoting {promote}...", ConsoleColor.Yellow);
                    AddSystemAlert($"You promoted {promote} to admin in {groupName}");
                    await Task.Delay(1000);
                }
                break;

            case "3":
                Console.Write($"   DELETE group '{groupName}'? This cannot be undone! (Y/N): ");
                if (Console.ReadLine()?.ToUpper() == "Y")
                {
                    await SendPacket("GRP_DELETE", groupName, "");
                    ShowMessage($"Deleting {groupName}...", ConsoleColor.Red);
                    AddSystemAlert($"You deleted group {groupName}");
                    await Task.Delay(1000);
                    return;
                }
                break;

            case "4":
                Console.Write("   Leave this group? (Y/N): ");
                if (Console.ReadLine()?.ToUpper() == "Y")
                {
                    await SendPacket("GRP_LEAVE", groupName, "");
                    ShowMessage($"Leaving {groupName}...", ConsoleColor.Yellow);
                    AddSystemAlert($"You left group {groupName}");
                    await Task.Delay(1000);
                    return;
                }
                break;

            case "5":
                return;
        }
    }
}

async Task ViewHistory()
{
    Console.Clear();
    DisplayHeader();
    Console.WriteLine("\n MESSAGE HISTORY");
    Console.WriteLine(new string('=', 58));

    Console.WriteLine("\n 1. View Private Chat History");
    Console.WriteLine(" 2. View Group Chat History");
    Console.WriteLine(" 3. Back");

    Console.Write("\n Choice: ");
    string choice = Console.ReadLine()?.Trim() ?? "";

    if (choice == "1")
    {
        await ShowPrivateHistory();
    }
    else if (choice == "2")
    {
        await ShowGroupHistory();
    }
}

async Task ShowPrivateHistory()
{
    Console.Clear();
    DisplayHeader();
    Console.WriteLine("\n PRIVATE CHAT HISTORY");
    Console.WriteLine(new string('=', 58));

    var otherUsers = userStatusList
        .Where(u => !u.StartsWith(myName + "|"))
        .OrderBy(u => u)
        .ToList();

    if (otherUsers.Count == 0)
    {
        Console.WriteLine("\n No other users found.");
    }
    else
    {
        for (int i = 0; i < otherUsers.Count; i++)
        {
            var parts = otherUsers[i].Split('|');
            Console.WriteLine($"   {i + 1}. {parts[0]} ({parts[1]})");
        }

        Console.Write("\n Select user to view history: ");
        if (int.TryParse(Console.ReadLine(), out int idx) && idx >= 1 && idx <= otherUsers.Count)
        {
            string targetUser = otherUsers[idx - 1].Split('|')[0];

            await SendPacket("GET_HISTORY", "", targetUser);
            ShowMessage("Loading history...", ConsoleColor.Cyan);
            await Task.Delay(1000);

            if (chatHistory.ContainsKey(targetUser))
            {
                Console.WriteLine($"\n History with {targetUser}:");
                Console.WriteLine(new string('-', 40));
                foreach (var msg in chatHistory[targetUser].TakeLast(20))
                {
                    string status = GetStatusIcon(messageStatus.ContainsKey(msg.MessageId) ? messageStatus[msg.MessageId] : "");
                    Console.WriteLine($"   [{msg.Timestamp:HH:mm:ss}] [{msg.Sender}] {msg.Content} {status}");
                }
            }
            else
            {
                Console.WriteLine("\n No history found.");
            }
        }
    }

    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey();
}

async Task ShowGroupHistory()
{
    Console.Clear();
    DisplayHeader();
    Console.WriteLine("\n GROUP CHAT HISTORY");
    Console.WriteLine(new string('=', 58));

    if (myGroups.Count == 0)
    {
        Console.WriteLine("\n You are not in any groups.");
    }
    else
    {
        for (int i = 0; i < myGroups.Count; i++)
        {
            var parts = myGroups[i].Split('|');
            Console.WriteLine($"   {i + 1}. {parts[0]}");
        }

        Console.Write("\n Select group to view history: ");
        if (int.TryParse(Console.ReadLine(), out int idx) && idx >= 1 && idx <= myGroups.Count)
        {
            string groupName = myGroups[idx - 1].Split('|')[0];

            await SendPacket("GET_HISTORY", "", $"group_{groupName}");
            ShowMessage("Loading history...", ConsoleColor.Cyan);
            await Task.Delay(1000);

            if (chatHistory.ContainsKey(groupName))
            {
                Console.WriteLine($"\n History for {groupName}:");
                Console.WriteLine(new string('-', 40));
                foreach (var msg in chatHistory[groupName].TakeLast(20))
                {
                    Console.WriteLine($"   [{msg.Timestamp:HH:mm:ss}] [{msg.Sender}] {msg.Content}");
                }
            }
            else
            {
                Console.WriteLine("\n No history found.");
            }
        }
    }

    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey();
}

async Task ViewSystemAlerts()
{
    Console.Clear();
    DisplayHeader();
    Console.WriteLine("\n SYSTEM ALERTS");
    Console.WriteLine(new string('=', 58));

    if (systemAlerts.Count == 0)
    {
        Console.WriteLine("\n No system alerts yet.");
    }
    else
    {
        Console.WriteLine($"\n Showing {systemAlerts.Count} alerts:");
        Console.WriteLine(new string('-', 58));

        foreach (var alert in systemAlerts)
        {
            Console.WriteLine($"   {alert}");
        }
    }

    Console.WriteLine("\n" + new string('=', 58));
    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey();
}

// ========== REAL-TIME PRIVATE CHAT ==========
async Task RealTimePrivateChat(string targetUser)
{
    currentWindow = targetUser;
    unreadCounts[targetUser] = 0;

    if (chatHistory.ContainsKey(targetUser))
    {
        foreach (var msg in chatHistory[targetUser].Where(m => m.Sender != myName && !string.IsNullOrEmpty(m.MessageId)))
        {
            await SendPacket("MSG_READ", "", msg.MessageId);
            UpdateMessageStatus(msg.MessageId, "Read");
        }
    }

    Console.Clear();
    Console.WriteLine($"\n PRIVATE CHAT: {targetUser}");
    Console.WriteLine(new string('=', 58));

    int messageCount = chatHistory.ContainsKey(targetUser) ? Math.Min(chatHistory[targetUser].Count, 15) : 0;
    int commandLine = 3 + messageCount;

    if (chatHistory.ContainsKey(targetUser) && chatHistory[targetUser].Count > 0)
    {
        var messagesToShow = chatHistory[targetUser]
            .OrderBy(m => m.Timestamp)
            .TakeLast(15)
            .ToList();

        foreach (var msg in messagesToShow)
        {
            if (msg.Sender == myName)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($" [{msg.Timestamp:HH:mm:ss}] [YOU] {msg.Content}");

                string status = messageStatus.ContainsKey(msg.MessageId) ? messageStatus[msg.MessageId] : "";
                Console.ForegroundColor = GetStatusColor(status);
                Console.Write($" {GetStatusIcon(status)}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" [{msg.Timestamp:HH:mm:ss}] [{msg.Sender}] {msg.Content}");
            }
            Console.WriteLine();
            Console.ResetColor();
        }
    }
    else
    {
        Console.WriteLine("\n   No messages yet. Say hello!");
    }

    Console.SetCursorPosition(0, commandLine);
    Console.WriteLine(new string('=', 58));
    Console.WriteLine(" Commands: B - Back | C - Clear Chat | ENTER - Send");
    Console.WriteLine(new string('=', 58));
    Console.Write("\n Message: ");

    int inputLine = Console.CursorTop;
    int inputColumn = Console.CursorLeft;

    int lastDisplayedCount = chatHistory.ContainsKey(targetUser) ? chatHistory[targetUser].Count : 0;
    Dictionary<string, string> lastMessageStatus = new();
    StringBuilder inputBuffer = new StringBuilder();

    bool cursorVisible = true;
    DateTime lastCursorBlink = DateTime.Now;
    bool isInputLineVisible = true;

    while (currentWindow == targetUser)
    {
        try
        {
            bool needToUpdateMessages = false;

            if (chatHistory.ContainsKey(targetUser))
            {
                int currentCount = chatHistory[targetUser].Count;
                if (currentCount != lastDisplayedCount)
                {
                    needToUpdateMessages = true;
                    lastDisplayedCount = currentCount;

                    messageCount = Math.Min(chatHistory[targetUser].Count, 15);
                    commandLine = 3 + messageCount;
                }

                foreach (var msg in chatHistory[targetUser].TakeLast(20))
                {
                    string currentStatus = messageStatus.ContainsKey(msg.MessageId) ? messageStatus[msg.MessageId] : "";
                    if (!lastMessageStatus.ContainsKey(msg.MessageId) || lastMessageStatus[msg.MessageId] != currentStatus)
                    {
                        needToUpdateMessages = true;
                        lastMessageStatus[msg.MessageId] = currentStatus;
                    }
                }
            }

            if (needToUpdateMessages || refreshDisplay)
            {
                refreshDisplay = false;

                int savedCursorTop = Console.CursorTop;
                int savedCursorLeft = Console.CursorLeft;

                Console.SetCursorPosition(0, 2);
                int linesToClear = Console.WindowHeight - 3;
                for (int i = 0; i < linesToClear; i++)
                {
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                }

                Console.SetCursorPosition(0, 2);

                if (chatHistory.ContainsKey(targetUser) && chatHistory[targetUser].Count > 0)
                {
                    var messagesToShow = chatHistory[targetUser]
                        .OrderBy(m => m.Timestamp)
                        .TakeLast(15)
                        .ToList();

                    foreach (var msg in messagesToShow)
                    {
                        if (msg.Sender == myName)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.Write($" [{msg.Timestamp:HH:mm:ss}] [YOU] {msg.Content}");

                            string status = messageStatus.ContainsKey(msg.MessageId) ? messageStatus[msg.MessageId] : "";
                            Console.ForegroundColor = GetStatusColor(status);
                            Console.Write($" {GetStatusIcon(status)}");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write($" [{msg.Timestamp:HH:mm:ss}] [{msg.Sender}] {msg.Content}");
                        }
                        Console.WriteLine();
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.WriteLine("\n   No messages yet. Say hello!");
                }

                Console.SetCursorPosition(0, commandLine);
                Console.WriteLine(new string('=', 58));
                Console.WriteLine(" Commands: B - Back | C - Clear Chat | ENTER - Send");
                Console.WriteLine(new string('=', 58));
                Console.Write("\n Message: ");

                inputLine = commandLine + 4;
                inputColumn = 10;

                Console.SetCursorPosition(savedCursorLeft, savedCursorTop);
            }

            if ((DateTime.Now - lastCursorBlink).TotalMilliseconds > 500)
            {
                cursorVisible = !cursorVisible;
                lastCursorBlink = DateTime.Now;

                if (isInputLineVisible)
                {
                    Console.SetCursorPosition(inputColumn + inputBuffer.Length, inputLine);
                    Console.Write(cursorVisible ? "_" : " ");
                    Console.SetCursorPosition(inputColumn + inputBuffer.Length, inputLine);
                }
            }

            if (isInputLineVisible)
            {
                Console.SetCursorPosition(inputColumn, inputLine);
                Console.Write(new string(' ', Console.WindowWidth - inputColumn - 1));
                Console.SetCursorPosition(inputColumn, inputLine);
                Console.Write(inputBuffer.ToString());

                Console.SetCursorPosition(inputColumn + inputBuffer.Length, inputLine);
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.B && inputBuffer.Length == 0)
                {
                    currentWindow = "";
                    break;
                }
                else if (key.Key == ConsoleKey.C && inputBuffer.Length == 0)
                {
                    isInputLineVisible = false;
                    Console.SetCursorPosition(0, inputLine);
                    Console.Write(new string(' ', Console.WindowWidth));

                    Console.SetCursorPosition(0, inputLine);
                    Console.Write("Clear chat history (both sides)? (Y/N): ");
                    string confirm = Console.ReadLine()?.Trim().ToUpper() ?? "";

                    Console.SetCursorPosition(0, inputLine);
                    Console.Write(new string(' ', Console.WindowWidth));

                    isInputLineVisible = true;
                    refreshDisplay = true;

                    if (confirm == "Y")
                    {
                        await SendPacket("CLEAR_CHAT", targetUser, "private");

                        if (chatHistory.ContainsKey(targetUser))
                        {
                            chatHistory[targetUser].Clear();
                        }

                        string reverseKey = $"{targetUser}_{myName}";
                        if (chatHistory.ContainsKey(reverseKey))
                        {
                            chatHistory[reverseKey].Clear();
                        }

                        SavePersistentHistory();
                        AddSystemAlert($"Cleared chat with {targetUser}");

                        refreshDisplay = true;
                    }
                    continue;
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    if (inputBuffer.Length > 0)
                    {
                        string message = inputBuffer.ToString();
                        inputBuffer.Clear();

                        string messageId = $"{myName}_{DateTime.Now.Ticks}_{Guid.NewGuid().ToString("N")}";

                        AddToHistory(targetUser, new ChatMessage
                        {
                            MessageId = messageId,
                            Sender = myName,
                            Content = message,
                            Timestamp = DateTime.Now,
                            Sequence = GetNextSequenceNumber(targetUser),
                            Status = "Delivered"
                        });

                        UpdateMessageStatus(messageId, "Delivered");
                        await SendPacket("PRIV", targetUser, message, messageId);

                        refreshDisplay = true;
                    }
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (inputBuffer.Length > 0)
                    {
                        inputBuffer.Length--;
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    inputBuffer.Append(key.KeyChar);
                }
            }

            await Task.Delay(50);
        }
        catch (Exception ex)
        {
            Console.SetCursorPosition(0, inputLine + 2);
            Console.WriteLine($"Error: {ex.Message}");
            break;
        }
    }
}

// ========== REAL-TIME GROUP CHAT ==========
async Task RealTimeGroupChat(string groupName, bool isAdmin, string active, string total)
{
    currentWindow = groupName;
    unreadCounts[groupName] = 0;

    Console.Clear();
    Console.WriteLine($"\n GROUP CHAT: {groupName}");
    Console.WriteLine($"   Online: {active}/{total} | Role: {(isAdmin ? "Admin" : "Member")}");
    Console.WriteLine(new string('=', 58));

    int messageCount = chatHistory.ContainsKey(groupName) ? Math.Min(chatHistory[groupName].Count, 15) : 0;
    int commandLine = 4 + messageCount;

    if (chatHistory.ContainsKey(groupName) && chatHistory[groupName].Count > 0)
    {
        var messagesToShow = chatHistory[groupName]
            .OrderBy(m => m.Timestamp)
            .ThenBy(m => m.Sequence)
            .TakeLast(15)
            .ToList();

        foreach (var msg in messagesToShow)
        {
            if (msg.Sender == "SYSTEM")
            {
                // Color join/leave alerts differently
                bool isJoinLeave = msg.Content.Contains("joined") || msg.Content.Contains("left") ||
                                   msg.Content.Contains("disconnected") || msg.Content.Contains("kicked");
                Console.ForegroundColor = isJoinLeave ? ConsoleColor.Red : ConsoleColor.Yellow;
                Console.WriteLine($"   [{msg.Timestamp:HH:mm:ss}] {msg.Content}");
            }
            else if (msg.Sender == myName)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"   [{msg.Timestamp:HH:mm:ss}] [YOU] {msg.Content}");

                string status = messageStatus.ContainsKey(msg.MessageId) ? messageStatus[msg.MessageId] : "";
                Console.ForegroundColor = GetStatusColor(status);
                Console.Write($" {GetStatusIcon(status)}");
                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   [{msg.Timestamp:HH:mm:ss}] [{msg.Sender}] {msg.Content}");
            }
            Console.ResetColor();
        }
    }
    else
    {
        Console.WriteLine("\n   No messages yet. Start chatting!");
    }

    Console.SetCursorPosition(0, commandLine);
    Console.WriteLine(new string('=', 58));
    Console.WriteLine(" Commands: B - Back | C - Clear Chat | M - Message Member | ENTER - Send");
    Console.WriteLine(new string('=', 58));
    Console.Write("\n Message: ");

    int inputLine = Console.CursorTop;
    int inputColumn = Console.CursorLeft;

    int lastDisplayedCount = chatHistory.ContainsKey(groupName) ? chatHistory[groupName].Count : 0;
    StringBuilder inputBuffer = new StringBuilder();

    bool cursorVisible = true;
    DateTime lastCursorBlink = DateTime.Now;
    bool isInputLineVisible = true;

    while (currentWindow == groupName)
    {
        try
        {
            bool needToUpdateMessages = false;

            if (chatHistory.ContainsKey(groupName))
            {
                int currentCount = chatHistory[groupName].Count;
                if (currentCount != lastDisplayedCount)
                {
                    needToUpdateMessages = true;
                    lastDisplayedCount = currentCount;

                    messageCount = Math.Min(chatHistory[groupName].Count, 15);
                    commandLine = 4 + messageCount;
                }
            }

            if (needToUpdateMessages || refreshDisplay)
            {
                refreshDisplay = false;

                int savedCursorTop = Console.CursorTop;
                int savedCursorLeft = Console.CursorLeft;

                Console.SetCursorPosition(0, 3);
                int linesToClear = Console.WindowHeight - 4;
                for (int i = 0; i < linesToClear; i++)
                {
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                }

                Console.SetCursorPosition(0, 3);

                if (chatHistory.ContainsKey(groupName) && chatHistory[groupName].Count > 0)
                {
                    var messagesToShow = chatHistory[groupName]
                        .OrderBy(m => m.Timestamp)
                        .ThenBy(m => m.Sequence)
                        .TakeLast(15)
                        .ToList();

                    foreach (var msg in messagesToShow)
                    {
                        if (msg.Sender == "SYSTEM")
                        {
                            bool isJoinLeave = msg.Content.Contains("joined") || msg.Content.Contains("left") ||
                                               msg.Content.Contains("disconnected") || msg.Content.Contains("kicked");
                            Console.ForegroundColor = isJoinLeave ? ConsoleColor.Red : ConsoleColor.Yellow;
                            Console.WriteLine($"   [{msg.Timestamp:HH:mm:ss}] {msg.Content}");
                        }
                        else if (msg.Sender == myName)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.Write($"   [{msg.Timestamp:HH:mm:ss}] [YOU] {msg.Content}");

                            string status = messageStatus.ContainsKey(msg.MessageId) ? messageStatus[msg.MessageId] : "";
                            Console.ForegroundColor = GetStatusColor(status);
                            Console.Write($" {GetStatusIcon(status)}");
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"   [{msg.Timestamp:HH:mm:ss}] [{msg.Sender}] {msg.Content}");
                        }
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.WriteLine("   No messages yet. Start chatting!");
                }

                Console.SetCursorPosition(0, commandLine);
                Console.WriteLine(new string('=', 58));
                Console.WriteLine(" Commands: B - Back | C - Clear Chat | M - Message Member | ENTER - Send");
                Console.WriteLine(new string('=', 58));
                Console.Write("\n Message: ");

                inputLine = commandLine + 4;
                inputColumn = 10;

                Console.SetCursorPosition(savedCursorLeft, savedCursorTop);
            }

            if ((DateTime.Now - lastCursorBlink).TotalMilliseconds > 500)
            {
                cursorVisible = !cursorVisible;
                lastCursorBlink = DateTime.Now;

                if (isInputLineVisible)
                {
                    Console.SetCursorPosition(inputColumn + inputBuffer.Length, inputLine);
                    Console.Write(cursorVisible ? "_" : " ");
                    Console.SetCursorPosition(inputColumn + inputBuffer.Length, inputLine);
                }
            }

            if (isInputLineVisible)
            {
                Console.SetCursorPosition(inputColumn, inputLine);
                Console.Write(new string(' ', Console.WindowWidth - inputColumn - 1));
                Console.SetCursorPosition(inputColumn, inputLine);
                Console.Write(inputBuffer.ToString());

                Console.SetCursorPosition(inputColumn + inputBuffer.Length, inputLine);
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.B && inputBuffer.Length == 0)
                {
                    currentWindow = "";
                    break;
                }
                else if (key.Key == ConsoleKey.C && inputBuffer.Length == 0 && isAdmin)
                {
                    isInputLineVisible = false;
                    Console.SetCursorPosition(0, inputLine);
                    Console.Write(new string(' ', Console.WindowWidth));

                    Console.SetCursorPosition(0, inputLine);
                    Console.Write("Clear group chat? (Y/N): ");
                    string confirm = Console.ReadLine()?.Trim().ToUpper() ?? "";

                    Console.SetCursorPosition(0, inputLine);
                    Console.Write(new string(' ', Console.WindowWidth));

                    isInputLineVisible = true;
                    refreshDisplay = true;

                    if (confirm == "Y")
                    {
                        await SendPacket("CLEAR_CHAT", groupName, "group");

                        if (chatHistory.ContainsKey(groupName))
                        {
                            chatHistory[groupName].Clear();
                        }

                        SavePersistentHistory();
                        AddSystemAlert($"Cleared chat in {groupName}");

                        refreshDisplay = true;
                    }
                    continue;
                }
                else if (key.Key == ConsoleKey.M && inputBuffer.Length == 0)
                {
                    // Message a specific group member privately
                    isInputLineVisible = false;
                    Console.SetCursorPosition(0, inputLine);
                    Console.Write(new string(' ', Console.WindowWidth));

                    Console.SetCursorPosition(0, inputLine);
                    Console.Write("Enter username to message privately: ");
                    string targetUser = Console.ReadLine()?.Trim() ?? "";

                    Console.SetCursorPosition(0, inputLine);
                    Console.Write(new string(' ', Console.WindowWidth));

                    Console.SetCursorPosition(0, inputLine);
                    Console.Write("Enter message: ");
                    string message = Console.ReadLine()?.Trim() ?? "";

                    Console.SetCursorPosition(0, inputLine);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, inputLine);
                    Console.Write(new string(' ', Console.WindowWidth));

                    isInputLineVisible = true;
                    refreshDisplay = true;

                    if (!string.IsNullOrEmpty(targetUser) && !string.IsNullOrEmpty(message))
                    {
                        await SendPacket("GROUP_PRIV", "", $"{groupName}|{targetUser}|{message}");
                        ShowMessage($"Private message sent to {targetUser}", ConsoleColor.Green);
                        await Task.Delay(1500);
                        refreshDisplay = true;
                    }
                    continue;
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    if (inputBuffer.Length > 0)
                    {
                        string message = inputBuffer.ToString();
                        inputBuffer.Clear();

                        string messageId = $"{myName}_{DateTime.Now.Ticks}_{Guid.NewGuid().ToString("N")}";

                        AddToHistory(groupName, new ChatMessage
                        {
                            MessageId = messageId,
                            Sender = myName,
                            Content = message,
                            Timestamp = DateTime.Now,
                            Sequence = GetNextSequenceNumber($"group_{groupName}"),
                            Status = ""
                        });

                        await SendPacket("GRP_MSG", groupName, message, messageId);

                        refreshDisplay = true;
                    }
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (inputBuffer.Length > 0)
                    {
                        inputBuffer.Length--;
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    inputBuffer.Append(key.KeyChar);
                }
            }

            await Task.Delay(50);
        }
        catch (Exception ex)
        {
            Console.SetCursorPosition(0, inputLine + 2);
            Console.WriteLine($"Error: {ex.Message}");
            break;
        }
    }
}

long GetNextSequenceNumber(string chatKey)
{
    if (!chatHistory.ContainsKey(chatKey) || chatHistory[chatKey].Count == 0)
        return 1;

    return chatHistory[chatKey].Max(m => m.Sequence) + 1;
}

// ========== NETWORK METHODS ==========
async Task SendPacket(string type, string to, string content, string messageId = "")
{
    try
    {
        if (socket.State != WebSocketState.Open)
        {
            ShowMessage("Connection lost. Please restart the application.", ConsoleColor.Red);
            return;
        }

        var packet = new Packet
        {
            Type = type,
            From = myName,
            To = to,
            Content = content,
            Time = DateTime.Now,
            MessageId = string.IsNullOrEmpty(messageId) ? $"{myName}_{DateTime.Now.Ticks}" : messageId
        };

        string json = JsonSerializer.Serialize(packet);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        await socket.SendAsync(new ArraySegment<byte>(buffer),
            WebSocketMessageType.Text, true, CancellationToken.None);
    }
    catch (Exception ex)
    {
        ShowMessage($"Send error: {ex.Message}", ConsoleColor.Red);
    }
}

async Task ReceiveMessages()
{
    var buffer = new byte[8192];

    while (socket.State == WebSocketState.Open)
    {
        try
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var packet = JsonSerializer.Deserialize<Packet>(json);

            if (packet == null) continue;

            await ProcessIncomingPacket(packet);
        }
        catch (WebSocketException)
        {
            ShowMessage("Connection lost to server", ConsoleColor.Red);
            break;
        }
        catch (Exception ex)
        {
            ShowMessage($"Receive error: {ex.Message}", ConsoleColor.Red);
        }
    }
}

// ========== PROCESS INCOMING PACKET ==========
async Task ProcessIncomingPacket(Packet packet)
{
    switch (packet.Type)
    {
        case "SYNC_DATA":
            var oldUsers = new HashSet<string>(userStatusList);
            userStatusList = packet.Content.Split(',')
                .Where(s => !string.IsNullOrEmpty(s)).ToList();
            myGroups = packet.GroupList.Split(',')
                .Where(s => !string.IsNullOrEmpty(s)).ToList();
            discoverableGroups = packet.AllGroups.Split(',')
                .Where(s => !string.IsNullOrEmpty(s)).ToList();
            refreshDisplay = true;

            var newUsers = new HashSet<string>(userStatusList);

            // Detect joins and leaves
            foreach (var user in newUsers.Except(oldUsers))
            {
                var parts = user.Split('|');
                if (parts.Length >= 2 && parts[1] == "online" && parts[0] != myName)
                {
                    AddSystemAlert($"{parts[0]} has joined the chat");
                }
            }

            foreach (var user in oldUsers.Except(newUsers))
            {
                var parts = user.Split('|');
                if (parts.Length >= 2 && parts[1] == "offline" && parts[0] != myName)
                {
                    AddSystemAlert($"{parts[0]} has left the chat");
                }
            }
            break;

        case "ALERT":
            alertMessage = packet.Content;
            AddSystemAlert(packet.Content);
            refreshDisplay = true;
            break;

        case "GROUP_MEMBERS":
            // Store group members for display
            var members = packet.GroupList.Split(',')
                .Where(s => !string.IsNullOrEmpty(s)).ToList();

            Console.WriteLine($"\n Members of {packet.Content}:");
            foreach (var member in members)
            {
                Console.WriteLine($"   • {member}");
            }
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
            refreshDisplay = true;
            break;

        case "HISTORY_DATA":
            var historyParts = packet.Content.Split('|');
            foreach (var msg in historyParts)
            {
                if (!string.IsNullOrEmpty(msg))
                {
                    var msgParts = msg.Split(' ', 4);
                    if (msgParts.Length >= 4)
                    {
                        string timestamp = msgParts[0].Trim('[', ']');
                        string sender = msgParts[1].Trim('[', ']');
                        string content = msgParts[3];
                        string historyChatKey = packet.Content.Contains("group_") ? packet.Content.Replace("group_", "") : sender;

                        AddToHistory(historyChatKey, new ChatMessage
                        {
                            Sender = sender,
                            Content = content,
                            Timestamp = DateTime.Parse(timestamp),
                            Sequence = packet.Sequence
                        });
                    }
                }
            }
            break;

        case "KICKED":
            alertMessage = packet.Content;
            AddSystemAlert(packet.Content);
            AddToHistory(packet.To, new ChatMessage
            {
                Sender = "SYSTEM",
                Content = packet.Content,
                Timestamp = DateTime.Now,
                Sequence = packet.Sequence
            });
            if (currentWindow == packet.To || currentWindow.StartsWith(packet.To))
            {
                currentWindow = "";
                refreshDisplay = true;
            }
            break;

        case "MSG_SENT":
            UpdateMessageStatus(packet.Content, "Delivered");
            refreshDisplay = true;
            break;

        case "MSG_DELIVERED":
            UpdateMessageStatus(packet.Content, "Delivered");
            refreshDisplay = true;
            break;

        case "MSG_READ":
            UpdateMessageStatus(packet.Content, "Read");
            refreshDisplay = true;
            break;

        case "GRP_MSG":
            HandleIncomingGroupMessage(packet);
            break;

        case "PRIV":
            HandleIncomingPrivateMessage(packet);
            break;

        case "TYPING":
            if (packet.Content == "start" && currentWindow == packet.From)
            {
                Console.WriteLine($"\n[{packet.From} is typing...]");
                refreshDisplay = true;
            }
            break;
    }
}

void HandleIncomingGroupMessage(Packet packet)
{
    string message = packet.Content;

    AddToHistory(packet.To, new ChatMessage
    {
        MessageId = packet.MessageId,
        Sender = packet.From,
        Content = message,
        Timestamp = packet.SentTime,
        Sequence = packet.Sequence,
        Status = ""
    });

    if (!string.IsNullOrEmpty(packet.MessageId) && packet.From != myName)
    {
        _ = SendPacket("MSG_DELIVERED", "", packet.MessageId);
    }

    // Highlight join/leave messages
    if (packet.From == "SYSTEM" &&
        (message.Contains("joined") || message.Contains("left") ||
         message.Contains("disconnected") || message.Contains("kicked")))
    {
        AddSystemAlert($"{packet.To}: {message}");
    }

    if (currentWindow != packet.To)
        unreadCounts[packet.To] = unreadCounts.GetValueOrDefault(packet.To, 0) + 1;

    refreshDisplay = true;
}

void HandleIncomingPrivateMessage(Packet packet)
{
    string message = packet.Content;
    string chatKey = packet.From;

    AddToHistory(chatKey, new ChatMessage
    {
        MessageId = packet.MessageId,
        Sender = packet.From,
        Content = message,
        Timestamp = packet.SentTime,
        Sequence = packet.Sequence,
        Status = ""
    });

    if (!string.IsNullOrEmpty(packet.MessageId) && packet.From != myName)
    {
        _ = SendPacket("MSG_DELIVERED", "", packet.MessageId);
    }

    if (currentWindow == chatKey)
    {
        _ = SendPacket("MSG_READ", "", packet.MessageId);
        UpdateMessageStatus(packet.MessageId, "Read");
    }

    if (currentWindow != chatKey)
        unreadCounts[chatKey] = unreadCounts.GetValueOrDefault(chatKey, 0) + 1;

    refreshDisplay = true;
}

// ========== EXIT APPLICATION ==========
async Task ExitApp()
{
    Console.Clear();
    DisplayHeader();
    Console.WriteLine("\n Thank you for using NexChat!");

    SavePersistentHistory();

    try
    {
        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", CancellationToken.None);
        }
    }
    catch { }

    await Task.Delay(1000);
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

class ChatMessage
{
    public string MessageId { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public long Sequence { get; set; }
    public string Status { get; set; } = "";
}