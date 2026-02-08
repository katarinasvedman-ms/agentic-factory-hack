namespace RepairPlanner.Services;

/// <summary>
/// Configuration options for Cosmos DB connection.
/// Populated from environment variables.
/// </summary>
public sealed class CosmosDbOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;

    // Container names
    public string TechniciansContainer { get; set; } = "Technicians";
    public string PartsContainer { get; set; } = "PartsInventory";
    public string WorkOrdersContainer { get; set; } = "WorkOrders";
}
