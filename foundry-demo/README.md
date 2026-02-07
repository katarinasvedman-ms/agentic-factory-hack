# Foundry Demo: Visual Workflow with ServiceNow Integration

This folder contains a demonstration of the Factory Maintenance Agentic Workflow defined in YAML format for visualization in the Azure AI Foundry portal, plus a Logic App integration with ServiceNow for enterprise ticket management.

## 📁 Contents

| File | Description |
|------|-------------|
| [factory-workflow.yaml](factory-workflow.yaml) | Declarative workflow definition for Foundry portal visualization |
| [servicenow-logic-app.json](servicenow-logic-app.json) | ARM template for ServiceNow Logic App connector |
| [sample-input.json](sample-input.json) | Sample telemetry input for testing |

## 🎯 Overview

This demo extends Challenge 4's agent workflow with:

1. **Foundry Workflow YAML** - A declarative workflow that can be imported into Azure AI Foundry for visual representation
2. **ServiceNow Logic App Tool** - Enterprise ITSM integration for automatic ticket creation
3. **Visual Agent Pipeline** - See the sequential agent flow in the Foundry UI

### Workflow Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Azure AI Foundry Workflow                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐    │
│  │   Anomaly   │──▶│    Fault    │──▶│   Repair    │──▶│ Maintenance │    │
│  │Classification│   │  Diagnosis  │   │  Planning   │   │ Scheduling  │    │
│  │   Agent     │   │    Agent    │   │   Agent     │   │   Agent     │    │
│  └─────────────┘   └─────────────┘   └─────────────┘   └─────────────┘    │
│         │                 │                 │                 │            │
│         ▼                 ▼                 ▼                 ▼            │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                        Foundry Agent Service                         │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────┐   ┌─────────────────────────────────────────────────┐    │
│  │   Parts     │──▶│              ServiceNow Logic App              │    │
│  │  Ordering   │   │         (Create Incident Ticket)               │    │
│  │   Agent     │   └─────────────────────────────────────────────────┘    │
│  └─────────────┘                         │                                 │
│                                          ▼                                 │
│                               ┌─────────────────┐                          │
│                               │   ServiceNow    │                          │
│                               │     ITSM        │                          │
│                               └─────────────────┘                          │
└─────────────────────────────────────────────────────────────────────────────┘
```

## 🚀 Deployment

### Prerequisites

- Azure subscription with Azure AI Foundry access
- ServiceNow instance (optional, for full integration)
- Completed Challenge 1 agents deployed to Foundry Agent Service

### Step 1: Deploy the ServiceNow Logic App

```bash
# Set your variables
RESOURCE_GROUP="your-resource-group"
SERVICENOW_INSTANCE="yourcompany.service-now.com"
SERVICENOW_USER="api_user"

# Deploy the Logic App
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file servicenow-logic-app.json \
  --parameters \
    servicenowInstance=$SERVICENOW_INSTANCE \
    servicenowUsername=$SERVICENOW_USER

# Get the Logic App callback URL
az logic workflow show \
  --resource-group $RESOURCE_GROUP \
  --name factory-servicenow-connector \
  --query "accessEndpoint" -o tsv
```

### Step 2: Configure Environment Variables

Add these to your `.env` file:

```bash
# Agent endpoints (from Challenge 4)
AZURE_AI_PROJECT_ENDPOINT=https://your-project.cognitiveservices.azure.com/
REPAIR_PLANNER_AGENT_URL=https://localhost:5231/repair-planner
MAINTENANCE_SCHEDULER_AGENT_URL=https://localhost:8000/maintenance-scheduler
PARTS_ORDERING_AGENT_URL=https://localhost:8000/parts-ordering

# ServiceNow Logic App
SERVICENOW_LOGIC_APP_RESOURCE_ID=/subscriptions/.../resourceGroups/.../providers/Microsoft.Logic/workflows/factory-servicenow-connector

