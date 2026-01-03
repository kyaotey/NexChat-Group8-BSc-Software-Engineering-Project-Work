

---

# NexChat – Real-Time WebSocket Chat Application

NexChat is a **real-time distributed chat application** developed as part of **Group 8 – BSc Software Engineering Project Work**.
The system uses a **client–server architecture** built with **WebSockets** to enable fast and reliable communication between multiple users.

---

## 📌 Project Description

NexChat allows users to connect to a central server and exchange messages instantly.
The project demonstrates key concepts in **network programming**, **distributed systems**, and **real-time communication**.

The system is composed of:

* One **WebSocket Server**
* Multiple **Client applications**

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
* Demonstrates distributed systems concepts

---

## 🛠️ Technologies Used

* **C#**
* **.NET (Console Applications)**
* **WebSocket protocol**
* **Visual Studio / .NET CLI**

---

## 🧑‍💻 System Requirements

Before running the project, ensure you have:

* **.NET 6.0 SDK or higher**
* **Visual Studio** (recommended) or any C# compatible IDE
* A terminal or command prompt

---

## ⚙️ How to Set Up and Run the Project

### 1️⃣ Clone the Repository

```bash
git clone https://github.com/kyaotey/NexChat-Group8-BSc-Software-Engineering-Project-Work.git
cd NexChat-Group8-BSc-Software-Engineering-Project-Work
```

---

### 2️⃣ Open the Solution (Recommended)

Open `websocket.sln` using **Visual Studio**.

This loads:

* The server project
* Both client projects

---

### 3️⃣ Run the Server

Using terminal:

```bash
cd websocket
dotnet restore
dotnet run
```

✔ The server will start and listen for incoming WebSocket connections.

---

### 4️⃣ Run the Clients

Open **separate terminal windows** for each client.

#### Client 1

```bash
cd websocket.client
dotnet restore
dotnet run
```

#### Client 2

```bash
cd websocket.client2
dotnet restore
dotnet run
```

✔ Each client connects to the server and can send/receive messages.

---

## 💬 How the System Works

1. The **server** starts and listens for connections
2. Clients connect to the server using WebSockets
3. Messages sent by one client are relayed through the server
4. Other connected clients receive the messages in real time

---

## 🧪 Testing the Application

* Run the server first
* Run at least two clients
* Send messages from one client and observe real-time delivery on the other

---

## 🛠️ Troubleshooting

**Server not responding**

* Ensure the server is running before starting clients

**Connection issues**

* Confirm server address and port are correct
* Disable firewall temporarily if needed

---

## 📚 Academic Relevance

This project demonstrates:

* Client–server architecture
* Real-time communication
* Distributed systems principles
* Network programming using WebSockets

---

## 👥 Team

**Group 8**
BSc Software Engineering
Ghana Communication Technology University (GCTU)

---

## 📄 License

This project is for **academic purposes**.

---

