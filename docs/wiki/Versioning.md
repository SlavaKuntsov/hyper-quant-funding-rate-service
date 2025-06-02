### Versioning

- [Home](Home.md)
- Components
  - [FRC_Service.Domain](FRC_Service-Domain.md)
  - FRC_Service.Infrastructure
    - [Data Access](FRC_Service-Infrastructure-DataAccess.md)
  - [FRC_Service.Application](FRC_Service-Application.md)
  - [FRC_Service.Presentation](FRC_Service-Presentation.md)
  - [FRC_Service](FRC_Service.md)
- **Versioning** (current)
- [Configuration](Configuration.md)
- [Deployment](Deployment.md)

This document provides a comprehensive overview of the FRC_Service project's
version history, highlighting key features, components, and enhancements introduced in
each release

### Version History

#### v0.1

**Initial Project Setup**
- Established basic project structure following Clean Architecture principles
- Configured structured logging with Serilog
- Set up Docker Compose for containerized deployment

#### v0.2

**Domain Model and Data Access**
- Added core domain entities and enumeration types
  - Exchange
  - FundingRateHistoryRepository
  - FundingRateOnlineRepository
  - ExchangeCodeType
- Implemented repository interfaces in Domain layer
  - IExchangeRepository
  - IFundingRateHistoryRepository
  - IFundingRateOnlineRepository
  - IUnitOfWork
- Added Data Access components in Infrastructure layer
  - ApplicationDbContext
  - Repository implementations

#### v0.3

**Application Services, API and Presentation Layer**

- Added Application layer with service interfaces and implementations
  - IFundingRateHistoryService
  - IFundingRateOnlineService
  - Services registration
- Implemented Data Transfer Objects (DTOs)
  - FundingRateDto
  - FuturesDataDto
- Added Presentation layer with REST API controllers
  - FundingRateHistoryController
  - FundingRateOnlineController
- Added global exception handling middleware
- Configured Swagger for API documentation

#### v0.4

**Application Services**

- Updated Application layer with service interfaces and implementations for all exchanges
  - IFundingRateHistoryService
  - IFundingRateOnlineService
  - Services registration
- Implemented Data Transfer Objects (DTOs)
  - ExchangeFundingRateDto
  - PaginationDto
  - SymbolFundingRatesDto
- Updated Presentation layer with REST API controllers
  - FundingRateHistoryController
  - FundingRateOnlineController