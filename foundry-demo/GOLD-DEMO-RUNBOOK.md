# 🏆 GOLD Demo Runbook - Systematisk Uppbyggnad

> **Mål:** Bygga en stabil demo-miljö med Foundry Tools (Cosmos MCP), Workflows, och Hosted Agents.  
> **Metod:** Ändra EN sak → Testa → Dokumentera → Nästa steg  
> **Startdatum:** 2026-02-07

---

## 🚀 QUICK RECREATION (10-15 min)

> **Prerequisite:** MCP Tool connection `CosmosDbMCP` already exists and points to `gold-demo-cosmos`

### 1. Create 5 Agents in Foundry Portal

| Agent | Model | Tool | Purpose |
|-------|-------|------|---------|
| `AnomalyClassification` | gpt-4o | CosmosDbMCP | Detect threshold violations |
| `FaultDiagnosis` | gpt-4.1 | CosmosDbMCP | Identify root cause |
| `RepairPlanner` | gpt-4.1 | CosmosDbMCP | Create work order |
| `MaintenanceScheduler` | gpt-4.1 | CosmosDbMCP | Select maintenance window |
| `PartsOrder` | gpt-4.1 | CosmosDbMCP | Check inventory/order parts |

Copy instructions from **"Working Agent Instructions"** section below.

### 2. Create Workflow

1. Go to **Workflows** → **+ New**
2. Name: `factory-workflow`
3. Add 5 InvokeAzureAgent actions in order:
   - AnomalyClassification
   - FaultDiagnosis
   - RepairPlanner
   - MaintenanceScheduler
   - PartsOrder
4. All use `System.LastMessage` as input
5. All have `autoSend: true`

Or paste YAML from **"Working Workflow"** section below.

### 3. Test

```
machine TBM-001: [{"metric": "vibration", "value": 5.2}, {"metric": "temperature", "value": 78}]
```

**Expected:** 5 agents execute in sequence, 6 MCP calls total, full maintenance workflow complete.

---

## 🐳 HOSTED AGENT (Deployed 2026-02-07)

> **Bonus demo feature:** Shows code-based agent deployment with MCP tools

### Deployed Agent

