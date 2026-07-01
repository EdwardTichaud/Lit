from pathlib import Path, PurePosixPath
import re


FILE_BLOCK_RE = re.compile(
    r"^=== FILE: (?P<path>.+?) ===\s*\n"
    r"<<<FILE_CONTENT\s*\n"
    r"(?P<content>.*?)"
    r"\nFILE_CONTENT>>>",
    re.DOTALL | re.MULTILINE,
)

ALLOWED_ROOTS = {
    "Assets",
    "Packages",
    "ProjectSettings",
}

BLOCKED_BINARY_EXTENSIONS = {
    ".aif",
    ".aiff",
    ".blend",
    ".dll",
    ".exe",
    ".fbx",
    ".gif",
    ".jpg",
    ".jpeg",
    ".mp3",
    ".mp4",
    ".ogg",
    ".pdf",
    ".png",
    ".psb",
    ".psd",
    ".tga",
    ".ttf",
    ".wav",
}


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

    if "\x00" in normalized:
        return False

    if ":" in normalized:
        return False

    path = PurePosixPath(normalized)

    if path.is_absolute():
        return False

    if any(part in {"", ".", ".."} for part in path.parts):
        return False

    if not path.parts:
        return False

    root = path.parts[0]

    if root not in ALLOWED_ROOTS:
        return False

    if len(path.parts) <= 1:
        return False

    if Path(path.name).suffix.lower() in BLOCKED_BINARY_EXTENSIONS:
        return False

    return True


def apply_file_blocks(project_root: Path, text: str) -> list[Path]:
    written: list[Path] = []

    blocks = extract_file_blocks(text)

    if not blocks:
        raise ValueError("Aucun bloc de fichier applicable trouvé.")

    root = project_root.resolve()
    targets: list[tuple[Path, str]] = []
    seen_paths: set[str] = set()

    for block in blocks:
        relative_path = block["path"].replace("\\", "/").strip()
        content = block["content"]

        if not is_safe_path(relative_path):
            raise ValueError(f"Chemin interdit : {relative_path}")

        target = (root / relative_path).resolve()

        try:
            target.relative_to(root)
        except ValueError:
            raise ValueError(f"Chemin hors projet : {relative_path}")

        target_key = target.as_posix()

        if target_key in seen_paths:
            raise ValueError(f"Chemin dupliqué : {relative_path}")

        seen_paths.add(target_key)
        targets.append((target, content))

    for target, content in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")

        written.append(target)

    return written
