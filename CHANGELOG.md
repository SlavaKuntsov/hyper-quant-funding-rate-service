# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [0.4.0] — 2025-06-02

### Added
- Background jobs system:
  - Base extensible job implementation.
  - Jobs for all exchanges.

## [0.4.0] — 2025-05-31

### Added
- Application layer for all exchanges:
  - Interfaces for services.
  - Service implementations.
  - Dtos.

## [0.3.0] — 2025-05-10

### Added
- Application layer:
  - Interfaces for services.
  - Service implementations.
  - Dtos.
- Controllers.
- Global middleware for exception handling.
- Swagger configuration for API documentation.

## [0.2.0] — 2025-05-07

### Added
- Domain models.
- Data access layer:
  - `ApplicationDbContext`.
  - `ApplicationDbContextDesignTimeFactory`.
  - Repositories and their interfaces.
  - Unit of Work.

## [0.1.0] — 2025-04-25

### Added
- Basic project structure.
- Logging configured via Serilog.
- Docker and Docker Compose support.