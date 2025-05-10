# BlackHolio

A multiplayer game built with Unity and SpacetimeDB where players control circles in an arena, consuming food to grow larger and other players to dominate the game.

## Project Structure

This project consists of two main components:

- **client-unity**: Unity client application that handles game rendering and user input
- **server-csharp**: SpacetimeDB server module that manages game state and logic

## Prerequisites

- [Unity](https://unity.com/download) (Unity 2021 LTS or newer recommended)
- [.NET SDK](https://dotnet.microsoft.com/download) (for server development)
- [SpacetimeDB](https://spacetimedb.com/) SDK and CLI tools

## Getting Started

### Setting Up the Server

1. Navigate to the server-csharp directory
2. 



### Running the Unity Client

1. Open the `client-unity` directory in Unity Hub
2. Open the main scene in `Assets/Scenes`
3. Press Play in the Unity Editor to run the game

By default, the client connects to a local server running on `http://127.0.0.1:3000`. This can be changed in `GameManager.cs` by modifying the `SERVER_URL` constant.

## Gameplay

- Players control circular entities in a 2D arena
- Consume food particles to grow larger
- Larger players can consume smaller ones
- The goal is to become the largest entity in the arena

## Deployment

To deploy to SpacetimeDB maincloud:

1. Update the `SERVER_URL` in `GameManager.cs` to point to `https://maincloud.spacetimedb.com`
2. Update the `MODULE_NAME` to your deployed module name (e.g., "blackholio-databasepj")
3. Build and deploy the server module using SpacetimeDB CLI
4. Build the Unity client for your target platform