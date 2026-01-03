Here’s a **professional and user-friendly `README.md`** you can use for your **NexChat-Group8-BSc-Software-Engineering-Project-Work** repository. It includes a clear **project overview**, **features**, and **step-by-step setup instructions** so anyone can run your project locally.

---

# NexChat — Real-Time Distributed Chat System

**NexChat** is a real-time distributed chat application built as a project for **Group 8 — BSc Software Engineering**. It provides instant messaging over networks using a **client–server model with WebSockets** for fast, two-way communication.

## 📌 Project Overview

NexChat enables users to:

* **Send and receive messages instantly** via WebSocket connections
* **Chat privately (1-on-1)** or in **groups**
* Track **delivery and read status**
* Persist chat history locally
* Handle **multiclient communication** over the network

This application is modular and works by running a **server** program and multiple **client** terminals that connect to it.

---

## 🚀 Features

✔ Real-time messaging using WebSockets
✔ Private and group chat support
✔ Persistent chat logs
✔ Delivery & read notifications
✔ Clean command-line interface
✔ Works across local network or single computer

---

## 🛠️ Technologies Used

* **C# / .NET** (Console apps)
* **WebSockets** for network communication
* JSON for chat history storage
* Cross-platform support (Windows / macOS / Linux)

---

## 📁 Repository Structure

```
/
├── Server/                  # WebSocket server application
├── Client/                  # Chat client program
├── websocket.sln            # Visual Studio / .NET solution
├── .gitignore
└── README.md
```

---

## 🧑‍💻 Getting Started — Setup on Your Computer

These steps assume you want to run NexChat **locally** on your machine.

### 📌 Prerequisites

Make sure you have:

✔ **.NET 6.0 SDK or higher** installed
✔ A terminal/command prompt (PowerShell, Bash, etc.)

You can download .NET here: [https://dotnet.microsoft.com/download/](https://dotnet.microsoft.com/download/)

---

### 1️⃣ Clone the Repository

```bash
git clone https://github.com/kyaotey/NexChat-Group8-BSc-Software-Engineering-Project-Work.git
cd NexChat-Group8-BSc-Software-Engineering-Project-Work
```

---

### 2️⃣ Run the Server

Navigate to the server project and start it:

```bash
cd Server
dotnet restore
dotnet run
```

💡 This starts the WebSocket server (default port **5000**).

---

### 3️⃣ Run One or More Clients

Open *another* terminal window for each user and run:

```bash
cd Client
dotnet restore
dotnet run
```

Each client will connect to the server and prompt for a **username**.

---

### 4️⃣ Connect & Chat

Once clients are running:

✔ Enter unique usernames
✔ Create or join chat groups
✔ Send messages in real time
✔ See message delivery status

---

## 🌐 Network Setup (Optional)

To run across computers on the same network:

1. Modify the server address in `Client/Program.cs` to use your server’s local IP
2. Ensure **port 5000** is open on your firewall/router
3. Clients connect using `ws://YOUR_SERVER_IP:5000/ws`

---

## 📦 Contribution Guide

Contributions are welcome! Here are the steps:

1. Fork this repository
2. Create a new branch (`feature/xyz`)
3. Make your changes
4. Commit with clear messages
5. Push to your fork
6. Open a Pull Request

---

## 🛟 Troubleshooting

**🔌 Connection refused**
✔ Ensure the server is running before starting clients
✔ Check that the firewall isn’t blocking port 5000

**🚫 Duplicate username**
✔ Always use unique usernames per client

---

## 📄 License

This project is licensed under **MIT License**.

---

## 👥 Team

Group 8 — BSc Software Engineering
Distributed Systems & Network Communication Specialists

---

If you want, I can also generate **a detailed setup script**, **environment diagrams**, or **quick start GIF/screenshots**!
