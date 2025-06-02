using FRC_Service.Application.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FRC_Service.Presentation.Middleware;

/// <summary>
/// Handles exceptions that were not handled by underlying services.
/// </summary>
/// <param name="next">Next</param>
/// <param name="logger">Logger</param>
/// <param name="environment">Web host environment to include more error information when runs in Development</param>
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

    /// <summary>
    /// Creates ProblemDetails object for validation error.
    /// </summary>
    /// <param name="context">Http context</param>
    /// <param name="validationException">Occured exception</param>
    /// <returns></returns>
    private static ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext context, ValidationException validationException)
    {
        var validationProblemDetails = new ValidationProblemDetails()
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Detail = "See the errors field for details.",
            Instance = context.Request.Path,
            Status = StatusCodes.Status400BadRequest
        };

        foreach (var error in validationException.Errors)
        {
            validationProblemDetails.Errors.Add(error.Key, error.Value);
        }

        return validationProblemDetails;
    }

    /// <summary>
    /// Creates ProblemDetails object for not found error.
    /// </summary>
    /// <param name="context">Http context</param>
    /// <param name="problemDetailsFactory">Problem Details Factory</param>
    /// <param name="notFoundException">Occured exception</param>
    /// <returns></returns>
    private static ProblemDetails CreateNotFoundProblemDetails(HttpContext context,
        ProblemDetailsFactory problemDetailsFactory, NotFoundException notFoundException)
    {
        var problemDetails = problemDetailsFactory.CreateProblemDetails(
            context,
            statusCode: StatusCodes.Status404NotFound,
            title: "Resource Not Found",
            detail: notFoundException.Message,
            instance: context.Request.Path);
        return problemDetails;
    }

    /// <summary>
    /// Creates ProblemDetails object for internal error.
    /// </summary>
    /// <param name="context">Http context</param>
    /// <param name="exception">Occured exception</param>
    /// <param name="problemDetailsFactory">Problem Details Factory</param>
    /// <returns></returns>
    private ProblemDetails CreateProblemDetails(
        HttpContext context, Exception exception, ProblemDetailsFactory problemDetailsFactory)
    {
        var problemDetails = problemDetailsFactory.CreateProblemDetails(
            context,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred",
            instance: context.Request.Path);

        if (environment.IsDevelopment())
        {
            problemDetails.Detail = exception.Message;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }
        else
        {
            problemDetails.Detail = "An internal server error has occurred.";
        }

        return problemDetails;
    }
}