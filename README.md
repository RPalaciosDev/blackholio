# Blackholio - SpacetimeDB Demo

## Prerequisites

- Docker
- Unity 6.1
- Unity Hub

## Setup Instructions

### 1. Install SpacetimeDB

Visit [https://spacetimedb.com/install](https://spacetimedb.com/install) to find installation images for all systems. We'll be using Docker for our setup, which:

- Skips the need to install Spacetime CLI
- Provides an extra layer of insulation between the server and OS
- Contains all necessary dependencies for Spacetime DB
- Eliminates the need to install .NET 8 on local machines

### 2. Set Up Docker Container

1. Navigate to or create a directory where you want to store your projects
2. Open a terminal and run:

```bash
docker run --pull always -p 3000:3000 --name spacetimedb -v <path to your directory>:/stdb clockworklabs/spacetime start --data-dir=/stdb
```

This mounts the directory `stdb` in the container to your specified directory.

> **Note:** Keep the terminal window open

### 3. Clone the Repository

1. In your mounted directory, create a `Projects` directory
2. Inside `Projects`
3. Clone the repository:

```bash
git clone https://github.com/RPalaciosDev/blackholio.git
```

### 4. Access and Configure the Container

1. Open a new terminal window and run:

```bash
docker exec -it <container_name> bash
```

2. Navigate to the mounted directory:

```bash
cd ../stdb/Projects
```

3. Enter the project directory and navigate to `server-csharp`

4. Publish the server:

```bash
spacetime publish --project-path . blackholio
```

You may be prompted to log into spacetimedb.com. After successful execution, you should see output similar to:

```
Saving config to /home/spacetime/.config/spacetime/cli.toml.
Saving config to /home/spacetime/.config/spacetime/cli.toml.
Optimising module with wasm-opt...
Build finished successfully.
Uploading to local => http://127.0.0.1:3000
Publishing module...
Updated database with name: blackholio, identity: c200d8bce9ef0d1b74e4a45292e14e7957ec46cdc6dae5862189159301aa9705
```

> **Reference:** For additional CLI commands, visit [SpacetimeDB CLI Reference](https://spacetimedb.com/docs/cli-reference)

### 5. Unity Setup

1. Download Unity from [https://unity.com/download](https://unity.com/download)
2. Open Unity Hub
3. Import the `unity-client` from the cloned repository
4. Open the project using Unity 6.1

### 6. Running the Demo

1. In Unity, navigate to `Assets > Scenes`
2. Double-click `SpacetimeDemoScene`
3. Click the play button at the top of the screen
4. The player character will follow the mouse

### 7. Multiplayer Configuration

To enable multiplayer functionality:

1. Navigate to `Assets > Scripts`
2. Open `GameManager.cs`
3. Locate and modify these settings:

```csharp
// Change this to https://maincloud.spacetimedb.com
// If deployed to maincloud
const string SERVER_URL = "http://127.0.0.1:3000";
// Name of your deployed server module
// Change this to blackholio-databasepj for group testing
const string MODULE_NAME = "blackholio";
```

### Publishing to MainCloud

To publish your server to SpacetimeDB's MainCloud, run:

```bash
spacetime publish --project-path . -s maincloud <module-name>
```

from inside the `client-server` directory.
