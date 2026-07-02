from pathlib import Path, PurePosixPath


SAFE_LIT_ROOTS = {
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

    if root not in SAFE_LIT_ROOTS:
        return False

    if len(path.parts) <= 1:
        return False

    if Path(path.name).suffix.lower() in BLOCKED_BINARY_EXTENSIONS:
        return False

    return True
