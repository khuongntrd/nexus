# Nexus

A .NET-based integration platform that connects multiple task management and collaboration tools.

## Overview

Nexus provides seamless synchronization and integration between various external services including:
- GitHub
- Jira
- Microsoft Todo

## Project Structure

- **src/Nexus.Web** - Main web application built with ASP.NET Core
- **src/Nexus.Application** - Application layer with business logic
- **src/Nexus.Core** - Core domain entities and value objects
- **src/Nexus.Infrastructure** - Infrastructure services and data access
- **src/Nexus.Connectors** - Connector plugins for third-party integrations
- **tests** - Unit tests for application and infrastructure layers

## Getting Started

### Prerequisites
- .NET 10 SDK or later
- Docker (for containerized deployment)

### Build
```bash
dotnet build Nexus.slnx
```

### Run
```bash
dotnet run --project src/Nexus.Web/Nexus.Web.csproj
```

### Test
```bash
dotnet test --project tests/Nexus.Application.Tests/Nexus.Application.Tests.csproj
dotnet test --project tests/Nexus.Infrastructure.Tests/Nexus.Infrastructure.Tests.csproj
```

## Docker Deployment

Build and run the containerized application:
```bash
docker-compose up
```

Or publish directly:
```bash
dotnet publish src/Nexus.Web/Nexus.Web.csproj --os linux --arch x64 -p:PublishProfile=DefaultContainer -c Release
```

## Development

- Configuration files: `appsettings.json`, `appsettings.Development.json`
- Database migrations located in `src/Nexus.Infrastructure/Migrations/`
- Style guidelines enforced by StyleCop configuration

## License

[Add your license here]
