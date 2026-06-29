from app.core.config import (
    DEFAULT_MODEL,
    OPENAI_API_KEY,
)


def get_llm():
    try:
        from langchain_openai import ChatOpenAI
    except ImportError as exc:
        raise RuntimeError(
            "Dependance manquante : installe langchain-openai pour lancer l'analyse LLM."
        ) from exc

    return ChatOpenAI(
        model=DEFAULT_MODEL,
        api_key=OPENAI_API_KEY,
        temperature=0.2,
    )
