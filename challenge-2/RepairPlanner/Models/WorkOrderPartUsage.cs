using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Tracks which parts are needed for a work order and quantities.
/// </summary>
public sealed class WorkOrderPartUsage
{
    [JsonPropertyName("partId")]
    [JsonProperty("partId")]
    public string PartId { get; set; } = string.Empty;

    [JsonPropertyName("partNumber")]
    [JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    [JsonProperty("quantity")]
    public int Quantity { get; set; } = 1;
}
