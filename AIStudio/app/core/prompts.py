from app.core.config import PROMPTS_DIR


def load_prompt(name: str) -> str:
    path = PROMPTS_DIR / name

    if not path.exists():
        raise FileNotFoundError(f"Prompt introuvable : {path}")

    return path.read_text(encoding="utf-8", errors="replace")
