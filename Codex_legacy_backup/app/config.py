from pathlib import Path
import os

from dotenv import load_dotenv

load_dotenv()

AI_STUDIO_ROOT = Path(__file__).resolve().parents[2]
LIT_ROOT = AI_STUDIO_ROOT.parent

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
DEFAULT_MODEL = os.getenv("AI_STUDIO_MODEL", "gpt-5.4")
API_SECRET = os.getenv("AI_STUDIO_SECRET", "")

PROMPTS_DIR = AI_STUDIO_ROOT / "prompts"
LOGS_DIR = AI_STUDIO_ROOT / "logs"

KNOWLEDGE_DIR = AI_STUDIO_ROOT / "knowledge"
KNOWLEDGE_SYSTEMS_DIR = KNOWLEDGE_DIR / "systems"

ROOT_KNOWLEDGE_DOCS = [
    KNOWLEDGE_DIR / "AGENTS.md",
    KNOWLEDGE_DIR / "architecture.md",
    KNOWLEDGE_DIR / "current_work.md",
    KNOWLEDGE_DIR / "known_bugs.md",
]