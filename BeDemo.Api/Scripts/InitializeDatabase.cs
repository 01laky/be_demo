using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Scripts;

/// <summary>
/// Script to initialize database and create default admin user
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Initialize database and create admin user if it doesn't exist
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            // Run migrations (this will create database if it doesn't exist)
            // MigrateAsync is safer than EnsureCreatedAsync when using migrations
            await context.Database.MigrateAsync();

            // Check if admin user exists (by email)
            var adminUser = await userManager.FindByEmailAsync("admin@admin.com");
            if (adminUser == null)
            {
                // Create admin user
                // Set both UserName and Email to "admin@admin.com" so login works with email
                adminUser = new ApplicationUser
                {
                    UserName = "admin@admin.com",
                    Email = "admin@admin.com",
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User",
                    CreatedAt = DateTime.UtcNow
                };

                // Temporarily remove password validators to allow simple "admin" password
                // This is only for the initial admin user - regular users still need strong passwords
                var validators = userManager.PasswordValidators.ToList();
                userManager.PasswordValidators.Clear();

                var result = await userManager.CreateAsync(adminUser, "admin");
                
                // Restore password validators
                foreach (var validator in validators)
                {
                    userManager.PasswordValidators.Add(validator);
                }

                if (result.Succeeded)
                {
                    Console.WriteLine("✅ Admin user created successfully!");
                    Console.WriteLine("   Email: admin@admin.com");
                    Console.WriteLine("   Password: admin");
                }
                else
                {
                    Console.WriteLine("❌ Failed to create admin user:");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"   - {error.Description}");
                    }
                }
            }
            else
            {
                Console.WriteLine("ℹ️  Admin user already exists");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error initializing database: {ex.Message}");
            throw;
        }
    }
}
