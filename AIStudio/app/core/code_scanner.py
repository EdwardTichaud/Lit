from pathlib import Path
import re

from app.core.config import AI_STUDIO_ROOT


IGNORED_DIRS = {
    ".venv",
    "__pycache__",
    ".git",
    "logs",
}

ALLOWED_EXTENSIONS = {
    ".py",
    ".md",
    ".txt",
}


def tokenize(text: str) -> list[str]:
    words = re.findall(r"[a-zA-Z0-9_]{3,}", text.lower())

    ignored = {
        "avec",
        "dans",
        "pour",
        "les",
        "des",
        "une",
        "que",
        "qui",
        "faire",
        "ajouter",
        "modifier",
        "corriger",
        "aistudio",
    }

    return [w for w in words if w not in ignored]


def iter_aistudio_files() -> list[Path]:
    files: list[Path] = []

    for path in AI_STUDIO_ROOT.rglob("*"):
        if not path.is_file():
            continue

        relative_parts = set(path.relative_to(AI_STUDIO_ROOT).parts)

        if relative_parts & IGNORED_DIRS:
            continue

        if path.suffix not in ALLOWED_EXTENSIONS:
            continue

        files.append(path)

    return files


def score_file(path: Path, query_keywords: list[str]) -> int:
    relative = path.relative_to(AI_STUDIO_ROOT).as_posix().lower()

    try:
        content = path.read_text(encoding="utf-8", errors="replace").lower()
    except Exception:
        return 0

    score = 0

    for keyword in query_keywords:
        if keyword in relative:
            score += 15

        if keyword in content:
            score += 5

    if relative.startswith("app/"):
        score += 5

    if "chat.py" in relative:
        score += 10

    if "mission" in relative:
        score += 8

    if "scanner" in relative:
        score += 8

    return score


def scan_aistudio_code(query: str, limit: int = 8) -> list[dict]:
    keywords = tokenize(query)
    results: list[dict] = []

    for path in iter_aistudio_files():
        score = score_file(path, keywords)

        if score <= 0:
            continue

        relative = path.relative_to(AI_STUDIO_ROOT).as_posix()

        content = path.read_text(encoding="utf-8", errors="replace")

        results.append(
            {
                "file": relative,
                "score": score,
                "content": content[:6000],
            }
        )

    results.sort(key=lambda item: item["score"], reverse=True)

    return results[:limit]


def build_aistudio_code_context(files: list[dict]) -> str:
    parts: list[str] = []

    for file in files:
        parts.append(f"\n\n===== {file['file']} | score {file['score']} =====\n")
        parts.append(file["content"])

    return "\n".join(parts)