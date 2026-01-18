/*
 * TestDiagramGeneration.cs - Quick test to generate database diagram
 * 
 * Temporary test script to manually generate diagram for testing
 */

using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Scripts;

namespace BeDemo.Api.Scripts;

public static class TestDiagramGeneration
{
    public static async Task TestAsync()
    {
        Console.WriteLine("📊 Testing diagram generation...");

        var connectionString = "Host=localhost;Port=5432;Database=bedemo;Username=bedemo_user;Password=bedemo_password";
        
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using var context = new ApplicationDbContext(options);
        
        try
        {
            await DatabaseDiagramGenerator.GenerateDiagramAsync(context, connectionString);
            Console.WriteLine("✅ Test completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
