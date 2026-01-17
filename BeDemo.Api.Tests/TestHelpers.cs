using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BeDemo.Api.Data;

namespace BeDemo.Api.Tests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing to skip database initialization
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContextOptions registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove ApplicationDbContext registration
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Use PostgreSQL for tests - use a single test database
            // Add MaxPoolSize to prevent "too many clients already" error
            var connectionString = "Host=localhost;Port=5432;Database=bedemo_test;Username=bedemo_user;Password=bedemo_password;MaxPoolSize=20;Connection Lifetime=0";
            
            // Add PostgreSQL database with connection pooling settings
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(30); // 30 second timeout
                });
                // Enable sensitive data logging only in test environment
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);
            
            // Ensure database is created and migrated
            // Migrate() will create database if it doesn't exist and apply all migrations including UserProfiles table
            var serviceProvider = services.BuildServiceProvider();
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    // Migrate() creates database if it doesn't exist and applies all migrations
                    // This is the correct way to ensure database schema matches the latest migrations
                    context.Database.Migrate();
                }
                catch (Exception ex)
                {
                    // If Migrate() fails, try to delete and recreate (database might have been created without migrations)
                    try
                    {
                        context.Database.EnsureDeleted();
                        context.Database.Migrate();
                    }
                    catch (Exception ex2)
                    {
                        // If both fail, log the error but continue
                        // Connection might be temporarily unavailable
                        Console.WriteLine($"Warning: Database migration failed in test setup: {ex.Message}, retry: {ex2.Message}");
                    }
                }
            }
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }
}