# Notifications (optional)
ALERT_EMAIL=factory-ops@yourcompany.com
TEAMS_WEBHOOK_URL=https://outlook.office.com/webhook/...
```

### Step 3: Import Workflow to Foundry Portal

1. Navigate to [Azure AI Foundry](https://ai.azure.com)
2. Open your project
3. Go to **Workflows** (or **Build** > **Workflows**)
4. Click **+ Create workflow** > **Import from YAML**
5. Upload `factory-workflow.yaml`
6. Review the visual pipeline and click **Create**

## 🔧 Workflow Components

### Agents

| Agent | Type | Hosting | Description |
|-------|------|---------|-------------|
| AnomalyClassificationAgent | Foundry Agent | Agent Service | Classifies telemetry anomalies |
| FaultDiagnosisAgent | Foundry Agent | Agent Service | Diagnoses root causes |
| RepairPlannerAgent | Local Agent | Self-hosted | Creates work orders with Cosmos DB |
| MaintenanceSchedulerAgent | A2A Agent | Python service | Schedules maintenance windows |
| PartsOrderingAgent | A2A Agent | Python service | Orders required parts |

### Tools

| Tool | Type | Description |
|------|------|-------------|
| servicenow_create_incident | Logic App | Creates ServiceNow incident tickets |

## 📊 Sample Input

```json
{
  "machine_telemetry": {
    "machine_id": "M-001",
    "temperature": 92.5,
    "vibration": 4.8,
    "pressure": 145.2,
    "timestamp": "2026-02-03T10:30:00Z"
  }
}
```

## 🔍 Viewing in Foundry Portal

Once imported, the workflow will appear as a visual pipeline:

```
┌──────────────────────────────────────────────────────────────────┐
│                    Factory Maintenance Workflow                   │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│   [Input: machine_telemetry]                                      │
│              │                                                    │
│              ▼                                                    │
│   ┌──────────────────────┐                                       │
│   │ 1️⃣ Anomaly          │                                       │
│   │    Classification     │                                       │
│   └──────────────────────┘                                       │
│              │                                                    │
│              ▼                                                    │
│   ┌──────────────────────┐                                       │
│   │ 2️⃣ Fault            │   (conditional: if anomaly detected)  │
│   │    Diagnosis          │                                       │
│   └──────────────────────┘                                       │
│              │                                                    │
│              ▼                                                    │
│   ┌──────────────────────┐                                       │
│   │ 3️⃣ Repair           │                                       │
│   │    Planning           │                                       │
│   └──────────────────────┘                                       │
│              │                                                    │
│              ▼                                                    │
│   ┌──────────────────────┐                                       │
│   │ 4️⃣ Maintenance      │                                       │
│   │    Scheduling         │                                       │
│   └──────────────────────┘                                       │
│              │                                                    │
│              ▼                                                    │
│   ┌──────────────────────┐                                       │
│   │ 5️⃣ Parts            │                                       │
│   │    Ordering           │                                       │
│   └──────────────────────┘                                       │
│              │                                                    │
│              ▼                                                    │
│   ┌──────────────────────┐                                       │
│   │ 6️⃣ ServiceNow       │   (Logic App Tool)                    │
│   │    Ticket Creation    │                                       │
│   └──────────────────────┘                                       │
│              │                                                    │
│              ▼                                                    │
│   [Output: analysis_result, work_order_id, servicenow_ticket_id] │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

## 🔗 Related Resources

- [Azure AI Foundry Workflows Documentation](https://learn.microsoft.com/azure/ai-studio/concepts/workflows)
- [Logic Apps ServiceNow Connector](https://learn.microsoft.com/connectors/service-now/)
- [Challenge 4 - Agent Workflow](../challenge-4/README.md)

## ⚠️ Notes

- The YAML schema used here is illustrative of Foundry workflow capabilities
- Actual schema may vary based on Foundry SDK version
- For production, secure all credentials using Azure Key Vault
- Test the Logic App separately before integrating with the workflow
