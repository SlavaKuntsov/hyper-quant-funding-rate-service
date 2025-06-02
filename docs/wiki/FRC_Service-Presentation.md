### FRC_Service.Presentation

- [Home](Home.md)
- Components
  - [FRC_Service.Domain](FRC_Service-Domain.md)
  - FRC_Service.Infrastructure
    - [Data Access](FRC_Service-Infrastructure-DataAccess.md)
  - [FRC_Service.Application](FRC_Service-Application.md)
  - **FRC_Service.Presentation** (current)
  - [FRC_Service](FRC_Service.md)
- [Versioning](Versioning.md)
- [Configuration](Configuration.md)
- [Deployment](Deployment.md)

### Overview

The Presentation layer exposes REST API that allow clients to interact with the system. This
layer is responsible for:

- Exposing HTTP endpoints through controllers
- Converting HTTP requests to application DTOs
- Converting application responses to HTTP responses
- Handling exceptions and generating appropriate HTTP status codes
- Configuring middleware components for cross-cutting concerns
- Providing API documentation through Swagger

### API Endpoints

#### FundingRateHistory Controller

| **Method** | **Endpoint**                                                   | **Description**                                                               |
| ---------- | -------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| POST       | /api/v1/funding-history/{exchangeName}                         | Updates historical funding rates for the specified exchange                   |
| GET        | /api/v1/funding-history/{exchangeName}/latest                  | Gets latest historical funding rates for the specified exchange               |
| GET        | /api/v1/funding-history/{exchangeName}/symbol/{symbol}/history | Gets historical funding rates for a specific symbol on the specified exchange |
| GET        | /api/v1/funding-history/latest                                 | Gets latest funding rates for all symbols across all exchanges from history   |

#### FundingRateHistory Controller

| **Method** | **Endpoint**                          | **Description**                                              |
| ---------- | ------------------------------------- | ------------------------------------------------------------ |
| POST       | /api/v1/funding-online/{exchangeName} | Updates online funding rates for the specified exchange      |
| GET        | /api/v1/funding-online/{exchangeName} | Gets current online funding rates for the specified exchange |
| GET        | /api/v1/funding-online                | Gets latest online funding rates for all exchanges           |

### Key Components

#### API Controllers

The Presentation layer implements RESTful API controllers that handle HTTP requests and
responses. All controllers follow a consistent pattern and use dependency injection for their
dependencies

#### FundingRateHistoryController

The `FundingRateHistoryController` fetched historical funding-related operations:

``` csharp
[ApiController]
[Route("api/v1/funding-history")]
[Produces("application/json")]
[Tags("FundingHistory")]
public class FundingRateHistoryController(BinanceFundingRateHistoryService binanceService) : ControllerBase
{
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FundingResultDto))]
	public async Task<ActionResult> Create(
		[FromRoute] ExchangeCodeType exchangeCodeType = ExchangeCodeType.BIN, 
		CancellationToken cancellationToken = default)
	{
		var fundings = exchangeCodeType switch
		{
			ExchangeCodeType.BIN => await binanceService.UpdateHistoricalFundingsAsync(cancellationToken),
			ExchangeCodeType.BYB or ExchangeCodeType.HL or ExchangeCodeType.MEX =>
				throw new NotImplementedException("Exchange code not implemented"),
			_ => null
		};

		return Ok(new FundingResultDto(fundings.Count(), fundings));
	}
	
	// Other endpoints for CRUD operations...
}
```

#### FundingRateOnlineController

The `FundingRateOnlineController` fetched historical funding-related operations:

``` csharp
[ApiController]
[Route("api/v1/funding-online")]
[Produces("application/json")]
[Tags("FundingOnline")]
public class FundingRateOnlineController(BinanceFundingRateOnlineService binanceService)
	: ControllerBase
{
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FundingResultDto))]
	public async Task<ActionResult> GetBinance(
		[FromRoute] ExchangeCodeType exchangeCodeType = ExchangeCodeType.BIN,
		CancellationToken cancellationToken = default)
	{
		var fundings = exchangeCodeType switch
		{
			ExchangeCodeType.BIN => await binanceService.UpdateOnlineFundingsAsync(cancellationToken),
			ExchangeCodeType.BYB or ExchangeCodeType.HL or ExchangeCodeType.MEX =>
				throw new NotImplementedException("Exchange code not implemented"),
			_ => null
		};
		
		return Ok(new FundingResultDto(fundings.Count(), fundings));
	}
	
	// Other endpoints for CRUD operations...
}
```

