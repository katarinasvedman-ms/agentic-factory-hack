using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepairPlanner;
using RepairPlanner.Models;
using RepairPlanner.Services;

// ============================================================================
// Repair Planner Agent - Entry Point
// ============================================================================
// This application demonstrates the Repair Planner Agent workflow:
// 1. Initialize services (Cosmos DB, Fault Mapping, AI Project Client)
// 2. Register the agent with Azure AI Foundry
// 3. Process a sample diagnosed fault
// 4. Generate and save a work order
// ============================================================================

Console.WriteLine("=== Repair Planner Agent ===\n");

// ----------------------------------------------------------------------------
// Step 1: Load configuration from environment variables
// ----------------------------------------------------------------------------
var aiEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT environment variable not set");

var modelDeployment = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("MODEL_DEPLOYMENT_NAME environment variable not set");

var cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")
    ?? throw new InvalidOperationException("COSMOS_ENDPOINT environment variable not set");

var cosmosKey = Environment.GetEnvironmentVariable("COSMOS_KEY")
    ?? throw new InvalidOperationException("COSMOS_KEY environment variable not set");

var cosmosDatabase = Environment.GetEnvironmentVariable("COSMOS_DATABASE_NAME")
    ?? throw new InvalidOperationException("COSMOS_DATABASE_NAME environment variable not set");

Console.WriteLine($"AI Endpoint: {aiEndpoint}");
Console.WriteLine($"Model: {modelDeployment}");
Console.WriteLine($"Cosmos DB: {cosmosDatabase}");
Console.WriteLine();

// ----------------------------------------------------------------------------
// Step 2: Set up dependency injection
// ----------------------------------------------------------------------------
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Add Cosmos DB options
services.AddSingleton(new CosmosDbOptions
{
    Endpoint = cosmosEndpoint,
    Key = cosmosKey,
    DatabaseName = cosmosDatabase
});

// Add services
services.AddSingleton<IFaultMappingService, FaultMappingService>();
services.AddSingleton<CosmosDbService>();

// Add AI Project Client (uses DefaultAzureCredential for authentication)
services.AddSingleton(_ => new AIProjectClient(
    new Uri(aiEndpoint),
    new DefaultAzureCredential()));

// Add the Repair Planner Agent
services.AddSingleton(sp => new RepairPlannerAgent(
    sp.GetRequiredService<AIProjectClient>(),
    sp.GetRequiredService<CosmosDbService>(),
    sp.GetRequiredService<IFaultMappingService>(),
    modelDeployment,
    sp.GetRequiredService<ILogger<RepairPlannerAgent>>()));

// Build the service provider
// "await using" is like Python's "async with" - ensures proper cleanup
await using var provider = services.BuildServiceProvider();

// ----------------------------------------------------------------------------
// Step 3: Get services and initialize the agent
// ----------------------------------------------------------------------------
var logger = provider.GetRequiredService<ILogger<Program>>();
var agent = provider.GetRequiredService<RepairPlannerAgent>();

logger.LogInformation("Registering agent with Azure AI Foundry...");
await agent.EnsureAgentVersionAsync();
logger.LogInformation("Agent registered successfully");

// ----------------------------------------------------------------------------
// Step 4: Create a sample diagnosed fault (simulating input from Challenge 1)
// ----------------------------------------------------------------------------
// To test the "no technicians available" scenario, change FaultType to "unknown_fault_xyz"
// To test normal flow, use a known fault like "curing_temperature_excessive"
var faultType = Environment.GetEnvironmentVariable("TEST_FAULT_TYPE") ?? "curing_temperature_excessive";

var sampleFault = new DiagnosedFault
{
    Id = Guid.NewGuid().ToString(),
    MachineId = "TCP-001",
    MachineName = "Tire Curing Press #1",
    FaultType = faultType,
    Severity = "high",
    Description = faultType == "curing_temperature_excessive" 
        ? "Temperature sensors detecting readings 15°C above normal operating range in Zone 2"
        : $"Unknown fault detected: {faultType}",
    RootCause = "Suspected heater element malfunction or thermocouple drift",
    RecommendedActions = [
        "Inspect heater elements in Zone 2",
        "Calibrate temperature sensors",
        "Check PLC temperature control logic",
        "Verify cooling system operation"
    ],
    DiagnosedAt = DateTime.UtcNow
};

Console.WriteLine("\n--- Sample Diagnosed Fault ---");
Console.WriteLine($"Machine: {sampleFault.MachineName} ({sampleFault.MachineId})");
Console.WriteLine($"Fault: {sampleFault.FaultType}");
Console.WriteLine($"Severity: {sampleFault.Severity}");
Console.WriteLine($"Description: {sampleFault.Description}");
Console.WriteLine();

// ----------------------------------------------------------------------------
// Step 5: Generate the repair plan and work order
// ----------------------------------------------------------------------------
logger.LogInformation("Generating repair plan...");

try
{
    var workOrder = await agent.PlanAndCreateWorkOrderAsync(sampleFault);

    Console.WriteLine("\n=== Work Order Created ===");
    Console.WriteLine($"Work Order #: {workOrder.WorkOrderNumber}");
    Console.WriteLine($"Title: {workOrder.Title}");
    Console.WriteLine($"Priority: {workOrder.Priority}");
    Console.WriteLine($"Type: {workOrder.Type}");
    Console.WriteLine($"Assigned To: {workOrder.AssignedTo ?? "(unassigned)"}");
    Console.WriteLine($"Estimated Duration: {workOrder.EstimatedDuration} minutes");
    Console.WriteLine($"Status: {workOrder.Status}");

    if (workOrder.Tasks.Count > 0)
    {
        Console.WriteLine($"\nTasks ({workOrder.Tasks.Count}):");
        foreach (var task in workOrder.Tasks.OrderBy(t => t.Sequence))
        {
            Console.WriteLine($"  {task.Sequence}. {task.Title} ({task.EstimatedDurationMinutes} min)");
        }
    }

    if (workOrder.PartsUsed.Count > 0)
    {
        Console.WriteLine($"\nParts Required ({workOrder.PartsUsed.Count}):");
        foreach (var part in workOrder.PartsUsed)
        {
            Console.WriteLine($"  - {part.PartNumber} x{part.Quantity}");
        }
    }

    // Output full JSON for debugging
    Console.WriteLine("\n--- Full Work Order JSON ---");
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    Console.WriteLine(JsonSerializer.Serialize(workOrder, jsonOptions));

    logger.LogInformation("Repair planning completed successfully!");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to generate repair plan");
    Console.WriteLine($"\nError: {ex.Message}");
    Environment.Exit(1);
}

Console.WriteLine("\n=== Done ===");
