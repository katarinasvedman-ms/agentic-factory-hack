using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a technician who can perform repairs.
/// Stored in Cosmos DB "Technicians" container with partition key "department".
/// </summary>
public sealed class Technician
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    [JsonPropertyName("skills")]
    [JsonProperty("skills")]
    public List<string> Skills { get; set; } = [];

    [JsonPropertyName("certifications")]
    [JsonProperty("certifications")]
    public List<string> Certifications { get; set; } = [];

    [JsonPropertyName("available")]
    [JsonProperty("available")]
    public bool Available { get; set; } = true;

    [JsonPropertyName("currentAssignment")]
    [JsonProperty("currentAssignment")]
    public string? CurrentAssignment { get; set; }

    [JsonPropertyName("shiftStart")]
    [JsonProperty("shiftStart")]
    public string ShiftStart { get; set; } = "08:00";

    [JsonPropertyName("shiftEnd")]
    [JsonProperty("shiftEnd")]
    public string ShiftEnd { get; set; } = "16:00";
}
