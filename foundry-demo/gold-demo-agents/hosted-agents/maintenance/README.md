# Hosted Agents

## Why Hosted Agents?

**"When you need custom logic (Python, .NET ...), external SDKs, like LangGraph - you deploy a Hosted Agent. It's your code running in Azure Container Apps, but managed by Foundry, with full access to Foundry's MCP tools and managed identity. You can deploy agents and workflows."**

## What Is It?

A hosted agent is:
- **Your custom code** packaged as a Docker container
- **Deployed to Azure Container Apps** via Foundry
- **Connected to Foundry tools (IQ, Bing, or custom like CosmosDb as MCP etc)** through Foundry's middleware
- **Called like any other agent** in workflows

## The Two Files

### agent.yaml - Declares the agent

```yaml
kind: hosted           # Not a prompt agent - runs custom code
tools:
  - type: mcp
    project_connection_id: CosmosDbMCP  # Same MCP tools as prompt agents
```

### main.py - Implements the logic

```python
chat_client = AzureOpenAIChatClient(
    middleware=FoundryToolsChatMiddleware(tools)  # Injects MCP tools
)
agent = chat_client.create_agent(instructions=INSTRUCTIONS)
from_agent_framework(agent).run()  # Starts the agent server
```

## Prompt Agent vs Hosted Agent

| Aspect | Prompt Agent | Hosted Agent |
|--------|--------------|--------------|
| Definition | YAML only | Code + YAML |
| Runtime | Foundry's runtime | Your container (managed by Foundry) |
| Custom SDKs | No | Yes (LangGraph, Semantic Kernel, etc.) |
| Custom code | No | Yes (Python, .NET, etc.) 

**When to use Hosted:**
- Need external SDKs (LangGraph, custom frameworks)
- Want to run your own code/container
- Need to integrate with systems that require custom auth/logic
- Building with Semantic Kernel, AutoGen, or other agent frameworks

## Local Development

```bash
cd maintenance

# Set environment variables
export AZURE_AI_PROJECT_ENDPOINT="https://..."
export AZURE_OPENAI_ENDPOINT="https://..."
export AZURE_OPENAI_CHAT_DEPLOYMENT_NAME="gpt-4.1"
export AZURE_AI_PROJECT_TOOL_CONNECTION_ID="CosmosDbMCP"

# Install dependencies
pip install -r requirements.txt

# Run locally
python main.py
```

## Prerequisites

### Create Account-Level Capability Host

Hosted agents require a capability host with public hosting enabled (one-time setup per AI account):

```bash
az rest --method put \
    --url "https://management.azure.com/subscriptions/[SUBSCRIPTIONID]/resourceGroups/[RESOURCEGROUPNAME]/providers/Microsoft.CognitiveServices/accounts/[ACCOUNTNAME]/capabilityHosts/accountcaphost?api-version=2025-10-01-preview" \
    --headers "content-type=application/json" \
    --body '{
        "properties": {
            "capabilityHostKind": "Agents",
            "enablePublicHostingEnvironment": true
        }
    }'
```

This creates the Container Apps infrastructure needed to host your agents.

## Deployment

### CLI Tools

Two CLI tools support deploying hosted agents:

| Tool | Command | Description |
|------|---------|-------------|
| **Azure Developer CLI (azd)** | `azd deploy` | Builds container, pushes to ACR, deploys to Container Apps |
| **Azure AI CLI** | `az cognitiveservices agent create` | Creates hosted agent from source or image |

### GitHub Actions

Hosted agents are fully CI/CD ready. Your code and YAML are versioned in Git, and `azd deploy` handles the complete pipeline - no portal clicks required after initial setup.

```yaml
# .github/workflows/deploy-hosted-agent.yml
name: Deploy Hosted Agent

on:
  push:
    paths:
      - 'hosted-agents/maintenance/**'

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Azure Login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      
      - name: Install azd
        run: curl -fsSL https://aka.ms/install-azd.sh | bash
      
      - name: Deploy hosted agent
        working-directory: hosted-agents
        run: azd deploy --no-prompt
        env:
          AZURE_ENV_NAME: ${{ vars.AZURE_ENV_NAME }}
```