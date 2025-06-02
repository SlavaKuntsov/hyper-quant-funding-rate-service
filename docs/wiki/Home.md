### FRC_Service Wiki

- **Home** (current)
- Components
  - [FRC_Service.Domain](FRC_Service-Domain.md)
  - FRC_Service.Infrastructure
    - [Data Access](FRC_Service-Infrastructure-DataAccess.md)
  - [FRC_Service.Application](FRC_Service-Application.md)
  - [FRC_Service.Presentation](FRC_Service-Presentation.md)
  - [FRC_Service](FRC_Service.md)
- [Versioning](Versioning.md)
- [Configuration](Configuration.md)
- [Deployment](Deployment.md)

### Welcome to FRC_Service

### Supported Exchanges

Currently, FRC_Service supports the following exchanges for funding:

 - Binance
 - Bybit
 - HyperLiquid
 - Mexc

### How It Works

The system operates on a scheduled basis, performing the following steps:

1. Fetch current symbols from exchange APIs
2. Request funding for each symbol
3.  Update the database to reflect the current state

### Project Structure

The project follows Clean Architecture principles with the following layers:

 - Domain: Contains the core business entities and repository
interfaces
 - Application: Houses application services, DTOs, and business logic
 - Infrastructure: Implements data access, external API clients (Binance) and background
jobs
 - Presentation: Exposes Web API endpoints and handles HTTP requests
 - FRC_Service: Startup project

### Getting Started

To set up the project for development, see the Deployment page which includes information
on prerequisites and configuration.
