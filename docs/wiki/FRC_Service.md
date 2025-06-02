### FRC_Service.Domain

- [Home](Home.md)
- Components
  - [FRC_Service.Domain](FRC_Service-Domain.md)
  - FRC_Service.Infrastructure
    - [Data Access](FRC_Service-Infrastructure-DataAccess.md)
  - [FRC_Service.Application](FRC_Service-Application.md)
  - [FRC_Service.Presentation](FRC_Service-Presentation.md)
  - **FRC_Service** (current)
- [Versioning](Versioning.md)
- [Configuration](Configuration.md)
- [Deployment](Deployment.md)

### Overview

The FRC_Service project is the entry point and startup project for the entire application.
It serves as the composition root that wires together all the different layers of the application:

- Domain
- Infrastructure
- Application
- Presentation

### Key Components

#### Program.cs

The `Program.cs` file is the main entry point for the application:

``` csharp
using FRC_Service.Application.Extensions;
using FRC_Service.Infrastructure.Extensions;
using FRC_Service.Presentation.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => 
	config.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext());

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApplication();
builder.Services.AddPresentation();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.ApplyMigrations();

// Seed supported exchanges
await app.SeedExchangesAsync();

app.AddPresentation(app.Environment);

app.UseSerilogRequestLogging(options => {
	options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

app.Run();

public partial class Program { }
```

The startup process follows these steps:

1. **Serilog Configuration**: Sets up structured logging with Serilog, reading settings from
   the configuration
2. **Infrastructure Setup**: Registers infrastructure services including database access,
   repositories, and background jobs
3. **Application Setup**: Registers application services, AutoMapper, and validation
4. **Presentation Setup**: Registers controllers, API endpoints, and middleware
5. **Database Migrations**: Applies any pending database migrations
6. **Application Pipeline**: Configures the HTTP request pipeline with middleware

### Extension Methods

The startup project uses extension methods from each layer to configure the application:

#### Infrastructure Extensions

``` csharp
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

app.ApplyMigrations();

// Seed supported exchanges
await app.SeedExchangesAsync();
```

These methods:

- Configure the database context
- Register repositories
- Set up Quartz.NET for background jobs
- Apply any pending database migrations
- Adds supported exchanges

#### Application Extensions

``` csharp
builder.Services.AddApplication();
```

This method:

- Registers application services
- Configures validation
- Registers Problem Details for standardized error responses

#### Presentation Extensions

``` csharp
builder.Services.AddPresentation();

app.AddPresentation(app.Environment);
```

These methods:

- Register controllers
- Configure Swagger documentation
- Set up exception handling middleware
- Configure routing and endpoints

### Dependencies

The startup project references all other projects in the solution:

- FRC_Service.Domain: Core entities and business logic
- FRC_Service.Infrastructure: Database access, external integrations
- FRC_Service.Application: Application services and DTOs
- FRC_Service.Presentation: API controllers and middleware