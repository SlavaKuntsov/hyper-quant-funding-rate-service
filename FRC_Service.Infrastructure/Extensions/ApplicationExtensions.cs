using FRC_Service.Infrastructure.DataAccess;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FRC_Service.Infrastructure.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IApplicationBuilder"/> to configure Infrastructure project.
/// </summary>
public static class ApplicationExtensions
{
    /// <summary>
    /// Applies pending database migrations to the database.
    /// </summary>
    /// <param name="app">The application builder</param>
    public static IApplicationBuilder ApplyMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations");
            throw;
        }

        return app;
    }
}