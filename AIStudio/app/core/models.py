from langchain_openai import ChatOpenAI

from app.core.config import DEFAULT_MODEL


def get_default_model():
    return ChatOpenAI(
        model=DEFAULT_MODEL,
        temperature=0.2,
    )
