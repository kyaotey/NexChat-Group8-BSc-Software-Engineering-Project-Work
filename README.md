NexChat – Distributed Communication System

Group Project – BSc Software Engineering
CSBC 311 – Distributed Systems
Ghana Communication Technology University

Project Overview

NexChat (from “nexus” meaning connection and “chat” for messaging) is a distributed chat application designed to support both group messaging and private one-to-one communication among users connected over a network.

The system simulates a real-world distributed environment where users communicate using message passing rather than shared memory. It demonstrates key distributed systems concepts such as active membership management, fault detection, message delivery reliability, and persistence.

Core Features

Messaging

Group messaging (multicast)

Private one-to-one messaging (unicast)

Group Management

Create groups

Join and leave groups

Track active group members

Reliability and Distributed Features

Heartbeat-based fault detection

Online and offline user tracking

Offline message queuing and later delivery

Message delivery and read acknowledgments

Persistent chat history using JSON files

Technologies Used

Programming Language: C# (.NET)

Communication Protocol: WebSockets

Data Storage: JSON files

Architecture: Client–Server distributed model

Project Structure

NexChat

Server

Program.cs

Server.csproj

ChatHistory (auto-created at runtime)

Client

Program.cs

Client.csproj

README file

.gitignore

Both the Server and Client are console-based applications that communicate using WebSockets.

Prerequisites

Before running the application, ensure the following are installed:

.NET SDK version 6.0 or higher

A supported operating system (Windows, macOS, or Linux)

Network connectivity for multi-machine execution

Setup and Run Instructions

Step 1: Clone the Repository

Run the following command in your terminal or command prompt:
