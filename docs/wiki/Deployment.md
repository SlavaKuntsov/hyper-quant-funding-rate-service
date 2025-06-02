### Deployment

- [Home](Home.md)
- Components
  - [FRC_Service.Domain](FRC_Service-Domain.md)
  - FRC_Service.Infrastructure
    - [Data Access](FRC_Service-Infrastructure-DataAccess.md)
  - [FRC_Service.Application](FRC_Service-Application.md)
  - [FRC_Service.Presentation](FRC_Service-Presentation.md)
  - [FRC_Service](FRC_Service.md)
- [Versioning](Versioning.md)
- [Configuration](Configuration.md)
- **Deployment** (current)

### Overview

FRC_Service is containerized using [Docker](https://www.docker.com/get-started/) and can be deployed using [Docker](https://www.docker.com/get-started/)
Compose for development and testing environments. This page explains how to deploy the
application using these tools.

### Docker Compose Deployment

The project includes a docker-compose.yml file that defines the application stack:

```
version: '3.8'

services:
  frcservice:
    image: frcservice
    build:
      context: .
      dockerfile: FRC_Service/Dockerfile
    ports:
      - "5121:5121"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=db;Port=5488;Database=frcservicedb;Username=postgres;Password=postgres;
    depends_on:
      db:
        condition: service_healthy

  db:
    image: postgres:alpine
    container_name: frcservice-postgres
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
      - POSTGRES_DB=frcservicedb
      - PGPORT=5488
    volumes:
      - postgres-data:/var/lib/postgresql/data
    ports:
      - "5488:5488"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -p 5488"]
      interval: 5s
      timeout: 5s
      retries: 5
      start_period: 10s

volumes:
  postgres-data:
```

#### Components:

1. **frcservice**: The main application service
   - Built from the Dockerfile in the project
   - Exposes port 5121
   - Depends on the database service
   - Configured via environment variables
2. db: PostgreSQL database service
   - Runs on explicitly defined port 61579
   - Includes a health check to ensure readiness
   - Persists data using a named volume
   - Port is exposed to the host for external tools to connect

#### Deployment Steps:

1. Clone the repository:
```
git clone https://github.com/yourusername/FRC_Service.git
cd FRC_Service
```
2. Start the services:
```
docker-compose up -d
```
3. Verify the deployment:
```
docker-compose ps
```
4. Check the logs:
```
docker-compose logs -f FRC_Service
```
5. Access the API:
 - Swagger UI: http://localhost:5121/swagger
 - API Endpoints: http://localhost:5121/api/v1/...

#### Stopping the Services:

To stop the services but keep the data volumes:
```
docker-compose down
```

To stop the services and remove the data volumes:
```
docker-compose down -v
```