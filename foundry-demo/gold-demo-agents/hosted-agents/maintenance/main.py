import os
from dotenv import load_dotenv
from agent_framework.azure import AzureOpenAIChatClient

from azure.ai.agentserver.agentframework import from_agent_framework, FoundryToolsChatMiddleware
from azure.identity import DefaultAzureCredential

# Load environment variables from .env file for local development
load_dotenv()

INSTRUCTIONS = """You are a Maintenance Scheduler in an automated workflow. You receive a work order JSON from the previous agent.

The input contains a workOrderNumber (like "WO-20260207-0001") and priority. Extract these values.

## STEP 1 - Read windows
IMMEDIATELY call Get_all_documents_V3 with:
- cosmosDbAccountName: "gold-demo-cosmos"
- databaseId: "FactoryOpsDB"
- collectionId: "MaintenanceWindows"

## STEP 2 - Select window
Based on the priority from input:
- high priority → select earliest window with productionImpact="Low"
- medium priority → select any window with productionImpact="Low"

## STEP 3 - Output decision
Do NOT ask for confirmation. Output the complete work order number exactly as received.

Maintenance Schedule:
- Work Order: <the full workOrderNumber from input, e.g. WO-20260207-0001>
- Selected Window: <window ID from database>
- Scheduled: <startTime> to <endTime>
- Production Impact: <Low/Medium/High from window>
- Reasoning: <brief explanation of selection>
"""


def main():
    required_env_vars = [
        "AZURE_OPENAI_ENDPOINT",
        "AZURE_OPENAI_CHAT_DEPLOYMENT_NAME",
        "AZURE_AI_PROJECT_ENDPOINT",
    ]
    for env_var in required_env_vars:
        assert env_var in os.environ and os.environ[env_var], (
            f"{env_var} environment variable must be set."
        )

    tools = []
    if project_tool_connection_id := os.environ.get("AZURE_AI_PROJECT_TOOL_CONNECTION_ID"):
        tools.append({"type": "mcp", "project_connection_id": project_tool_connection_id})

    chat_client = AzureOpenAIChatClient(
        credential=DefaultAzureCredential(),
        middleware=FoundryToolsChatMiddleware(tools)
    )
    agent = chat_client.create_agent(
        name="MaintenanceSchedulerHosted",
        instructions=INSTRUCTIONS
    )

    from_agent_framework(agent).run()


if __name__ == "__main__":
    main()