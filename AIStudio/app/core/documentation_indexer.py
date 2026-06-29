from pathlib import Path
import json
import re

from app.core.config import DOCS_DIR


FRONTMATTER_RE = re.compile(r"^---\s*\n(.*?)\n---\s*\n", re.DOTALL)


def parse_frontmatter(content: str) -> dict:
    match = FRONTMATTER_RE.match(content)
    if not match:
        return {}

    metadata = {}
    current_key = None

    for raw_line in match.group(1).splitlines():
        line = raw_line.rstrip()

        if not line.strip():
            continue

        if line.startswith("  - ") and current_key:
            metadata.setdefault(current_key, []).append(line[4:].strip())
            continue

        if ":" in line:
            key, value = line.split(":", 1)
            key = key.strip()
            value = value.strip()

            if value:
                metadata[key] = value
                current_key = None
            else:
                metadata[key] = []
                current_key = key

    return metadata


def build_documentation_index() -> dict:
    index = {}

    for path in sorted(DOCS_DIR.rglob("*.md")):
        relative_path = path.relative_to(DOCS_DIR).as_posix()
        content = path.read_text(encoding="utf-8", errors="replace")
        metadata = parse_frontmatter(content)

        document_id = metadata.get("id") or relative_path.replace("/", ".").replace(".md", "")

        index[document_id] = {
            "file": relative_path,
            "title": metadata.get("title", path.stem),
            "tags": metadata.get("tags", []),
            "systems": metadata.get("systems", []),
            "owner": metadata.get("owner", ""),
            "priority": int(metadata.get("priority", 5)),
        }

    return index


def write_documentation_index() -> Path:
    index = build_documentation_index()
    output_path = DOCS_DIR / "documentation_index.json"

    output_path.write_text(
        json.dumps(index, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )

    return output_path