### Request/Response DTOs

The controllers use several DTOs for request and response handling:

**FundingResultDto**: Request DTO after fetching funding rated data

### Global Exception Handling

The Presentation layer includes middleware for handling exceptions and converting them to
appropriate HTTP responses:

``` csharp
public class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger,
    IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            logger.LogError(
                "An unhandled exception occurred. TraceId: {TraceId}, Error: {Error}", traceId, ex.Message);
            var problemDetailsFactory = context.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            await HandleExceptionAsync(context, ex, problemDetailsFactory);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context, 
        Exception exception, 
        ProblemDetailsFactory problemDetailsFactory)
    {
        context.Response.ContentType = "application/problem+json";

        // Handle validation error separately, cause ProblemDetails does not have Problems
        // property unlike ValidationProblemDetails.
        if (exception is ValidationException validationException)
        {
            var validationProblemDetails = CreateValidationProblemDetails(context, validationException);
            context.Response.StatusCode = validationProblemDetails.Status!.Value;
            await context.Response.WriteAsJsonAsync(validationProblemDetails);
            return;
        }
        
        // Creates problem details for left exceptions.
        var problemDetails = exception switch
        {
            NotFoundException notFoundException
                => CreateNotFoundProblemDetails(context, problemDetailsFactory, notFoundException),
            _ => CreateProblemDetails(context, exception, problemDetailsFactory)
        };

        context.Response.StatusCode = problemDetails.Status!.Value;
        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    // Helper methods for creating problem details...
}
```

The middleware is registered in the application pipeline using an extension method:

``` csharp
public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder AddPresentation(
        this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        
        // If runs in development environment, configures Swagger to test endpoints.
        if (environment.IsDevelopment())
        {
            app.UseSwagger(c =>
            {
                c.SerializeAsV2 = false;
                c.RouteTemplate = "swagger/{documentName}/swagger.json";
            });
        
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "FRC_Service API v1");
            });
        }
        
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
        
        return app;
    }
}
```

### Service Registration

The presentation layer registers its services through an extension method:

``` csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.WriteIndented = true;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddEndpointsApiExplorer();

        // Configures Swagger, that will be used in Development environment.
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "FRC Service API",
                Version = "v1",
                Description = "API for managing funding rates"
            });

            // Configure xml documentation for Swagger endpoints and objects. 
            var presentationXmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var presentationXmlPath = Path.Combine(AppContext.BaseDirectory, presentationXmlFile);
            if (File.Exists(presentationXmlPath))
            {
                options.IncludeXmlComments(presentationXmlPath);
            }
            
            var applicationXmlFile = "FRC_Service.Application.xml";
            var applicationXmlPath = Path.Combine(AppContext.BaseDirectory, applicationXmlFile);
            if (File.Exists(applicationXmlPath))
            {
                options.IncludeXmlComments(applicationXmlPath);
            }
            
            // Configures enums to be serialized into strings instead of integers.
            options.UseInlineDefinitionsForEnums();
        });

        return services;
    }
}
```

### API Documentation

The Presentation layer provides API documentation through Swagger/OpenAPI:

- **Swagger UI**: Available at /swagger in development environment
- **OpenAPI Specification**: Provides machine-readable API documentation
- **Response Types**: Each endpoint documents its possible response types

### Swagger Configuration

The Swagger UI is configured and made available in the development environment:

``` csharp
if (environment.IsDevelopment())
{
    app.UseSwagger(c =>
    {
        c.SerializeAsV2 = false;
        c.RouteTemplate = "swagger/{documentName}/swagger.json";
    });
    
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FRC_Service API v1");
    });
}
```