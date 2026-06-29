from pathlib import Path

from app.core.config import ROOT_DOCS, DOCS_SYSTEMS_DIR


def read_file(path: Path) -> str:
    if not path.exists():
        return f"[FICHIER INTROUVABLE] {path}"

    return path.read_text(encoding="utf-8", errors="replace")


def load_core_context() -> str:
    parts: list[str] = []

    for path in ROOT_DOCS:
        parts.append(f"\n# {path.name}\n")
        parts.append(read_file(path))

    if DOCS_SYSTEMS_DIR.exists():
        for path in sorted(DOCS_SYSTEMS_DIR.glob("*.md")):
            parts.append(f"\n# systems/{path.name}\n")
            parts.append(read_file(path))

    return "\n".join(parts)