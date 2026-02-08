using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;
using RepairPlanner.Services;

namespace RepairPlanner;

/// <summary>
/// The main Repair Planner Agent that orchestrates the workflow:
/// 1. Receives a diagnosed fault
/// 2. Looks up required skills and parts
/// 3. Queries available technicians and inventory
/// 4. Invokes AI to generate a repair plan
/// 5. Saves the work order to Cosmos DB
/// </summary>
public sealed class RepairPlannerAgent(
    AIProjectClient projectClient,
    CosmosDbService cosmosDb,
    IFaultMappingService faultMapping,
    string modelDeploymentName,
    ILogger<RepairPlannerAgent> logger)
{
    private const string AgentName = "RepairPlannerAgent";

    // System prompt for the AI agent
    private const string AgentInstructions = """
        You are a Repair Planner Agent for tire manufacturing equipment.
        Generate a repair plan with tasks, timeline, and resource allocation.
        Return the response as valid JSON matching the WorkOrder schema.
        
        Output JSON with these fields:
        - workOrderNumber: string (format: "WO-YYYYMMDD-XXXX")
        - machineId: string (from the fault)
        - title: string (brief description)
        - description: string (detailed description)
        - type: "corrective" | "preventive" | "emergency"
        - priority: "critical" | "high" | "medium" | "low"
        - status: "pending"
        - assignedTo: string (technician id) or null
        - notes: string
        - estimatedDuration: integer (total minutes, e.g. 90)
        - partsUsed: [{ partId, partNumber, quantity }]
        - tasks: [{ sequence, title, description, estimatedDurationMinutes (integer), requiredSkills, safetyNotes }]
        
        IMPORTANT: All duration fields must be integers representing minutes (e.g. 90), not strings like "90 minutes".
        
        Rules:
        - Assign the most qualified available technician based on skill match
        - Include only relevant parts from the provided inventory; use empty array if none needed
        - Tasks must be ordered by sequence and be actionable
        - Set priority based on fault severity (critical/high for severe faults)
        - Include safety notes for hazardous tasks
        
        Return ONLY valid JSON, no markdown code blocks or extra text.
        """;

    // JSON options for parsing LLM responses (handles numbers as strings)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// JSON schema for structured output - ensures LLM returns valid WorkOrderResponse.
    /// Generated once and reused for type-safe responses.
    /// AIJsonUtilities.CreateJsonSchema generates a JSON schema from a .NET type.
    /// </summary>
    private static readonly JsonElement WorkOrderSchema = AIJsonUtilities.CreateJsonSchema(
        type: typeof(WorkOrderResponse),
        serializerOptions: JsonOptions);

    /// <summary>
    /// Response format that enforces the JSON schema on LLM output.
    /// This tells the AI model to strictly follow the schema.
    /// Parameters: (schema, schemaName, schemaDescription)
    /// </summary>
    private static readonly ChatResponseFormat StructuredOutputFormat = 
        ChatResponseFormat.ForJsonSchema(
            WorkOrderSchema,
            "WorkOrderResponse",
            "A structured work order response for tire manufacturing equipment repair");

    /// <summary>
    /// Registers or updates the agent definition in Azure AI Foundry.
    /// Call this once at startup.
    /// </summary>
    public async Task EnsureAgentVersionAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Registering agent {AgentName} with model {Model}", AgentName, modelDeploymentName);

        // Log the schema for debugging (shows what structure the AI must follow)
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("WorkOrder JSON Schema: {Schema}", WorkOrderSchema.ToString());
        }

        var definition = new PromptAgentDefinition(model: modelDeploymentName)
        {
            Instructions = AgentInstructions
        };

        await projectClient.Agents.CreateAgentVersionAsync(
            AgentName,
            new AgentVersionCreationOptions(definition),
            ct);

        logger.LogInformation("Agent {AgentName} registered successfully", AgentName);
    }

    /// <summary>
    /// Gets the JSON schema used for structured output.
    /// Useful for debugging or displaying the expected response format.
    /// </summary>
    public static JsonElement GetWorkOrderSchema() => WorkOrderSchema;

    /// <summary>
    /// Gets the response format configuration for structured output.
    /// Can be used when invoking the agent with explicit format requirements.
    /// </summary>
    public static ChatResponseFormat GetStructuredOutputFormat() => StructuredOutputFormat;

    /// <summary>
    /// Main workflow: creates a work order from a diagnosed fault.
    /// </summary>
    /// <param name="fault">The diagnosed fault from Challenge 1.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created work order.</returns>
    public async Task<WorkOrder> PlanAndCreateWorkOrderAsync(DiagnosedFault fault, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Planning repair for fault {FaultType} on machine {MachineId}",
            fault.FaultType, fault.MachineId);

        // Step 1: Get required skills and parts from mapping service
        var requiredSkills = faultMapping.GetRequiredSkills(fault.FaultType);
        var requiredParts = faultMapping.GetRequiredParts(fault.FaultType);

        logger.LogDebug(
            "Fault {FaultType} requires skills: [{Skills}], parts: [{Parts}]",
            fault.FaultType,
            string.Join(", ", requiredSkills),
            string.Join(", ", requiredParts));

        // Step 2: Query available technicians and parts inventory in parallel
        var techniciansTask = cosmosDb.GetAvailableTechniciansBySkillsAsync(requiredSkills, ct);
        var partsTask = cosmosDb.GetPartsByNumbersAsync(requiredParts, ct);

        await Task.WhenAll(techniciansTask, partsTask);

        var technicians = await techniciansTask;
        var partsInventory = await partsTask;

        logger.LogInformation(
            "Found {TechCount} available technicians, {PartsCount} parts in stock",
            technicians.Count, partsInventory.Count);

        // Step 2b: Handle case when no technicians are available
        var technicianWarning = "";
        if (technicians.Count == 0)
        {
            logger.LogWarning(
                "No available technicians found with required skills: [{Skills}]. Work order will be unassigned.",
                string.Join(", ", requiredSkills));
            
            technicianWarning = "WARNING: No technicians are currently available with the required skills. " +
                               "Leave assignedTo as null. Add a note about needing to find qualified personnel.";
        }

        // Step 2c: Check for missing parts
        var missingParts = requiredParts.Where(p => !partsInventory.ContainsKey(p)).ToList();
        var partsWarning = "";
        if (missingParts.Count > 0)
        {
            logger.LogWarning(
                "Missing parts in inventory: [{Parts}]",
                string.Join(", ", missingParts));
            
            partsWarning = $"WARNING: The following required parts are not in stock: {string.Join(", ", missingParts)}. " +
                          "Include a note about ordering these parts.";
        }

        // Step 3: Build the prompt for the AI agent
        var prompt = BuildPrompt(fault, technicians, partsInventory, requiredSkills, technicianWarning, partsWarning);

        // Step 4: Invoke the AI agent
        logger.LogDebug("Invoking AI agent with prompt length: {Length} chars", prompt.Length);

        var agent = projectClient.GetAIAgent(name: AgentName);
        var response = await agent.RunAsync(prompt, thread: null, options: null, ct);

        var responseText = response.Text ?? "";
        logger.LogDebug("Agent response length: {Length} chars", responseText.Length);

        // Step 5: Parse the response into a WorkOrder
        var workOrder = ParseWorkOrderResponse(responseText, fault);

        // Step 6: Apply defaults and validate
        ApplyDefaults(workOrder, fault, technicians);

        // Step 7: Save to Cosmos DB
        var savedWorkOrder = await cosmosDb.CreateWorkOrderAsync(workOrder, ct);

        logger.LogInformation(
            "Created work order {WorkOrderNumber} assigned to {AssignedTo}",
            savedWorkOrder.WorkOrderNumber,
            savedWorkOrder.AssignedTo ?? "(unassigned)");

        return savedWorkOrder;
    }

    /// <summary>
    /// Builds the user prompt with fault details, technicians, and parts.
    /// </summary>
    private static string BuildPrompt(
        DiagnosedFault fault,
        List<Technician> technicians,
        Dictionary<string, Part> partsInventory,
        IReadOnlyList<string> requiredSkills,
        string technicianWarning = "",
        string partsWarning = "")
    {
        // Serialize technicians summary (id, name, skills, availability)
        var techSummary = technicians.Select(t => new
        {
            t.Id,
            t.Name,
            t.Skills,
            t.Department,
            MatchingSkills = t.Skills.Count(s => requiredSkills.Contains(s, StringComparer.OrdinalIgnoreCase))
        });

        // Serialize parts summary
        var partsSummary = partsInventory.Values.Select(p => new
        {
            p.Id,
            p.PartNumber,
            p.Name,
            p.QuantityInStock,
            p.Location
        });

        // Build warnings section if any issues
        var warningsSection = "";
        if (!string.IsNullOrEmpty(technicianWarning) || !string.IsNullOrEmpty(partsWarning))
        {
            warningsSection = $"""

            ## ⚠️ Warnings
            {technicianWarning}
            {partsWarning}
            """;
        }

        return $"""
            Create a repair work order for the following diagnosed fault:

            ## Fault Details
            - Fault ID: {fault.Id}
            - Machine ID: {fault.MachineId}
            - Machine Name: {fault.MachineName}
            - Fault Type: {fault.FaultType}
            - Severity: {fault.Severity}
            - Description: {fault.Description}
            - Root Cause: {fault.RootCause}
            - Recommended Actions: {string.Join("; ", fault.RecommendedActions)}
            - Diagnosed At: {fault.DiagnosedAt:yyyy-MM-dd HH:mm:ss} UTC

            ## Available Technicians
            {(technicians.Count > 0 ? JsonSerializer.Serialize(techSummary, JsonOptions) : "(No technicians available)")}

            ## Parts Inventory
            {(partsInventory.Count > 0 ? JsonSerializer.Serialize(partsSummary, JsonOptions) : "(No parts in inventory)")}

            ## Required Skills for this Fault Type
            {string.Join(", ", requiredSkills)}
            {warningsSection}
            Generate a complete work order JSON response.
            """;
    }

    /// <summary>
    /// Parses the AI response into a WorkOrder object using structured output.
    /// With JSON schema enforcement, the response should be well-formed.
    /// Still handles edge cases like markdown code blocks for robustness.
    /// </summary>
    private WorkOrder ParseWorkOrderResponse(string responseText, DiagnosedFault fault)
    {
        // Strip markdown code blocks if present (shouldn't happen with structured output, but be safe)
        var json = responseText.Trim();
        if (json.StartsWith("```"))
        {
            // Remove ```json and closing ```
            var firstNewline = json.IndexOf('\n');
            if (firstNewline > 0)
            {
                json = json[(firstNewline + 1)..];
            }
            if (json.EndsWith("```"))
            {
                json = json[..^3];
            }
            json = json.Trim();
        }

        try
        {
            // First try to parse as the structured WorkOrderResponse type
            // This gives us type-safe deserialization with the schema we defined
            var response = JsonSerializer.Deserialize<WorkOrderResponse>(json, JsonOptions);
            
            if (response != null)
            {
                logger.LogDebug("Successfully parsed structured WorkOrderResponse");
                return response.ToWorkOrder();
            }

            // Fallback: try parsing directly as WorkOrder (for backward compatibility)
            logger.LogDebug("Trying fallback WorkOrder parsing");
            var workOrder = JsonSerializer.Deserialize<WorkOrder>(json, JsonOptions);
            
            if (workOrder == null)
            {
                logger.LogWarning("Deserialized work order was null, creating default");
                return CreateDefaultWorkOrder(fault);
            }

            return workOrder;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse work order JSON. Response: {Response}", 
                responseText.Length > 500 ? responseText[..500] + "..." : responseText);
            
            // Return a default work order rather than failing completely
            return CreateDefaultWorkOrder(fault);
        }
    }

    /// <summary>
    /// Creates a minimal default work order when parsing fails.
    /// </summary>
    private static WorkOrder CreateDefaultWorkOrder(DiagnosedFault fault)
    {
        return new WorkOrder
        {
            Id = Guid.NewGuid().ToString(),
            WorkOrderNumber = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
            MachineId = fault.MachineId,
            Title = $"Repair: {fault.FaultType}",
            Description = fault.Description,
            Type = "corrective",
            Priority = fault.Severity?.ToLowerInvariant() switch
            {
                "critical" => "critical",
                "high" => "high",
                "medium" => "medium",
                _ => "low"
            },
            Status = "pending",
            FaultId = fault.Id,
            Tasks = [],
            PartsUsed = []
        };
    }

    /// <summary>
    /// Applies default values and links to the original fault.
    /// </summary>
    private static void ApplyDefaults(
        WorkOrder workOrder, 
        DiagnosedFault fault,
        List<Technician> technicians)
    {
        // Ensure required fields are set
        // ??= means "assign if null" (like Python's: x = x or default_value)
        workOrder.Id ??= Guid.NewGuid().ToString();
        workOrder.FaultId = fault.Id;
        workOrder.MachineId = fault.MachineId;
        workOrder.Status ??= "pending";
        workOrder.Type ??= "corrective";
        workOrder.Tasks ??= [];
        workOrder.PartsUsed ??= [];

        // Calculate priority based on fault severity (overrides AI suggestion)
        workOrder.Priority = CalculatePriority(fault.Severity);

        // Generate work order number if missing
        if (string.IsNullOrWhiteSpace(workOrder.WorkOrderNumber))
        {
            workOrder.WorkOrderNumber = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
        }

        // Validate assigned technician exists and handle no-technician case
        if (technicians.Count == 0)
        {
            // No technicians available - ensure unassigned and add note
            workOrder.AssignedTo = null;
            workOrder.Status = "pending_assignment";
            
            // Append to existing notes (don't overwrite AI notes)
            var noTechNote = "⚠️ ATTENTION: No technicians with required skills are currently available. " +
                            "Manual assignment required once personnel become available.";
            workOrder.Notes = string.IsNullOrWhiteSpace(workOrder.Notes) 
                ? noTechNote 
                : $"{workOrder.Notes}\n\n{noTechNote}";
        }
        else if (!string.IsNullOrEmpty(workOrder.AssignedTo))
        {
            var techExists = technicians.Any(t => 
                t.Id.Equals(workOrder.AssignedTo, StringComparison.OrdinalIgnoreCase));
            
            if (!techExists)
            {
                // AI assigned someone not in our list, clear it
                workOrder.AssignedTo = null;
                workOrder.Notes = string.IsNullOrWhiteSpace(workOrder.Notes)
                    ? "Note: Originally assigned technician was not available; reassignment needed."
                    : $"{workOrder.Notes}\n\nNote: Originally assigned technician was not available; reassignment needed.";
            }
        }

        // Ensure timestamps
        workOrder.CreatedAt = DateTime.UtcNow;
        workOrder.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates work order priority based on fault severity.
    /// Maps severity levels to priority values deterministically.
    /// </summary>
    /// <param name="severity">The fault severity (critical, high, medium, low, warning, etc.)</param>
    /// <returns>Priority string: critical, high, medium, or low</returns>
    private static string CalculatePriority(string? severity)
    {
        // Normalize severity to lowercase for comparison
        var normalizedSeverity = severity?.ToLowerInvariant()?.Trim() ?? "";

        return normalizedSeverity switch
        {
            // Direct mappings
            "critical" => "critical",
            "high" => "high",
            "medium" => "medium",
            "low" => "low",
            
            // Alternative severity names → priority
            "severe" or "emergency" => "critical",
            "warning" or "moderate" => "medium",
            "minor" or "informational" or "info" => "low",
            
            // Unknown severity defaults to medium
            _ => "medium"
        };
    }
}
