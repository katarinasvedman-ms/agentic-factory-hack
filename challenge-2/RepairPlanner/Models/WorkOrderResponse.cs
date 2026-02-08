using System.Text.Json.Serialization;

namespace RepairPlanner.Models;

/// <summary>
/// Structured output schema for the AI response.
/// This defines the exact shape of JSON the AI should return.
/// Using this with JSON schema ensures type-safe responses.
/// </summary>
public sealed class WorkOrderResponse
{
    [JsonPropertyName("workOrderNumber")]
    public string WorkOrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("machineId")]
    public string MachineId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "corrective";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "medium";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("assignedTo")]
    public string? AssignedTo { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("estimatedDuration")]
    public int EstimatedDuration { get; set; }

    [JsonPropertyName("partsUsed")]
    public List<WorkOrderPartUsageResponse> PartsUsed { get; set; } = [];

    [JsonPropertyName("tasks")]
    public List<RepairTaskResponse> Tasks { get; set; } = [];

    /// <summary>
    /// Converts this response to a full WorkOrder entity.
    /// </summary>
    public WorkOrder ToWorkOrder() => new()
    {
        Id = Guid.NewGuid().ToString(),
        WorkOrderNumber = WorkOrderNumber,
        MachineId = MachineId,
        Title = Title,
        Description = Description,
        Type = Type,
        Priority = Priority,
        Status = Status,
        AssignedTo = AssignedTo,
        Notes = Notes,
        EstimatedDuration = EstimatedDuration,
        PartsUsed = PartsUsed.Select(p => new WorkOrderPartUsage
        {
            PartId = p.PartId,
            PartNumber = p.PartNumber,
            Quantity = p.Quantity
        }).ToList(),
        Tasks = Tasks.Select(t => new RepairTask
        {
            Sequence = t.Sequence,
            Title = t.Title,
            Description = t.Description,
            EstimatedDurationMinutes = t.EstimatedDurationMinutes,
            RequiredSkills = t.RequiredSkills,
            SafetyNotes = t.SafetyNotes
        }).ToList()
    };
}

/// <summary>
/// Structured output for repair task in AI response.
/// </summary>
public sealed class RepairTaskResponse
{
    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("estimatedDurationMinutes")]
    public int EstimatedDurationMinutes { get; set; }

    /// <summary>
    /// Required skills for this task.
    /// Uses custom converter to handle LLM returning string instead of array.
    /// </summary>
    [JsonPropertyName("requiredSkills")]
    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string> RequiredSkills { get; set; } = [];

    [JsonPropertyName("safetyNotes")]
    public string SafetyNotes { get; set; } = string.Empty;
}

/// <summary>
/// Structured output for part usage in AI response.
/// </summary>
public sealed class WorkOrderPartUsageResponse
{
    [JsonPropertyName("partId")]
    public string PartId { get; set; } = string.Empty;

    [JsonPropertyName("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = 1;
}
