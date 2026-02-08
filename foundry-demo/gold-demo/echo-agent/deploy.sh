#!/bin/bash
# =============================================================================
# GOLD Demo Quick Deploy Script
# =============================================================================
# Helps deploy the echo agent to the GOLD Foundry environment.
# Run from the gold-demo/echo-agent directory.
# =============================================================================

set -e

echo "🏆 GOLD Demo - Echo Agent Deployment"
echo "======================================="
echo ""

# Check if azd is installed
if ! command -v azd &> /dev/null; then
    echo "❌ azd (Azure Developer CLI) is not installed"
    echo "Install: https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd"
    exit 1
fi

# Check for required extension
echo "📦 Checking azd extensions..."
azd extension list 2>/dev/null | grep -q "azure.ai.agents" || {
    echo "Installing azure.ai.agents extension..."
    azd extension add azure.ai.agents
}

echo ""
echo "📋 Configuration Checklist:"
echo ""
echo "  1. Have you created a new Foundry project in Azure Portal?"
echo "     - Region: France Central (recommended)"
echo "     - Name: gold-demo-project"
echo ""
echo "  2. Do you have the project endpoint?"
echo "     Format: https://<name>.services.ai.azure.com/api/projects/<project-name>"
echo ""

read -p "Continue with deployment? (y/n) " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 0
fi

# Environment setup
echo ""
echo "🔧 Setting up environment..."

ENV_NAME="${GOLD_ENV_NAME:-gold-demo}"
echo "Environment name: $ENV_NAME"

# Check if environment exists
if azd env list 2>/dev/null | grep -q "$ENV_NAME"; then
    echo "Using existing environment: $ENV_NAME"
    azd env select "$ENV_NAME"
else
    echo "Creating new environment: $ENV_NAME"
    azd env new "$ENV_NAME"
fi

# Prompt for values if not set
if [ -z "$(azd env get-values 2>/dev/null | grep AZURE_AI_PROJECT_ENDPOINT)" ]; then
    echo ""
    read -p "Enter AZURE_AI_PROJECT_ENDPOINT: " endpoint
    azd env set AZURE_AI_PROJECT_ENDPOINT "$endpoint"
fi

if [ -z "$(azd env get-values 2>/dev/null | grep AZURE_LOCATION)" ]; then
    echo ""
    read -p "Enter AZURE_LOCATION (e.g., francecentral): " location
    azd env set AZURE_LOCATION "$location"
fi

echo ""
echo "🚀 Deploying echo agent..."
echo ""

azd up

echo ""
echo "✅ Deployment complete!"
echo ""
echo "📋 Next steps:"
echo "  1. Go to Azure AI Foundry portal"
echo "  2. Open your project"
echo "  3. Go to Agents section"
echo "  4. Find 'gold-echo-agent'"
echo "  5. Test with: 'Hello, test message'"
echo ""
echo "🐤 Run Canary A test (3x prompts) and document in GOLD-DEMO-RUNBOOK.md"
