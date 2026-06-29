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


def build_keywords(content: str) -> list[str]:

    words = re.findall(r"[A-Za-z_]{4,}", content.lower())

    ignored = {
        "this",
        "that",
        "with",
        "from",
        "pour",
        "dans",
        "avec",
        "sont",
        "plus",
        "être",
        "dont",
        "comme",
        "vous",
        "leur",
        "ainsi",
    }

    frequency = {}

    for word in words:

        if word in ignored:
            continue

        frequency[word] = frequency.get(word, 0) + 1

    return sorted(
        frequency,
        key=frequency.get,
        reverse=True
    )[:20]


def build_documentation_index():

    index = {}

    for path in sorted(DOCS_DIR.rglob("*.md")):

        relative = path.relative_to(DOCS_DIR).as_posix()

        content = path.read_text(
            encoding="utf-8",
            errors="replace"
        )

        metadata = parse_frontmatter(content)

        document_id = metadata.get(
            "id",
            relative.replace("/", ".").replace(".md", "")
        )

        preview = content[:800]

        index[document_id] = {

            "file": relative,

            "title": metadata.get(
                "title",
                path.stem
            ),

            "priority": int(
                metadata.get(
                    "priority",
                    5
                )
            ),

            "tags": metadata.get(
                "tags",
                []
            ),

            "systems": metadata.get(
                "systems",
                []
            ),

            "owner": metadata.get(
                "owner",
                ""
            ),

            "size": len(content),

            "preview": preview,

            "keywords": build_keywords(content),
        }

    return index


def write_documentation_index():

    index = build_documentation_index()

    output = DOCS_DIR / "documentation_index.json"

    output.write_text(
        json.dumps(
            index,
            indent=2,
            ensure_ascii=False
        ),
        encoding="utf-8"
    )

    return output