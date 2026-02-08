using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// The main output of the Repair Planner Agent.
/// Stored in Cosmos DB "WorkOrders" container with partition key "status".
/// </summary>
public sealed class WorkOrder
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("workOrderNumber")]
    [JsonProperty("workOrderNumber")]
    public string WorkOrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("machineId")]
    [JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Type of work order: "corrective", "preventive", or "emergency"
    /// </summary>
    [JsonPropertyName("type")]
    [JsonProperty("type")]
    public string Type { get; set; } = "corrective";

    /// <summary>
    /// Priority level: "critical", "high", "medium", or "low"
    /// </summary>
    [JsonPropertyName("priority")]
    [JsonProperty("priority")]
    public string Priority { get; set; } = "medium";

    /// <summary>
    /// Current status: "pending", "assigned", "in_progress", "completed", "cancelled"
    /// This is also the partition key in Cosmos DB.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Technician ID assigned to this work order (null if unassigned).
    /// </summary>
    [JsonPropertyName("assignedTo")]
    [JsonProperty("assignedTo")]
    public string? AssignedTo { get; set; }

    [JsonPropertyName("notes")]
    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Estimated total duration in minutes (integer, not string).
    /// </summary>
    [JsonPropertyName("estimatedDuration")]
    [JsonProperty("estimatedDuration")]
    public int EstimatedDuration { get; set; }

    [JsonPropertyName("partsUsed")]
    [JsonProperty("partsUsed")]
    public List<WorkOrderPartUsage> PartsUsed { get; set; } = [];

    [JsonPropertyName("tasks")]
    [JsonProperty("tasks")]
    public List<RepairTask> Tasks { get; set; } = [];

    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Reference to the original diagnosed fault ID.
    /// </summary>
    [JsonPropertyName("faultId")]
    [JsonProperty("faultId")]
    public string FaultId { get; set; } = string.Empty;
}