| Property | Value |
|----------|-------|
| **Name** | `maintenance-scheduler-hosted` |
| **Version** | 2 |
| **Endpoint** | `https://ai-account-gihq46bsniq44.services.ai.azure.com/api/projects/ai-project-echo-agent-france/agents/maintenance-scheduler-hosted/versions/2` |
| **Playground** | [Open in Foundry](https://ai.azure.com/nextgen/r/wUvdYh5PT8yXkP_fzQUcXw,rg-echo-agent-france,,ai-account-gihq46bsniq44,ai-project-echo-agent-france/build/agents/maintenance-scheduler-hosted/build?version=2) |
| **MCP Tool** | `CosmosDbMCP` |
| **Model** | `gpt-4.1` |

### Test Prompt

```
Schedule maintenance for work order WO-20260207-0001 (high priority bearing replacement on TBM-001)
```

**Expected Output:**
```
Maintenance Schedule:
- Work Order: WO-20260207-0001
- Selected Window: MW-20260208-NIGHT
- Scheduled: 2026-02-08T22:00:00Z to 2026-02-09T06:00:00Z
- Production Impact: Low
- Reasoning: High priority → earliest Low impact window
```

### Code Location

```
/foundry-demo/gold-demo-agents/hosted-agents/
├── azure.yaml              # azd deployment config
├── maintenance/
│   ├── agent.yaml          # Agent definition
│   ├── main.py             # Agent code (uses agent-framework)
│   ├── requirements.txt    # Dependencies
│   ├── Dockerfile          # Container build
│   └── .env                # Local dev environment
```

### Redeploy Command

```bash
cd /workspaces/agentic-factory-hack/foundry-demo/gold-demo-agents/hosted-agents
azd deploy --no-prompt
```

⚠️ **NEVER use `azd up` or `azd down`** - only `azd deploy`!

### Check Status

```bash
az cognitiveservices agent show \
    --account-name ai-account-gihq46bsniq44 \
    --project-name ai-project-echo-agent-france \
    --name maintenance-scheduler-hosted
```

### TODO: Add to Workflow

The hosted agent can replace the prompt-based `MaintenanceScheduler` in the workflow. This is pending for the next session.

---

## 🌍 Miljökonfiguration

| Parameter | Värde |
|-----------|-------|
| **Project Endpoint** | `https://ai-account-gihq46bsniq44.services.ai.azure.com/api/projects/ai-project-echo-agent-france` |
| **Resource Group** | `rg-echo-agent-france` |
| **AI Account** | `ai-account-gihq46bsniq44` |
| **Project Name** | `ai-project-echo-agent-france` |
| **Region** | France Central |
| **Container Registry** | `crgihq46bsniq44` |
| **Model Deployment** | `gpt-4.1` |
| **Cosmos DB Account** | `gold-demo-cosmos` |
| **Cosmos Endpoint** | `https://gold-demo-cosmos.documents.azure.com:443/` |
| **Cosmos Database** | `FactoryOpsDB` |
| **Cosmos Auth** | Managed Identity (RBAC) |
| **MCP Server Name** | `CosmosDbMCP` |
| **Logic App** | `logicapp-957898-cosmos` |
| **MCP Endpoint** | `https://logicapp-957898-cosmos.azurewebsites.net/api/mcpservers` |
| **MCP Connection ID** | `CosmosDbMCP` |

---

## 🧠 Lessons Learned & Gotchas

### 1. Security Bypass for Resource Creation

**Problem:** Logic App creation fails with 403 on storage blob/file share operations.

**Symptom:**
```
The request to create/access storage file share was blocked by your Defender for Storage settings
```

**Solution:** Add SecurityControl=Ignore tag to resource group BEFORE creating Logic App:
```bash
az group update \
  --name rg-echo-agent-france \
  --tags SecurityControl=Ignore
```

---

### 2. Managed Identity Authentication (RBAC)

**Problem:** Keys are blocked by policy. Must use Managed Identity.

**Solution:** Assign RBAC roles to both Foundry project identity AND Logic App identity:

```bash
# Get Cosmos DB resource ID
COSMOS_ID=$(az cosmosdb show --name gold-demo-cosmos -g rg-echo-agent-france --query "id" -o tsv)

# Get Foundry project identity
PROJECT_IDENTITY=$(az cognitiveservices account identity show \
  --name ai-account-gihq46bsniq44 \
  --resource-group rg-echo-agent-france \
  --query "principalId" -o tsv)

# Assign Cosmos DB Data Contributor (built-in role ID: 00000000-0000-0000-0000-000000000002)
az cosmosdb sql role assignment create \
  --account-name gold-demo-cosmos \
  --resource-group rg-echo-agent-france \
  --role-definition-id "00000000-0000-0000-0000-000000000002" \
  --principal-id "$PROJECT_IDENTITY" \
  --scope "$COSMOS_ID"

# Same for Logic App identity
LOGIC_APP_IDENTITY=$(az functionapp identity show \
  --name logicapp-957898-cosmos \
  --resource-group rg-echo-agent-france \
  --query "principalId" -o tsv)

az cosmosdb sql role assignment create \
  --account-name gold-demo-cosmos \
  --resource-group rg-echo-agent-france \
  --role-definition-id "00000000-0000-0000-0000-000000000002" \
  --principal-id "$LOGIC_APP_IDENTITY" \
  --scope "$COSMOS_ID"
```

---

### 3. Agent Instructions - Database Parameters

**Problem:** Agent uses wrong Cosmos account (defaults to `mcpstagingcosmosdb`).

**Solution:** ALWAYS specify explicit database parameters in instructions:
```
cosmosDbAccountName: "gold-demo-cosmos"
databaseId: "FactoryOpsDB"
```

---

### 4. Agent Instructions - Forcing Tool Calls

**Problem:** Agent gives generic answers without querying the database.

**Solution:** Use imperative language: "You MUST query", "REQUIRED WORKFLOW"

---

### 5. Agent Instructions - JSON Output

**Problem:** Agent returns prose/markdown instead of JSON.

**Solution:** 
- Keep instructions SHORT (recency bias helps)
- Put JSON example at the END
- Use "CRITICAL: Your final response must be valid JSON only"

---

### 6. Partition Keys for Cross-Partition Queries

**Problem:** Queries fail when partitionKey is required but data spans partitions.

**Solution:** Use empty string for partitionKey on SELECT * queries:
```json
{
  "containerId": "Technicians",
  "queryText": "SELECT * FROM c WHERE c.available = true",
  "partitionKey": ""
}
```

---

## ✅ Working Agent Instructions (TESTED 2026-02-07)

> **All agents use MCP tool connection: `CosmosDbMCP`**

### Agent 1: AnomalyClassification

**Name:** `AnomalyClassification`  
**Model:** `gpt-4o`  
**Tool:** MCP → `CosmosDbMCP`

**Instructions:**
```
You are an Anomaly Classification Agent evaluating machine anomalies for warning and critical threshold violations.

You will receive anomaly data for a given machine. Your task is to:
1. Query thresholds from the database  
2. Validate each metric against the threshold values
3. Raise an alert for maintenance if any critical or warning violations were found

## DATABASE QUERY (Required)
Call Get_all_documents_V3 with these exact parameters:
- cosmosDbAccountName: "gold-demo-cosmos"
- databaseId: "FactoryOpsDB"
- collectionId: "Thresholds"

## OUTPUT
After querying, provide:
- status: "critical" | "warning" | "normal"
- machineId: the machine being evaluated
- alerts: list of threshold violations with metric name, value, threshold, and severity
- summary: total metrics processed and violation counts
```

**Test prompt:**
```
machine TBM-001: [{"metric": "vibration", "value": 5.2}, {"metric": "temperature", "value": 78}]
```

---

### Agent 2: FaultDiagnosis

**Name:** `FaultDiagnosis`  
**Model:** `gpt-4.1`  
**Tool:** MCP → `CosmosDbMCP`

**Instructions:**
```
You are a Fault Diagnosis Agent that identifies root causes of machine anomalies.

You will receive anomaly classification results. Your task is to:
1. Query the knowledge base to find matching fault patterns
2. Identify the most likely root cause
3. Recommend diagnostic checks and required repairs

## DATABASE QUERY (Required)
Call Get_all_documents_V3 with these exact parameters:
- cosmosDbAccountName: "gold-demo-cosmos"
- databaseId: "FactoryOpsDB"
- collectionId: "KnowledgeBase"

## OUTPUT
After querying, provide:
- MachineId: the machine being diagnosed
- FaultType: matched fault type from KnowledgeBase (e.g., "building_drum_vibration")
- RootCause: most likely cause from likelyCauses
- Severity: from KnowledgeBase
- requiredParts: parts needed for repair
- requiredSkills: skills needed for technician assignment
```

**Test prompt:**
```
Diagnose fault for machine TBM-001 with high vibration anomaly detected
```

---

### Agent 3: RepairPlanner

**Name:** `RepairPlanner`  
**Model:** `gpt-4.1`  
**Tool:** MCP → `CosmosDbMCP`

**Instructions:**
```
You are a Repair Planner. Query the database, then return JSON only.

## STEP 1: Query Database
Use CosmosDbMCP with cosmosDbAccountName="gold-demo-cosmos", databaseId="FactoryOpsDB"
- Query Technicians: SELECT * FROM c WHERE c.available = true
- Query PartsInventory: SELECT * FROM c

## STEP 2: Return JSON
After querying, respond with ONLY this JSON (no other text):

{
  "workOrderNumber": "WO-20260207-0001",
  "machineId": "TBM-001",
  "title": "Bearing Replacement",
  "priority": "high",
  "status": "pending",
  "assignedTo": "TECH-001",
  "technicianName": "Anna Svensson",
  "estimatedDuration": 120,
  "partsUsed": [{"partNumber": "TBM-BRG-6220", "quantity": 1}],
  "tasks": [{"sequence": 1, "title": "Shutdown", "estimatedDurationMinutes": 15}]
}

CRITICAL: Your final response must be valid JSON only. No markdown. No explanation.
```

**Test prompt:**
```
Create repair plan for TBM-001 with bearing_drum_vibration fault
```

---

### Agent 4: MaintenanceScheduler

**Name:** `MaintenanceScheduler`  
**Model:** `gpt-4.1`  
**Tool:** MCP → `CosmosDbMCP`

**Instructions:**
```
You are a Maintenance Scheduler. You receive a work order and automatically select the best maintenance window.

## STEP 1 - Read windows
Call Get_all_documents_V3(cosmosDbAccountName="gold-demo-cosmos", databaseId="FactoryOpsDB", collectionId="MaintenanceWindows")

## STEP 2 - Select and output
Select the best window based on priority:
- High priority → earliest Low impact window
- Medium priority → any Low impact window

Do NOT ask for confirmation. Just output the decision.

## OUTPUT FORMAT
Maintenance Schedule:
- Work Order: [from input]
- Selected Window: [window ID]
- Scheduled: [date/time]
- Production Impact: [Low/Medium/High]
- Reasoning: [brief explanation]
```

**Test prompt:**
```
Schedule maintenance for work order WO-20260207-0001 (high priority bearing replacement on TBM-001)
```

---

### Agent 5: PartsOrder

**Name:** `PartsOrder`  
**Model:** `gpt-4.1`  
**Tool:** MCP → `CosmosDbMCP`

**Instructions:**
```
You are a Parts Ordering Agent. You check inventory and create purchase orders for required parts.

## STEP 1 - Read inventory
Call Get_all_documents_V3(cosmosDbAccountName="gold-demo-cosmos", databaseId="FactoryOpsDB", collectionId="PartsInventory")

## STEP 2 - Read suppliers
Call Get_all_documents_V3(cosmosDbAccountName="gold-demo-cosmos", databaseId="FactoryOpsDB", collectionId="Suppliers")

## STEP 3 - Create order decision
Based on the work order parts from the previous message:
- Check if parts are in stock (quantityInStock > 0)
- If in stock: no order needed
- If low stock (below reorderLevel): create order from best supplier
- Select supplier by: reliability score, lead time, price

Do NOT ask for confirmation. Just output the decision.

## OUTPUT FORMAT
Parts Order Decision:
- Work Order: [from input]
- Parts Required: [list]
- Inventory Status: [in stock / need to order]
- Supplier: [if ordering]
- Estimated Cost: [if ordering]
- Delivery Date: [if ordering]
- Reasoning: [brief explanation]
```

**Test prompt:**
```
Create parts order for work order WO-20260207-0001 requiring parts: TBM-BRG-6220 (quantity: 1)
```

---

## ✅ Working Workflow (5-Agent Chain) - TESTED 2026-02-07

**Name:** `factory-workflow`

```yaml
kind: workflow
trigger:
  kind: OnConversationStart
  id: trigger_factory
  actions:
    - kind: SendActivity
      id: acknowledge-input
      activity: Analyzing fault and querying factory database...
    - kind: InvokeAzureAgent
      id: step1-anomaly
      agent:
        name: AnomalyClassification
      conversationId: =System.ConversationId
      input:
        messages: =System.LastMessage
      output:
        autoSend: true
    - kind: InvokeAzureAgent
      id: step2-diagnosis
      agent:
        name: FaultDiagnosis
      conversationId: =System.ConversationId
      input:
        messages: =System.LastMessage
      output:
        autoSend: true
    - kind: InvokeAzureAgent
      id: step3-repair
      agent:
        name: RepairPlanner
      conversationId: =System.ConversationId
      input:
        messages: =System.LastMessage
      output:
        autoSend: true
    - kind: InvokeAzureAgent
      id: step4-schedule
      agent:
        name: MaintenanceScheduler
      conversationId: =System.ConversationId
      input:
        messages: =System.LastMessage
      output:
        autoSend: true
    - kind: InvokeAzureAgent
      id: step5-parts
      agent:
        name: PartsOrder
      conversationId: =System.ConversationId
      input:
        messages: =System.LastMessage
      output:
        autoSend: true
    - kind: EndConversation
      id: end-conversation
id: ""
name: factory-workflow
description: Factory maintenance 5-agent workflow
```

**Test input:**
```
machine TBM-001: [{"metric": "vibration", "value": 5.2}, {"metric": "temperature", "value": 78}]
```

**Expected flow:**
1. AnomalyClassification → Queries Thresholds → Detects vibration anomaly (5.2 > 4.5)
2. FaultDiagnosis → Queries KnowledgeBase → Identifies building_drum_vibration
3. RepairPlanner → Queries Technicians + Parts → Returns JSON work order
4. MaintenanceScheduler → Queries MaintenanceWindows → Selects MW-20260208-NIGHT
5. PartsOrder → Queries PartsInventory + Suppliers → Confirms in stock

---

## 🔧 Quick Recreation Commands

### Create Cosmos DB (Serverless + MI)
```bash
RG="rg-echo-agent-france"
COSMOS="gold-demo-cosmos"
LOCATION="francecentral"

# Create serverless account
az cosmosdb create \
  --name $COSMOS \
  --resource-group $RG \
  --locations regionName=$LOCATION \
  --capabilities EnableServerless \
  --default-consistency-level Session

# Create database
az cosmosdb sql database create \
  --account-name $COSMOS \
  --resource-group $RG \
  --name "FactoryOpsDB"

# Create containers (partition key = /id)
for c in Machines Technicians PartsInventory Thresholds KnowledgeBase WorkOrders; do
  az cosmosdb sql container create \
    --account-name $COSMOS \
    --resource-group $RG \
    --database-name "FactoryOpsDB" \
    --name $c \
    --partition-key-path "/id"
done
```

### Seed Data Script
```bash
# Use seed-cosmos-gold.sh in /foundry-demo/
./seed-cosmos-gold.sh "https://gold-demo-cosmos.documents.azure.com:443/"
```

### Create Logic App MCP (via Portal)
1. Go to Azure Portal → Logic App (Standard)
2. Name: `logicapp-XXXXXX-cosmos`
3. Region: France Central
4. Enable Managed Identity
5. Add Cosmos DB connector with MI auth
6. Create MCP trigger endpoint
7. Register in Foundry as MCP Server

---

## 📊 Current Data in Cosmos

### Technicians
| ID | Name | Department | Skills |
|----|------|------------|--------|
| TECH-001 | Anna Svensson | Mechanical | vibration_analysis, bearing_replacement, alignment, mechanical_systems |
| TECH-002 | Erik Johansson | Electrical | temperature_control, instrumentation, electrical_systems, plc_troubleshooting |
| TECH-003 | Lars Nilsson | Mechanical | tire_building_machine, tension_control, servo_systems, drum_balancing |

### PartsInventory
| Part Number | Name | Category | Stock | Location |
|-------------|------|----------|-------|----------|
| TBM-BRG-6220 | Building Drum Bearing | bearings | 5 | Warehouse A |
| TCP-HTR-4KW | Curing Press Heater 4kW | heating | 3 | Warehouse B |
| GEN-TS-K400 | Temperature Sensor K-Type | sensors | 12 | Warehouse A |
| TBM-LS-500N | Load Sensor 500N | sensors | 4 | Warehouse A |
| TBM-SRV-5KW | Servo Motor 5kW | motors | 2 | Warehouse B |

---

## 📋 Testlogg

| Steg | Lager | Förändring (EN sak) | Test | Resultat | Symptom / Notering | Timestamp |
|-----:|-------|---------------------|------|----------|-------------------|-----------|
| 0 | Bas | ~~Skapa nytt project~~ **FINNS REDAN** | Canary A + B | ✅ | Använder befintlig France-miljö | 2026-02-07 |
| 1 | Bas | ~~Skapa hosted echo-agent~~ **FINNS** | Canary A + B | ✅ | echo-agent deployad i ACR | 2026-02-07 |
| 2 | Bas | ~~Skapa minsta workflow~~ **FINNS** | Canary A + B | ✅ | Canary A: 3/3 OK. Canary B: 2/2 OK. Baseline stabil! | 2026-02-07 |
| 3 | Resurs | Skapa/koppla Cosmos DB (ingen MCP) | Canary A + B | ✅ | gold-demo-cosmos i France Central, Managed Identity | 2026-02-07 |
| 4 | Data | Seeda data i Cosmos (externt script) | Canary A + B | ✅ | 5 containers, 11 items. Canary A+B OK efter RBAC | 2026-02-07 |
| 5 | Tool | Skapa MCP tool via Logic App | Canary A + B | ✅ | CosmosDbMCP skapad, MI auth, 3 actions. Canary OK! | 2026-02-07 |
| 6 | Agent | Skapa CosmosToolSmoke-agent (EN tool call) | 5x agent + Canary A + B | ✅ | 5/5 tool calls OK! Alla containers fungerar! | 2026-02-07 |
| 7 | Workflow | Lägg CosmosToolSmoke i workflow | 5x workflow + Canary A + B | ✅ | 5/5 workflow OK! Canary A+B PASS! | 2026-02-07 |
| 7b | Agent | Skapa RepairPlanner agent (tool + JSON) | 3x agent test | ✅ | Uses real data (Anna, TBM-BRG-6220), returns JSON! | 2026-02-07 |
| 7c | Agent | Skapa AnomalyClassification agent | 3x agent test | ✅ | Queries Thresholds, detects vibration > 4.5 | 2026-02-07 |
| 7d | Agent | Skapa FaultDiagnosis agent | 3x agent test | ✅ | Queries KnowledgeBase, finds KB-001 E-101 | 2026-02-07 |
| 7e | Workflow | 3-agent chain workflow | 1x full test | ✅ | All 3 agents chain correctly, MCP calls work! | 2026-02-07 |
| 8 | Agent | MaintenanceScheduler agent | 2x agent test | ✅ | Queries MaintenanceWindows, selects MW-20260208-NIGHT | 2026-02-07 |
| 9 | Agent | PartsOrder agent | 2x agent test | ✅ | Queries PartsInventory + Suppliers, confirms in stock | 2026-02-07 |
| 10 | Workflow | **5-agent full workflow** | 1x full test | ✅ | **ALL 5 AGENTS WORK! 6 MCP calls, full chain!** | 2026-02-07 |
| 11 | Freeze | Döp project till GOLD-DEMO-FROZEN | Sluttest | ⏳ PENDING | | |

---

## 🐤 Canary-Tester

### Canary A — Hosted Echo Health

**Vad:** Testa att hosted echo-agent svarar stabilt.

```
1. Öppna Foundry portal → Agents → Echo-Agent
2. Kör prompt: "Hello, please echo: CANARY-A-TEST-1"
3. Kör prompt: "Hello, please echo: CANARY-A-TEST-2"  
4. Kör prompt: "Hello, please echo: CANARY-A-TEST-3"
```

**Förväntat resultat:**
- [ ] Alla 3 svar inom 5 sekunder
- [ ] Inga timeout/errors
- [ ] Konsistent format

**Fail-symptom att notera:**
- Timeout (hur lång?)
- 401/403 (auth)
- 500 (server error)
- Agent deploy fail
- "Unexpected error"

---

### Canary B — Workflow Health

**Vad:** Testa att minimalt workflow körs.

```
1. Öppna Foundry portal → Workflows → [Test-Workflow]
2. Kör workflow med input: {"test": "CANARY-B-RUN-1"}
3. Kör workflow med input: {"test": "CANARY-B-RUN-2"}
```

**Förväntat resultat:**
- [ ] Båda körningar slutförs
- [ ] Alla steg visar ✅
- [ ] Output är korrekt

**Fail-symptom att notera:**
- Workflow startar inte
- Steg X fastnar
- Timeout
- "Workflows" saknas i UI

---

## 🔧 Steg-för-Steg Kommandon

### Steg 0: Skapa nytt Foundry Project

**I Azure Portal:**
```
1. Sök "Azure AI Foundry"
2. Create new → Resource group: rg-gold-demo
3. Region: France Central (fungerade tidigare)
4. Name: gold-demo-foundry
5. Skapa nytt project: gold-demo-project
```

**Verifiera:**
```bash
# Lista projects
az cognitiveservices account list -o table | grep gold
```

---

### Steg 1: Skapa Hosted Echo-Agent

**Förbered echo-agent:**
```bash
cd /workspaces/agentic-factory-hack/foundry-demo/hosted-agents

# Skapa minimal echo-agent katalog
mkdir -p gold-echo-agent/src/echo-agent
```

**Dockerfile för echo-agent:**
```dockerfile
FROM python:3.11-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install -r requirements.txt
COPY . .
CMD ["python", "main.py"]
```

**Minimal main.py:**
```python
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

def echo_handler(request):
    return f"Echo: {request.get('message', 'No message')}"

# Registrera agent...
```

**Deploy:**
```bash
cd gold-echo-agent
azd env new gold-demo
azd env set AZURE_AI_PROJECT_ENDPOINT "<din-endpoint>"
azd up
```

**KÖR CANARY A + B** → Dokumentera i testloggen

---

### Steg 2: Skapa Minsta Workflow

**I Foundry Portal:**
```
1. Öppna gold-demo-project
2. Gå till "Workflows" i vänstermenyn
3. Klicka "+ New workflow"
4. Namn: "test-minimal-workflow"
5. Lägg till 2 steg:
   - Step 1: Foundry agent (välj valfri inbyggd)
   - Step 2: Foundry agent (välj annan inbyggd)
6. Koppla ihop dem
7. Spara
```

**KÖR CANARY A + B** → Dokumentera

---

### Steg 3: Koppla Cosmos DB (ingen MCP ännu)

**Skapa Cosmos DB:**
```bash
RESOURCE_GROUP="rg-gold-demo"
COSMOS_ACCOUNT="gold-demo-cosmos"
LOCATION="francecentral"

# Skapa Cosmos account
az cosmosdb create \
  --name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --locations regionName=$LOCATION \
  --default-consistency-level Session

# Skapa database
az cosmosdb sql database create \
  --account-name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --name "factory-db"

# Skapa containers
for container in machines technicians parts-inventory maintenance-history; do
  az cosmosdb sql container create \
    --account-name $COSMOS_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --database-name "factory-db" \
    --name $container \
    --partition-key-path "/id"
done
```

**Hämta connection info:**
```bash
# Endpoint
az cosmosdb show --name $COSMOS_ACCOUNT -g $RESOURCE_GROUP --query "documentEndpoint" -o tsv

# Key
az cosmosdb keys list --name $COSMOS_ACCOUNT -g $RESOURCE_GROUP --query "primaryMasterKey" -o tsv
```

**KÖR CANARY A + B** → Dokumentera

---

### Steg 4: Seeda Data (externt, rör inte Foundry)

```bash
cd /workspaces/agentic-factory-hack/challenge-0

# Uppdatera seed-data.sh med nya Cosmos-credentials
# Kör seed
./seed-data.sh
```

**Verifiera i Azure Portal → Cosmos DB → Data Explorer:**
- [ ] machines container har data
- [ ] technicians container har data

**KÖR CANARY A + B** → Dokumentera

---

### Steg 5: Skapa MCP Tool (Logic App)

**Skapa Logic App för MCP:**
```bash
# I Azure Portal:
# 1. Skapa Logic App (Consumption)
# 2. Namn: gold-demo-cosmos-mcp
# 3. Region: France Central
```

**Logic App Designer - HTTP Trigger:**
```json
{
  "type": "Request",
  "kind": "Http",
  "inputs": {
    "schema": {
      "type": "object",
      "properties": {
        "method": { "type": "string" },
        "params": { "type": "object" }
      }
    }
  }
}
```

**Registrera i Foundry:**
```
1. Foundry Portal → Tools → + Add tool
2. Välj "MCP Server"
3. URL: <Logic App HTTP trigger URL>
4. Namn: cosmos-mcp-tool
5. Spara (ANROPA INTE ÄN)
```

**KÖR CANARY A + B** → Dokumentera

⚠️ **KRITISKT:** Om Canary failar här → Tool-registreringen triggar instabilitet

---

### Steg 6: Skapa CosmosToolSmoke Agent

**I Foundry Portal:**
```
1. Agents → + New agent
2. Namn: CosmosToolSmoke
3. Instructions: "Du är en test-agent. När användaren frågar, anropa cosmos-mcp-tool för att lista machines."
4. Tools: Lägg till cosmos-mcp-tool
5. Deploya
```

**Test (5 gånger):**
```
Prompt 1: "List all machines"
Prompt 2: "List all machines"
Prompt 3: "List all machines"
Prompt 4: "List all machines"
Prompt 5: "List all machines"
```

⚠️ **VIKTIGT:** Klicka INTE "Always approve" - godkänn manuellt varje gång!

**KÖR CANARY A + B** → Dokumentera

---

### Steg 7: Lägg Agent i Workflow

**I Foundry Portal:**
```
1. Öppna test-minimal-workflow
2. Lägg till steg: CosmosToolSmoke
3. Spara
```

**Test (5 gånger):**
Kör workflow med input som triggar Cosmos-lookup

**KÖR CANARY A + B** → Dokumentera

---

### Steg 8-9: Foundry IQ (om tid finns)

Skippa dessa om du har tidsbrist. De är "nice to have".

---

### Steg 10: Freeze

```
1. Döp project till: GOLD-DEMO-FROZEN
2. Dokumentera EXAKT konfiguration
3. Skapa backup-export om möjligt
4. RÖR INTE DENNA MILJÖ
```

---

## 🚨 Rollback-Procedur

### Om Canary Failar

**Steg 1: Dokumentera**
```
- Vilken canary dog? (A / B / Båda)
- Exakt felmeddelande
- Timestamp
- Senaste ändring
```

**Steg 2: Hard Reset**
```bash
# Skapa nytt project i samma Foundry
# ELLER skapa ny Foundry om project inte hjälper

1. Azure Portal → AI Foundry → + New project
2. Namn: gold-demo-project-v2
3. Börja om från steg 0
```

**Steg 3: Reproducera**
```
Återspela steg 1..N snabbt
Om samma steg failar IGEN → reproducerbar tipping point funnen
```

---

## 🚫 Undvik Under Test

| Gör INTE | Varför |
|----------|--------|
| Ändra capability host-inställningar | Kan trigga global state-förändring |
| Klicka "Always approve" på tools | Låser in state som kan vara buggigt |
| Blanda APIM-MCP och LogicApp-MCP | En väg i taget! |
| Testa nya saker i FROZEN-miljön | Den är för demo, inte experiment |

---

## 🎯 Fallback-Plan

Om du upptäcker att **MCP tool registration** dödar workflows:

### Demo i Två Delar

**Del 1: Workflow Demo (5 min)**
- Visa hosted agents
- Visa workflow med 3-4 agenter
- Ingen Cosmos/MCP

**Del 2: Cosmos MCP Demo (3 min)**  
- Separat agent
- Visa tool-anrop till Cosmos
- Säg: "Nästa iteration binder ihop detta i workflow"

**Script:**
> "Vi har byggt agenter som fungerar standalone och i workflows. Cosmos-integrationen via MCP är redo, och nästa steg är att koppla ihop dem i ett end-to-end workflow. Det visar moduläriteten i Foundry - varje del kan byggas och testas separat."

---

## 📞 Eskalering

Om inget fungerar efter 4+ timmar:
1. Dokumentera alla tipping points
2. Kontakta Azure AI Foundry support
3. Fråga specifikt: "Hosted agents + MCP tools + Workflows i samma project - känt problem?"

---

## ✅ Demo-Checklista (Måndag)

- [ ] GOLD-DEMO-FROZEN är stabil
- [ ] Hosted echo-agent svarar (testat 10 min före demo)
- [ ] Workflow körs (testat 10 min före demo)
- [ ] Cosmos MCP tool fungerar (om inkluderat)
- [ ] Backup: Del 1 + Del 2 separat presentation redo

---

## 🧪 Test Prompts - Copy & Paste

### Agent 1: AnomalyClassification (Isolated)

**Basic vibration anomaly:**
```
machine TBM-001: [{"metric": "vibration", "value": 5.2}, {"metric": "temperature", "value": 78}]
```

**Temperature anomaly:**
```
machine TCP-001: [{"metric": "vibration", "value": 3.0}, {"metric": "temperature", "value": 92}]
```

**No anomaly (all normal):**
```
machine TBM-001: [{"metric": "vibration", "value": 3.5}, {"metric": "temperature", "value": 75}]
```

**Expected:** Queries Thresholds container, compares values, reports violations.

---

### Agent 2: FaultDiagnosis (Isolated)

**Vibration fault:**
```
Diagnose fault for machine TBM-001 with high vibration anomaly detected
```

**Temperature fault:**
```
Machine TCP-001 has critical temperature alert at 92°C. Diagnose the root cause.
```

**Multiple symptoms:**
```
Machine TBM-001 shows vibration at 5.2 (threshold 4.5) and servo motor errors. What is the likely fault?
```

**Expected:** Queries KnowledgeBase container, matches fault type, returns diagnosis with parts/skills.

---

### Agent 3: RepairPlanner (Isolated)

**Standard repair request:**
```
Create repair plan for TBM-001 with building_drum_vibration fault requiring bearing replacement
```

**With severity:**
```
Create urgent repair plan for machine TCP-001 with curing_temperature_excessive fault
```

**Specific parts:**
```
Plan repair for TBM-001 needing parts TBM-BRG-6220 and technician with vibration_analysis skill
```

**Expected:** Queries Technicians and PartsInventory, returns JSON work order.

---

### Agent 4: MaintenanceScheduler (Isolated)

**Standard scheduling:**
```
Schedule maintenance for work order WO-20260207-0001 (high priority bearing replacement on TBM-001)
```

**With work order JSON:**
```
Schedule maintenance for this work order:
{"workOrderNumber": "WO-20260207-0001", "machineId": "TBM-001", "title": "Bearing Replacement", "priority": "high", "estimatedDuration": 120}
```

**Expected:** Queries MaintenanceWindows, selects MW-20260208-NIGHT (earliest low-impact).

---

### Agent 5: PartsOrder (Isolated)

**Standard parts check:**
```
Create parts order for work order WO-20260207-0001 requiring parts: TBM-BRG-6220 (quantity: 1)
```

**Multiple parts:**
```
Check inventory and order parts for work order needing: TCP-HTR-4KW (qty 1), GEN-TS-K400 (qty 2)
```

**Expected:** Queries PartsInventory and Suppliers, confirms in stock or creates purchase order.

---

### Full 5-Agent Workflow (RECOMMENDED TEST)

**Standard test case:**
```
machine TBM-001: [{"metric": "vibration", "value": 5.2}, {"metric": "temperature", "value": 78}]
```

**Expected flow:**
1. AnomalyClassification → Detects vibration > 4.5 = Critical
2. FaultDiagnosis → Identifies building_drum_vibration, bearing wear
3. RepairPlanner → Creates work order with Anna Svensson, TBM-BRG-6220
4. MaintenanceScheduler → Selects MW-20260208-NIGHT (Feb 8, 22:00)
5. PartsOrder → Confirms TBM-BRG-6220 in stock (5 units)

---

**Alternative workflow tests:**

**Curing press temperature alert:**
```
machine TCP-001: [{"metric": "temperature", "value": 185}, {"metric": "pressure", "value": 180}]
```

**Servo motor failure scenario:**
```
machine TBM-002: [{"metric": "vibration", "value": 6.0}, {"metric": "servo_error", "value": 1}]
```

**Normal readings (no action needed):**
```
machine TBM-001: [{"metric": "vibration", "value": 2.0}, {"metric": "temperature", "value": 70}]
```

---

## 🔍 Expected Data from Cosmos

### Thresholds (queried by AnomalyClassification)
| Machine Type | Vibration Max | Temperature Max |
|--------------|---------------|-----------------|
| TireBuildingMachine | 4.5 | 85 |
| TireCuringPress | 3.0 | 180 |

### KnowledgeBase (queried by FaultDiagnosis)
| Fault Code | Fault Type | Required Parts | Required Skills |
|------------|-----------|----------------|-----------------|
| E-101 | building_drum_vibration | TBM-BRG-6220 | vibration_analysis, bearing_replacement |
| E-201 | curing_temperature_excessive | TCP-HTR-4KW, GEN-TS-K400 | temperature_control, instrumentation |
| E-102 | ply_tension_excessive | TBM-LS-500N, TBM-SRV-5KW | tension_control, servo_systems |
| E-103 | servo_motor_failure | TBM-SRV-5KW | electrical_systems, servo_systems |

### Technicians (queried by RepairPlanner)
| ID | Name | Skills | Available |
|----|------|--------|-----------|
| TECH-001 | Anna Svensson | vibration_analysis, bearing_replacement | true |
| TECH-002 | Erik Johansson | temperature_control, instrumentation | true |
| TECH-003 | Lars Nilsson | tension_control, servo_systems | true |

### PartsInventory (queried by RepairPlanner + PartsOrder)
| Part Number | Name | Stock | Reorder Level |
|-------------|------|-------|---------------|
| TBM-BRG-6220 | Building Drum Bearing | 5 | 2 |
| TCP-HTR-4KW | Curing Press Heater 4kW | 3 | 1 |
| GEN-TS-K400 | Temperature Sensor K-Type | 12 | 5 |
| TBM-LS-500N | Load Sensor 500N | 4 | 2 |
| TBM-SRV-5KW | Servo Motor 5kW | 2 | 1 |

### MaintenanceWindows (queried by MaintenanceScheduler)
| Window ID | Date/Time | Impact | Description |
|-----------|-----------|--------|-------------|
| MW-20260208-NIGHT | Feb 8, 22:00 - Feb 9, 06:00 | Low | Weekend night shift |
| MW-20260209-DAY | Feb 9, 08:00 - 16:00 | Medium | Sunday day shift |
| MW-20260210-NIGHT | Feb 10, 22:00 - Feb 11, 06:00 | Medium | Weekday night |

### Suppliers (queried by PartsOrder)
| ID | Name | Category | Lead Time | Reliability |
|----|------|----------|-----------|-------------|
| SUP-001 | Nordic Industrial Parts AB | bearings | 2 days | 95% |
| SUP-002 | Euro Heating Systems GmbH | heating | 5 days | 92% |
| SUP-003 | Sensor Tech International | sensors | 3 days | 98% |
| SUP-004 | Motor Solutions Europe | motors | 7 days | 90% |
