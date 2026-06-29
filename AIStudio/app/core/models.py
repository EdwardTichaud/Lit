from langchain_openai import ChatOpenAI

from app.core.config import (
    DEFAULT_MODEL,
    OPENAI_API_KEY,
)


def get_llm():
    return ChatOpenAI(
        model=DEFAULT_MODEL,
        api_key=OPENAI_API_KEY,
        temperature=0.2,
    )