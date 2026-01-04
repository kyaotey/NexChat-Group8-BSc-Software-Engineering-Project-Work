
---

# NexChat – Real-Time WebSocket Chat Application

**Group 8 – BSc Software Engineering Project Work**
Ghana Communication Technology University (GCTU)
CSBC 311 – Distributed Systems – Level 300, First Semester 2025

---

## 📌 Project Description

NexChat is a **real-time distributed chat application** that allows multiple users to connect across a network and communicate instantly.

The system demonstrates **distributed systems concepts**, including:

* Group messaging (multicast)
* Private messaging (unicast)
* Active membership tracking
* Basic reliability features

**System Composition:**

* **One WebSocket Server**
* **Multiple Client applications**

---

## 📂 Project Structure

```
/
├── websocket/               # WebSocket Server application
├── websocket.client/        # Client application (Client 1)
├── websocket.client2/       # Client application (Client 2)
├── websocket.sln            # Visual Studio solution file
├── .gitignore
├── .gitattributes
└── README.md
```

---

## 🚀 Features

* Real-time messaging using WebSockets
* Multiple client support
* Client–server communication model
* Console-based user interface
* Distributed systems concepts:

  * Group and private messaging
  * Active member tracking
  * Basic message logging and reliability

---

## 🛠️ Technologies Used

* **C#**
* **.NET 6.0** (Console Applications)
* **WebSocket protocol**
* Visual Studio / .NET CLI

---

## 🧑‍💻 System Requirements

* .NET 6.0 SDK or higher
* Visual Studio (recommended) or any C# compatible IDE
* Terminal or command prompt

---

## ⚙️ Setup and Run Instructions

1️⃣ **Clone the Repository**

```bash
git clone https://github.com/kyaotey/NexChat-Group8-BSc-Software-Engineering-Project-Work.git
cd NexChat-Group8-BSc-Software-Engineering-Project-Work
```

2️⃣ **Open the Solution**

Open `websocket.sln` in Visual Studio. This loads:

* The server project
* Both client projects

3️⃣ **Run the Server**

```bash
cd websocket
dotnet restore
dotnet run
```

✔ The server starts and listens for incoming WebSocket connections.

4️⃣ **Run the Clients**

Open separate terminal windows for each client.

**Client 1:**

```bash
cd websocket.client
dotnet restore
dotnet run
```

**Client 2:**

```bash
cd websocket.client2
dotnet restore
dotnet run
```

✔ Each client connects to the server and can send/receive messages.

---

## 💬 How the System Works

1. The server starts and listens for client connections
2. Clients connect to the server using WebSockets
3. Messages sent by a client are routed through the server
4. Other clients receive the messages in real time

---

## 🧪 Core Feature Implementation

### 1️⃣ Group Management

```csharp
Dictionary<string, List<WebSocket>> groups = new Dictionary<string, List<WebSocket>>();

public void JoinGroup(string groupName, WebSocket client)
{
    if (!groups.ContainsKey(groupName))
        groups[groupName] = new List<WebSocket>();
    groups[groupName].Add(client);
    Console.WriteLine($"Client joined group {groupName}");
}

public void LeaveGroup(string groupName, WebSocket client)
{
    if (groups.ContainsKey(groupName))
        groups[groupName].Remove(client);
}
```

---

### 2️⃣ Group Communication

```csharp
public async Task SendToGroup(string groupName, string message)
{
    if (groups.ContainsKey(groupName))
    {
        foreach (var client in groups[groupName])
        {
            if (client.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await client.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
```

---

### 3️⃣ Private Messaging

```csharp
public async Task SendPrivateMessage(WebSocket recipient, string message)
{
    if (recipient.State == WebSocketState.Open)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await recipient.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
```

---

### 4️⃣ Reliability Features

```csharp
// Detect client disconnection
private async Task MonitorClients(WebSocket client)
{
    while (client.State == WebSocketState.Open)
        await Task.Delay(1000);

    Console.WriteLine("Client disconnected.");
}

// Simple message log
List<string> messageLog = new List<string>();
messageLog.Add($"{DateTime.Now}: {message}");
```

---

## 🖼️ System Architecture Diagram

```
                ┌─────────────────────┐
                │      WebSocket      │
                │       Server        │
                │ - Track Groups      │
                │ - Track Clients     │
                │ - Route Messages    │
                └─────────┬──────────┘
                          │
       ┌──────────────────┴──────────────────┐
       │                                     │
┌───────────────┐                     ┌───────────────┐
│  Client 1     │                     │  Client 2     │
│ - Connects    │                     │ - Connects    │
│ - Joins Group │                     │ - Joins Group │
│ - Sends Msg   │                     │ - Sends Msg   │
│ - Receives Msg│                     │ - Receives Msg│
└───────────────┘                     └───────────────┘
```

---

## 🧪 Testing the Application

1. Run the server first
2. Run at least two clients
3. Send messages from one client and observe real-time delivery on the other

---

## 📚 Academic Relevance

* Demonstrates **client–server architecture**
* Real-time communication
* Distributed systems principles:

  * Group messaging
  * Private messaging
  * Active membership
* Network programming using WebSockets

---

## 👥 Team

**Group 8**
BSc Software Engineering
Ghana Communication Technology University (GCTU)

---

