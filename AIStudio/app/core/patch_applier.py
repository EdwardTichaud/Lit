from pathlib import Path
import re


FILE_BLOCK_RE = re.compile(
    r"^=== FILE: (?P<path>.+?) ===\s*\n"
    r"<<<FILE_CONTENT\s*\n"
    r"(?P<content>.*?)"
    r"\nFILE_CONTENT>>>",
    re.DOTALL | re.MULTILINE,
)


def extract_file_blocks(text: str) -> list[dict]:
    blocks = []

    for match in FILE_BLOCK_RE.finditer(text):
        blocks.append(
            {
                "path": match.group("path").strip(),
                "content": match.group("content"),
            }
        )

    return blocks


def is_safe_path(relative_path: str) -> bool:
    normalized = relative_path.replace("\\", "/").strip()

    if not normalized:
        return False

    if normalized.startswith("../"):
        return False

    if normalized.startswith("/"):
        return False

    if ":" in normalized:
        return False

    allowed_roots = [
        "README.md",
        "app/",
        "docs/",
        "prompts/",
    ]

    return any(
        normalized == root or normalized.startswith(root)
        for root in allowed_roots
    )


def apply_file_blocks(project_root: Path, text: str) -> list[Path]:
    written: list[Path] = []

    blocks = extract_file_blocks(text)

    if not blocks:
        raise ValueError("Aucun bloc de fichier applicable trouvé.")

    root = project_root.resolve()

    for block in blocks:
        relative_path = block["path"]
        content = block["content"]

        if not is_safe_path(relative_path):
            raise ValueError(f"Chemin interdit : {relative_path}")

        target = (root / relative_path).resolve()

        if not str(target).startswith(str(root)):
            raise ValueError(f"Chemin hors projet : {relative_path}")

        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")

        written.append(target)

    return written