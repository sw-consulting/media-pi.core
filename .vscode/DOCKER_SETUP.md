# VS Code Docker Development Setup

This project is configured for Docker-based development. Follow these steps to get started.

## Prerequisites

- Docker Desktop installed and running
- VS Code with extensions: C# Dev Kit, Docker, and others (auto-prompted on workspace open)

## Development Workflow

### Option 1: Local Development with Docker Compose (Recommended)

**1. Start the Docker containers:**
```bash
Cmd+Shift+P → Run Task → docker-compose-up
```

This will:
- Start the PostgreSQL database
- Build and start the MediaPi.Core API container
- Start the Adminer database UI (http://localhost:8080)

**2. Verify services are running:**
```bash
Cmd+Shift+P → Run Task → docker-compose-logs-all
```

**3. Access the application:**
- API: http://localhost:8084
- Swagger UI: http://localhost:8084/swagger
- HTTPS API: https://localhost:8085
- Database UI (Adminer): http://localhost:8080

**4. View logs:**
```bash
Cmd+Shift+P → Run Task → docker-compose-logs
```

### Option 2: Remote Container Development

Use VS Code Dev Containers to develop inside the container:

```bash
Cmd+Shift+P → Dev Containers: Reopen in Container
```

This will:
- Open VS Code inside the container
- Provide full .NET development environment
- All source code changes reflect in the container automatically

## Common Tasks

| Task | Command |
|------|---------|
| Start containers | `Cmd+Shift+P → Run Task → docker-compose-up` |
| Stop containers | `Cmd+Shift+P → Run Task → docker-compose-down` |
| Rebuild containers | `Cmd+Shift+P → Run Task → docker-compose-rebuild` |
| View logs | `Cmd+Shift+P → Run Task → docker-compose-logs` |
| View all logs | `Cmd+Shift+P → Run Task → docker-compose-logs-all` |
| Shell into API | `Cmd+Shift+P → Run Task → docker-compose-api-shell` |
| Shell into DB | `Cmd+Shift+P → Run Task → docker-compose-db-shell` |

## Debugging

### Using DevTools Attach

1. Ensure containers are running: `docker-compose-up`
2. Press `F5` to open debugger
3. Select "Docker Compose - Attach"
4. Choose the `dotnet` process from the API container
5. Set breakpoints and debug normally

### View Database

Access the Adminer UI at http://localhost:8080:
- Server: `db`
- User: `postgres`
- Password: `postgres`
- Database: `mediapi`

## Troubleshooting

### Exit code 134 from dotnet run
This happens when running locally without Docker. The app requires the PostgreSQL database and other services from Docker Compose.

**Solution:** Always use `docker-compose-up` instead of `dotnet run`.

### Database connection refused
Ensure the database container is healthy:
```bash
docker compose ps
```

Wait for `db` to show status `healthy (Up ...)`

### Port conflicts
If ports 5432, 8084, or 8085 are already in use, stop other services or modify docker-compose.yml port mappings.

## File Structure

```
.vscode/
  ├── launch.json       # Debug configurations
  ├── tasks.json        # Docker & build tasks
  ├── settings.json     # Workspace settings
  └── extensions.json   # Recommended extensions

.devcontainer/
  └── devcontainer.json # Remote container setup

.editorconfig           # Code style rules
```

## More Information

- See the main [README.md](../README.md) for deployment instructions
- View [docker-compose.yml](../docker-compose.yml) for container configuration
