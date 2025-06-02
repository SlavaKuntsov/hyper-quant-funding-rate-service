### Configuration

- [Home](Home.md)
- Components
  - [FRC_Service.Domain](FRC_Service-Domain.md)
  - FRC_Service.Infrastructure
    - [Data Access](FRC_Service-Infrastructure-DataAccess.md)
  - [FRC_Service.Application](FRC_Service-Application.md)
  - [FRC_Service.Presentation](FRC_Service-Presentation.md)
  - [FRC_Service](FRC_Service.md)
- [Versioning](Versioning.md)
- **Configuration** (current)
- [Deployment](Deployment.md)

### Overview

FRC_Service uses the standard .NET configuration system with appsettings.json
files. This page explains the configuration options available in the application and how to
customize them for different environments.

### Configuration File Structure

The main configuration file is appsettings.json . Here's an overview of its structure:

```
{
  "Serilog": {
    // Logging configuration
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    // Database connection strings
  },
  "BackgroundJobs": {
    // Quartz.NET job schedules
  }
}
```

### Logging Configuration (Serilog)

FRC_Service uses Serilog for structured logging. The configuration defines log levels,
outputs, and formatting:

```
"Serilog": {
   "Using": [ "Serilog.Sinks.Console" ],
   "MinimumLevel": {
     "Default": "Information"
   },
   "WriteTo": [
     {
      "Name": "Console",
      "Args": {
        "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}"
      }
     }
   ],
   "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
}
```

#### Key Settings:
 - Using: List of Serilog sinks to use (e.g., Console, File)
 - MinimumLevel: Defines the minimum log level to capture
 - WriteTo: Configuration for output destinations
 - Enrich: Additional context to include in log entries

### Database Connection

```
"ConnectionStrings": {
   "DefaultConnection": "Host=db;Port=5488;Database=frcservicedb;Username=postgres;Password=postgres;IncludeErrorDetail=true"
}
```

#### Key Settings:

 - Host: Database server hostname (e.g., db for Docker, localhost for local
 - development)
 - Database: Name of the database
 - Username/Password: Database credentials
 - IncludeErrorDetail: Whether to include detailed error information (useful for development)