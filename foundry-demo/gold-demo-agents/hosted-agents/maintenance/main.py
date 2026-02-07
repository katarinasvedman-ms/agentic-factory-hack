import os
from dotenv import load_dotenv
from agent_framework.azure import AzureOpenAIChatClient

from azure.ai.agentserver.agentframework import from_agent_framework, FoundryToolsChatMiddleware
from azure.identity import DefaultAzureCredential

# Load environment variables from .env file for local development
load_dotenv()

INSTRUCTIONS = """You are a Maintenance Scheduler. You receive a work order and automatically select the best maintenance window.

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