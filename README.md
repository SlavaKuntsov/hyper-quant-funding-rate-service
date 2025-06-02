# Hyper-Quant Funding Rate Service

A service for collecting, storing, and providing funding rates data from various cryptocurrency exchanges.

## Description

Hyper-Quant Funding Rate Service is a microservice designed to work with historical and current (online) funding rates from various cryptocurrency exchanges. The service provides data collection, storage, and API access to funding rate information.

## Supported Exchanges

- Binance
- Bybit
- HyperLiquid
- Mexc

## Features

### Core Functionality

- Collection and storage of historical funding rates
- Collection and storage of current (online) funding rates
- API for accessing funding rate data
- Background tasks for automatic data updates

### API Endpoints

#### Online Funding Rates

- `POST /api/v1/funding-online/{exchangeName}` - Trigger update of current funding rates for the specified exchange
- `GET /api/v1/funding-online/{exchangeName}` - Get current funding rates for the specified exchange
- `GET /api/v1/funding-online` - Get latest funding rates for all exchanges

#### Historical Funding Rates

- `GET /api/v1/funding-history/{exchangeName}` - Get historical funding rates for the specified exchange
- `GET /api/v1/funding-history` - Get historical funding rates across all exchanges

## Architecture

The service is built using a clean, layered (onion) architecture:

- **FRC_Service.Domain** - Domain model and repository interfaces
- **FRC_Service.Application** - Business logic and services
- **FRC_Service.Infrastructure** - Repository implementations and external system integrations
- **FRC_Service.Presentation** - API controllers and middleware
- **FRC_Service** - Application entry point and configuration

## Service Structure

### Domain Layer

Contains the core business entities and repository interfaces:

- **Models** - Entity classes (Exchange, FundingRateOnline, FundingRateHistory)
- **Enums** - Enumeration types (ExchangeCodeType)
- **Repositories** - Repository interfaces for data access

### Application Layer

Implements the business logic and services:

- **Services** - Exchange-specific and base services for funding rate operations
  - Base services: BaseFundingRateOnlineService, BaseFundingRateHistoryService
  - Exchange-specific services: BinanceFundingRateOnlineService, BybitFundingRateOnlineService, etc.
- **Dtos** - Data transfer objects for API responses
- **Abstractions** - Service interfaces and contracts
- **Extensions** - Extension methods for application services

### Infrastructure Layer

Provides concrete implementations of repositories and external system integrations:

- Database access implementations
- External API clients for cryptocurrency exchanges
- Cache and storage mechanisms

### Presentation Layer

Provides API access to the application:

- **Controllers** - API controllers for accessing funding rate data
- **Middleware** - Request/response processing, error handling
- **Extensions** - Service collection extensions for dependency registration

### Background Services

The service includes background jobs to periodically:

- Update online funding rates from all supported exchanges
- Collect and store historical funding rates data
- Clean up outdated data

## Requirements

- .NET 8.0 (or higher)
- PostgreSQL
- Docker and Docker Compose (for containerization)

## Running the Project

### Using Docker

```bash
docker-compose up -d
```

After startup, the service will be available at http://localhost:5121

### Database

The service uses PostgreSQL for data storage. Database connection settings are specified in the docker-compose.yml file:

```yaml
ConnectionStrings__DefaultConnection=Host=db;Port=5488;Database=frcservicedb;Username=postgres;Password=postgres;
```

## Domain Model

### Exchange

Represents a cryptocurrency exchange:

- Id - Unique identifier
- Code - Exchange type (Binance, Bybit, HyperLiquid, Mexc)

### FundingRateOnline

Represents current funding rates:

- Id - Unique identifier
- Symbol - Trading pair (e.g., 'BTCUSDT')
- Name - Coin name
- ExchangeId - Exchange identifier
- Rate - Funding rate value
- OpenInterest - Volume of open positions
- TsRate - Rate timestamp
- FetchedAt - Data retrieval timestamp

### FundingRateHistory

Represents historical funding rate data:

- Id - Unique identifier
- ExchangeId - Exchange identifier
- Symbol - Trading pair
- Name - Coin name
- IntervalHours - Rate update interval in hours
- Rate - Funding rate value
- OpenInterest - Volume of open positions
- TsRate - Rate timestamp
- FetchedAt - Data retrieval timestamp

## Usage

1. Start the service using Docker Compose
2. Use the API to access funding rate data

## Service Workflow

1. **Data Collection**: The service periodically connects to supported exchanges to fetch the latest funding rates
2. **Data Processing**: Collected data is processed and transformed into the service's domain model
3. **Data Storage**: Processed data is stored in the PostgreSQL database
4. **Data Access**: Users can access both current and historical funding rates via the REST API

## License

Copyright (c) 2024 Hyper-Quant