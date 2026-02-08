# =============================================================================
# GOLD Demo Echo Agent
# =============================================================================
# Minimal echo agent for Canary A testing.
# Echoes user messages with a timestamp prefix.
# NO external dependencies, NO tools - just pure echo.
# =============================================================================

import asyncio
from collections.abc import AsyncIterable
from datetime import datetime
from typing import Any

from agent_framework import (
    AgentRunResponse,
    AgentRunResponseUpdate,
    AgentThread,
    BaseAgent,
    ChatMessage,
    Role,
    TextContent,
)
from azure.ai.agentserver.agentframework import from_agent_framework


class GoldEchoAgent(BaseAgent):
    """GOLD Demo canary agent - echoes messages for stability testing."""

    def __init__(
        self,
        *,
        name: str | None = None,
        description: str | None = None,
        **kwargs: Any,
    ) -> None:
        super().__init__(
            name=name or "GoldEchoAgent",
            description=description or "GOLD Demo canary echo agent",
            **kwargs,
        )

    async def run(
        self,
        messages: str | ChatMessage | list[str] | list[ChatMessage] | None = None,
        *,
        thread: AgentThread | None = None,
        **kwargs: Any,
    ) -> AgentRunResponse:
        """Echo the last user message."""
        normalized_messages = self._normalize_messages(messages)
        
        timestamp = datetime.utcnow().strftime("%H:%M:%S")
        
        if not normalized_messages:
            response_text = f"[{timestamp}] 🟢 GOLD Echo Agent ready. Send a message to test."
        else:
            last_message = normalized_messages[-1]
            user_text = last_message.text if last_message.text else "[no text]"
            response_text = f"[{timestamp}] 🔊 Echo: {user_text}"

        response_message = ChatMessage(
            role=Role.ASSISTANT,
            contents=[TextContent(text=response_text)]
        )

        if thread is not None:
            await self._notify_thread_of_new_messages(thread, normalized_messages, response_message)

        return AgentRunResponse(messages=[response_message])

    async def run_stream(
        self,
        messages: str | ChatMessage | list[str] | list[ChatMessage] | None = None,
        *,
        thread: AgentThread | None = None,
        **kwargs: Any,
    ) -> AsyncIterable[AgentRunResponseUpdate]:
        """Stream echo response word by word."""
        normalized_messages = self._normalize_messages(messages)
        
        timestamp = datetime.utcnow().strftime("%H:%M:%S")
        
        if not normalized_messages:
            response_text = f"[{timestamp}] 🟢 GOLD Echo Agent ready. Send a message to test."
        else:
            last_message = normalized_messages[-1]
            user_text = last_message.text if last_message.text else "[no text]"
            response_text = f"[{timestamp}] 🔊 Echo: {user_text}"

        # Stream word by word
        words = response_text.split()
        for i, word in enumerate(words):
            chunk = f" {word}" if i > 0 else word
            yield AgentRunResponseUpdate(
                contents=[TextContent(text=chunk)],
                role=Role.ASSISTANT,
            )
            await asyncio.sleep(0.05)  # Fast streaming

        if thread is not None:
            complete_response = ChatMessage(
                role=Role.ASSISTANT,
                contents=[TextContent(text=response_text)]
            )
            await self._notify_thread_of_new_messages(thread, normalized_messages, complete_response)


def create_agent() -> GoldEchoAgent:
    """Factory function for the agent."""
    return GoldEchoAgent(
        name="GoldEchoAgent",
        description="GOLD Demo canary echo agent for stability testing"
    )


if __name__ == "__main__":
    from_agent_framework(create_agent()).run()
