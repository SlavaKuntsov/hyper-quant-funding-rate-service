using FRC_Service.Presentation.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace FRC_Service.Presentation.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IApplicationBuilder"/> to configure Infrastructure project.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Applies pending database migrations to the database.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <param name="environment">Web host environment</param>
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