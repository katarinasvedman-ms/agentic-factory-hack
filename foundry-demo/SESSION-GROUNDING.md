# 🧠 Session Grounding - Gold Demo Build

> **Read this first tomorrow to continue where we left off**
> **Last updated:** 2026-02-07 18:30 UTC

---

## 🎯 Goal

Build a complete **Azure AI Foundry demo** showcasing:
1. ✅ **Foundry Tools (MCP)** - Logic App exposing Cosmos DB as MCP server
2. ✅ **Prompt Agents** - 5 agents chained in a workflow
3. ✅ **Hosted Agents** - Code-based agent deployed to Foundry
4. ⏳ **Workflow with Hosted Agent** - Replace prompt agent with hosted in workflow

**Demo date:** Monday (2 days away)

---

## ✅ What We Accomplished Today

### 1. Infrastructure
- Created Cosmos DB `gold-demo-cosmos` (serverless, Managed Identity)
- Created Logic App `logicapp-957898-cosmos` with MCP endpoint
- Set up MCP tool connection `CosmosDbMCP` in Foundry
- Seeded 10 containers with factory data

### 2. Prompt Agents (All Working)
| Agent | Purpose | MCP Calls |
|-------|---------|-----------|
| AnomalyClassification | Detect threshold violations | 2 |
| FaultDiagnosis | Identify root cause | 1 |
| RepairPlanner | Create work order | 1 |
| MaintenanceScheduler | Select maintenance window | 1 |
| PartsOrder | Check inventory/order parts | 1 |

### 3. Workflow (Working)
- 5-agent chain using `System.LastMessage` for context passing
- All agents have `autoSend: true`
- Total: **6 MCP calls** per execution

### 4. Hosted Agent (Deployed & Working)
- `maintenance-scheduler-hosted` v2 deployed
- Uses `agent-framework[azure]>=1.0.0b260107`
- Successfully calls MCP tool from container
- Verified in Foundry traces: 2 Tool calls visible

---

## 🔑 Key Resources

| Resource | Value |
|----------|-------|
| **Foundry Project** | `ai-project-echo-agent-france` |
| **AI Account** | `ai-account-gihq46bsniq44` |
| **Resource Group** | `rg-echo-agent-france` |
| **Region** | France Central |
| **Cosmos DB** | `gold-demo-cosmos` |
| **MCP Tool** | `CosmosDbMCP` |
| **Logic App** | `logicapp-957898-cosmos` |
| **Container Registry** | `crgihq46bsniq44` |

---

## 📁 Key Files

| File | Purpose |
|------|---------|
| `/foundry-demo/GOLD-DEMO-RUNBOOK.md` | **Main runbook** - all instructions, working YAML, lessons learned |
| `/foundry-demo/gold-demo-agents/` | Agent YAML files for all 5 agents + workflow |
| `/foundry-demo/gold-demo-agents/hosted-agents/` | Hosted agent code + azd config |
| `/foundry-demo/gold-demo-agents/hosted-agents/maintenance/` | MaintenanceScheduler hosted code |

---

## 🧪 Test Commands

### Test Prompt Agents (via Portal)
```
machine TBM-001: [{"metric": "vibration", "value": 5.2}, {"metric": "temperature", "value": 78}]
```

### Test Hosted Agent (via Portal)
```
Schedule maintenance for work order WO-20260207-0001 (high priority bearing replacement on TBM-001)
```

### Check Hosted Agent Status
```bash
az cognitiveservices agent show \
    --account-name ai-account-gihq46bsniq44 \
    --project-name ai-project-echo-agent-france \
    --name maintenance-scheduler-hosted
```

---

## ⏭️ Next Steps (Tomorrow)

1. **Add hosted agent to workflow**
   - Replace prompt-based MaintenanceScheduler with hosted version
   - Test workflow with hosted agent in chain

2. **Optional: Create PartsOrder as hosted agent**
   - Similar pattern to MaintenanceScheduler

3. **Polish demo flow**
   - Practice the demo sequence
   - Prepare talking points

---

## ⚠️ Critical Warnings

### DO NOT RUN:
```bash
azd up      # Creates new infrastructure
azd down    # DELETES EVERYTHING (we lost the project yesterday!)
```

### ONLY USE:
```bash
azd deploy --no-prompt  # Safe - only deploys code
```

---

## 📚 Lessons Learned

1. **Agent instructions must be explicit** - "Do NOT ask for confirmation"
2. **MCP parameters must be exact** - `cosmosDbAccountName: "gold-demo-cosmos"`
3. **Package versions matter** - use `agent-framework[azure]>=1.0.0b260107`
4. **azd env must be set correctly** - `AZURE_AI_PROJECT_ID`, `AZURE_AI_PROJECT_ENDPOINT`, `AZURE_OPENAI_ENDPOINT`
5. **Traces show MCP calls** - Look for "Tool ✓" entries in Foundry traces

---

## 🔗 Quick Links

- [Foundry Portal](https://ai.azure.com)
- [Hosted Agent Playground](https://ai.azure.com/nextgen/r/wUvdYh5PT8yXkP_fzQUcXw,rg-echo-agent-france,,ai-account-gihq46bsniq44,ai-project-echo-agent-france/build/agents/maintenance-scheduler-hosted/build?version=2)
- [Resource Group](https://portal.azure.com/#@/resource/subscriptions/c14bdd62-1e4f-4fcc-9790-ffdfcd051c5f/resourceGroups/rg-echo-agent-france/overview)

---

## 💡 For Copilot: Instructions

When continuing this session:
1. Read `/foundry-demo/GOLD-DEMO-RUNBOOK.md` for full context
2. The goal is to integrate the hosted agent into the workflow
3. Use `azd deploy` only - never `azd up` or `azd down`
4. All 5 prompt agents are working - don't recreate them
5. The hosted agent `maintenance-scheduler-hosted` v2 is deployed and working
