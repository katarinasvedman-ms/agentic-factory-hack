using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;

namespace RepairPlanner.Services;

/// <summary>
/// Service for interacting with Cosmos DB.
/// Handles technicians, parts inventory, and work orders.
/// </summary>
public sealed class CosmosDbService : IAsyncDisposable
{
    private readonly CosmosClient _client;
    private readonly Database _database;
    private readonly Container _techniciansContainer;
    private readonly Container _partsContainer;
    private readonly Container _workOrdersContainer;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(CosmosDbOptions options, ILogger<CosmosDbService> logger)
    {
        _logger = logger;

        // Create Cosmos client with recommended settings
        var clientOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };

        _client = new CosmosClient(options.Endpoint, options.Key, clientOptions);
        _database = _client.GetDatabase(options.DatabaseName);
        _techniciansContainer = _database.GetContainer(options.TechniciansContainer);
        _partsContainer = _database.GetContainer(options.PartsContainer);
        _workOrdersContainer = _database.GetContainer(options.WorkOrdersContainer);

        _logger.LogInformation("CosmosDbService initialized for database: {Database}", options.DatabaseName);
    }

    /// <summary>
    /// Finds technicians who have at least one of the required skills and are available.
    /// </summary>
    /// <param name="requiredSkills">List of skills needed for the repair.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching technicians.</returns>
    public async Task<List<Technician>> GetAvailableTechniciansBySkillsAsync(
        IReadOnlyList<string> requiredSkills,
        CancellationToken ct = default)
    {
        var technicians = new List<Technician>();

        try
        {
            // Query all available technicians (we'll filter by skills in memory
            // because Cosmos DB doesn't support ARRAY_CONTAINS with a parameter array easily)
            // Note: seed data uses 'available' not 'isAvailable'
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.available = true");

            _logger.LogDebug("Querying available technicians");

            using var iterator = _techniciansContainer.GetItemQueryIterator<Technician>(
                query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 100 });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(ct);
                
                foreach (var tech in response)
                {
                    // Check if technician has at least one required skill
                    var hasMatchingSkill = tech.Skills.Any(skill =>
                        requiredSkills.Contains(skill, StringComparer.OrdinalIgnoreCase));

                    if (hasMatchingSkill)
                    {
                        technicians.Add(tech);
                    }
                }
            }

            _logger.LogInformation(
                "Found {Count} available technicians with required skills",
                technicians.Count);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex,
                "Cosmos DB error querying technicians. Status: {Status}",
                ex.StatusCode);
            throw;
        }

        return technicians;
    }

    /// <summary>
    /// Fetches parts from inventory by their part numbers.
    /// </summary>
    /// <param name="partNumbers">List of part numbers to fetch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of part number to Part object.</returns>
    public async Task<Dictionary<string, Part>> GetPartsByNumbersAsync(
        IReadOnlyList<string> partNumbers,
        CancellationToken ct = default)
    {
        var parts = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);

        if (partNumbers.Count == 0)
        {
            _logger.LogDebug("No part numbers requested, returning empty dictionary");
            return parts;
        }

        try
        {
            // Build query with IN clause for part numbers
            // Using parameterized query for safety
            var partNumberList = string.Join(", ",
                partNumbers.Select((_, i) => $"@p{i}"));
            
            var queryText = $"SELECT * FROM c WHERE c.partNumber IN ({partNumberList})";
            var queryDef = new QueryDefinition(queryText);

            for (int i = 0; i < partNumbers.Count; i++)
            {
                queryDef = queryDef.WithParameter($"@p{i}", partNumbers[i]);
            }

            _logger.LogDebug("Querying parts: {PartNumbers}", string.Join(", ", partNumbers));

            using var iterator = _partsContainer.GetItemQueryIterator<Part>(
                queryDef,
                requestOptions: new QueryRequestOptions { MaxItemCount = 50 });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(ct);
                foreach (var part in response)
                {
                    parts[part.PartNumber] = part;
                }
            }

            _logger.LogInformation(
                "Found {Found}/{Requested} parts in inventory",
                parts.Count, partNumbers.Count);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex,
                "Cosmos DB error querying parts. Status: {Status}",
                ex.StatusCode);
            throw;
        }

        return parts;
    }

    /// <summary>
    /// Creates a new work order in Cosmos DB.
    /// </summary>
    /// <param name="workOrder">The work order to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created work order with any server-side updates.</returns>
    public async Task<WorkOrder> CreateWorkOrderAsync(
        WorkOrder workOrder,
        CancellationToken ct = default)
    {
        try
        {
            // Ensure timestamps are set
            workOrder.CreatedAt = DateTime.UtcNow;
            workOrder.UpdatedAt = DateTime.UtcNow;

            // Partition key is "status"
            var response = await _workOrdersContainer.CreateItemAsync(
                workOrder,
                new PartitionKey(workOrder.Status),
                cancellationToken: ct);

            _logger.LogInformation(
                "Created work order {WorkOrderNumber} with ID {Id}. RU charge: {RU}",
                workOrder.WorkOrderNumber,
                workOrder.Id,
                response.RequestCharge);

            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex,
                "Failed to create work order {WorkOrderNumber}. Status: {Status}",
                workOrder.WorkOrderNumber,
                ex.StatusCode);
            throw;
        }
    }

    /// <summary>
    /// Gets a single technician by ID.
    /// </summary>
    public async Task<Technician?> GetTechnicianByIdAsync(
        string technicianId,
        string department,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _techniciansContainer.ReadItemAsync<Technician>(
                technicianId,
                new PartitionKey(department),
                cancellationToken: ct);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Technician {Id} not found", technicianId);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error fetching technician {Id}", technicianId);
            throw;
        }
    }

    // await using support (like Python's "async with")
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        _logger.LogDebug("CosmosDbService disposed");
        await ValueTask.CompletedTask;
    }
}
