# Azure AI Foundry Demo: Factory Maintenance Workflow

> **🎯 Show 3 things:**
> 
> **1. Foundry Custom Tools** (MCP + Logic App → Cosmos DB)
> 
> **2. Workflows** (Declarative YAML orchestration)
> 
> **3. Hosted Agents** (Custom Python code in containers)

---

A complete multi-agent workflow demonstrating Azure AI Foundry's agent orchestration, MCP tool integration, and hosted agent capabilities.

## What This Demo Shows

- **5 AI Agents** working in sequence to handle factory anomalies
- **MCP Tools** connecting agents to Cosmos DB via Logic App
- **Hosted Agents** running custom Python code in Azure Container Apps
- **Declarative YAML Workflows** defining agent orchestration

## 🏗️ Architecture

```
User Input (Telemetry)
        │
        ▼
┌─────────────────────────────────────────────────────────────────┐
│                    factory-workflow-hosted                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌───────────────┐  │
│  │   Anomaly      │───▶│     Fault      │───▶│    Repair     │  │
│  │Classification  │    │   Diagnosis    │    │   Planner     │  │
│  │  (Prompt)      │    │   (Prompt)     │    │  (Prompt)     │  │
│  │                │    │                │    │               │  │
│  │ 🔧 Thresholds  │    │ 🔧 KnowledgeBase│   │ 🔧 Technicians│  │
│  │ 🔧 Machines    │    │ 🔧 Machines    │    │ 🔧 Parts      │  │
│  └───────┬────────┘    └───────┬────────┘    └───────┬───────┘  │
│          │                     │                     │          │
│          ▼                     ▼                     ▼          │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                     MCP Tool: CosmosDbMCP                │   │
│  │                    (Logic App connector)                 │   │
│  └──────────────────────────────────────────────────────────┘   │
│          ▲                     ▲                     ▲          │
│          │                     │                     │          │
│  ┌───────┴────────┐    ┌───────┴────────────────────┴───────┐   │
│  │ Parts Order    │    │   Maintenance Scheduler            │   │
│  │  (Prompt)      │    │  (Hosted Agent - Python)           │   │
│  │                │    │                                    │   │
│  │ 🔧 Parts       │    │ 🔧 MaintenanceWindows              │   │
│  │ 🔧 Suppliers   │    │ 🔧 WorkOrders                      │   │
│  └────────────────┘    └────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
                      ┌─────────────────────┐
                      │     Cosmos DB       │
                      │   (gold-demo-cosmos)│
                      │                     │
                      │ • Machines          │
                      │ • Technicians       │
                      │ • PartsInventory    │
                      │ • Thresholds        │
                      │ • KnowledgeBase     │
                      │ • WorkOrders        │
                      │ • MaintenanceWindows│
                      │ • Suppliers         │
                      └─────────────────────┘
```

## 📁 Project Structure

```
foundry-demo/
├── README.md                    # This file
├── GOLD-DEMO-RUNBOOK.md        # Detailed runbook with commands
├── SESSION-GROUNDING.md        # Session continuity notes
├── sample-input.json           # Test telemetry data
│
└── gold-demo-agents/            # ✅ All demo assets
    ├── factory-workflow-hosted.yaml   # Main workflow (4 prompt + 1 hosted)
    ├── gold-workflow.yaml             # Backup workflow (5 prompt agents)
    ├── seed-cosmos-gold.sh            # Seed core Cosmos data
    ├── seed-cosmos-challenge3.sh      # Seed additional containers
    │
    ├── anomaly-classification-agent.yaml
    ├── fault-diagnosis-agent.yaml
    ├── repair-planner-agent.yaml
    ├── parts-order-agent.yaml
    ├── maintenance-scheduler-agent.yaml
    │
    └── hosted-agents/            # Hosted agent code
        └── maintenance/
            ├── main.py           # Agent implementation
            ├── Dockerfile
            ├── requirements.txt
            └── agent.yaml
```

## 🚀 Quick Start (Demo)

### Prerequisites
- Azure CLI logged in
- Access to Azure AI Foundry project `ai-project-echo-agent-france`

### Test the Workflow

**In Foundry Portal:**
1. Open [Azure AI Foundry](https://ai.azure.com)
2. Navigate to project `ai-project-echo-agent-france`
3. Go to **Agent Applications** → `factory-workflow-hosted`
4. Click **Open app** to launch playground
5. Enter test prompt:

```
machine TBM-001: [{"metric": "vibration", "value": 5.2}, {"metric": "temperature", "value": 78}]
```

**Expected Output:**
- Anomaly classified as building_drum_vibration
- Fault diagnosed with root cause
- Work order created
- Parts ordered with supplier info
- Maintenance scheduled with specific window

### Test via API

```bash
# Get token
TOKEN=$(az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv)

# Call workflow
curl -X POST "https://ai-account-gihq46bsniq44.services.ai.azure.com/api/projects/ai-project-echo-agent-france/applications/factory-workflow-hosted?api-version=2025-05-01-preview" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"input": "machine TBM-001: [{\"metric\": \"vibration\", \"value\": 5.2}, {\"metric\": \"temperature\", \"value\": 78}]"}'
```

## 🔧 Key Components

### Azure Resources

| Resource | Purpose |
|----------|---------|
| `ai-project-echo-agent-france` | AI Foundry project |
| `gold-demo-cosmos` | Cosmos DB with factory data |
| `logicapp-957898-cosmos` | Logic App exposing MCP tools |
| `crgihq46bsniq44.azurecr.io` | Container registry for hosted agents |

### Agents

| Agent | Type | MCP Tools |
|-------|------|-----------|
| AnomalyClassification | Prompt | Thresholds, Machines |
| FaultDiagnosis | Prompt | KnowledgeBase, Machines |
| RepairPlanner | Prompt | Technicians, PartsInventory |
| PartsOrder | Prompt | PartsInventory, Suppliers |
| maintenance-scheduler-hosted | Hosted | MaintenanceWindows, WorkOrders |

### MCP Tool Connection

All agents connect to Cosmos DB through `CosmosDbMCP`:
- **Type**: Logic App MCP Server
- **Auth**: Project Managed Identity
- **Containers**: Machines, Technicians, PartsInventory, Thresholds, KnowledgeBase, WorkOrders, MaintenanceWindows, Suppliers

## 🔄 Deploying Hosted Agent Updates

```bash
cd /workspaces/agentic-factory-hack/foundry-demo/gold-demo-agents/hosted-agents

# Deploy changes (NEVER use azd up/down!)
azd deploy --no-prompt
```

## 📊 Cosmos DB Data

Seeded containers:

| Container | Data |
|-----------|------|
| Machines | TBM-001, TCP-001, TUM-001 |
| Technicians | Anna, Erik, Lars |
| PartsInventory | Bearings, heaters, sensors |
| Thresholds | Machine type limits |
| KnowledgeBase | Fault diagnosis procedures |
| WorkOrders | Sample pending orders |
| MaintenanceWindows | Available scheduling slots |
| Suppliers | Nordic, Euro Heating, Sensor Tech |

## 📚 Documentation

- [GOLD-DEMO-RUNBOOK.md](GOLD-DEMO-RUNBOOK.md) - Full technical runbook
- [SESSION-GROUNDING.md](SESSION-GROUNDING.md) - Session notes and context

## ⚠️ Important Notes

- **Do NOT run `azd up` or `azd down`** - only use `azd deploy`
- Hosted agents must be **last in workflow** (they terminate the chain)
- MCP tools require Project Managed Identity permissions on Logic App
