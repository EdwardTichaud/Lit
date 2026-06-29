from pathlib import Path


def read_text(path: Path) -> str:
    """Lit un fichier texte UTF-8."""

    return path.read_text(
        encoding="utf-8",
        errors="replace"
    )


def write_text(path: Path, content: str):
    """Écrit un fichier UTF-8."""

    path.parent.mkdir(
        parents=True,
        exist_ok=True
    )

    path.write_text(
        content,
        encoding="utf-8"
    )