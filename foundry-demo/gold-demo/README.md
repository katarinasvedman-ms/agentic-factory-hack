# =============================================================================
# GOLD Demo Structure
# =============================================================================
#
# This folder contains clean, minimal agent implementations for the GOLD demo.
# Follow the GOLD-DEMO-RUNBOOK.md for step-by-step deployment.
#
# Structure:
#   gold-demo/
#   ├── echo-agent/          # Step 1: Canary agent (no tools)
#   ├── cosmos-smoke-agent/  # Step 6: Minimal Cosmos MCP test agent
#   └── README.md            # This file
#
# CRITICAL: Deploy agents in ORDER. Test after EACH step.
# =============================================================================

## Echo Agent (Step 1)

Minimal hosted agent for Canary A testing. No tools, no external dependencies.

```bash
cd echo-agent
azd env new gold-demo
azd env set AZURE_AI_PROJECT_ENDPOINT "<your-gold-foundry-endpoint>"
azd up
```

## Cosmos Smoke Agent (Step 6)

Single-purpose agent that makes ONE Cosmos MCP tool call.

Deploy ONLY after steps 1-5 are verified stable.

```bash
cd cosmos-smoke-agent
# Configure with MCP tool connection
azd up
```

## Verification

After each deployment, run:
1. Canary A: Chat with echo-agent 3x
2. Canary B: Run minimal workflow 2x

Document results in ../GOLD-DEMO-RUNBOOK.md
