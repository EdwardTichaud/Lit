from pathlib import Path
import json

from app.core.config import DOCS_DIR


INDEX_PATH = DOCS_DIR / "documentation_index.json"


def load_index() -> dict:
    if not INDEX_PATH.exists():
        raise FileNotFoundError(
            f"Index documentaire introuvable : {INDEX_PATH}. "
            "Lance d'abord : python -m app.main"
        )

    return json.loads(
        INDEX_PATH.read_text(encoding="utf-8")
    )


def read_document(relative_file: str) -> str:
    path = DOCS_DIR / relative_file

    if not path.exists():
        return f"[FICHIER INTROUVABLE] {relative_file}"

    return path.read_text(encoding="utf-8", errors="replace")


def find_by_tag(tag: str, limit: int = 5) -> list[dict]:
    tag = tag.lower().strip()
    results = []

    for doc_id, doc in load_index().items():
        tags = [t.lower() for t in doc.get("tags", [])]
        systems = [s.lower() for s in doc.get("systems", [])]

        if tag in tags or tag in systems:
            results.append({
                "id": doc_id,
                "score": int(doc.get("priority", 5)),
                **doc,
            })

    return sorted(results, key=lambda x: x["score"], reverse=True)[:limit]


def find_by_text(query: str, limit: int = 5) -> list[dict]:
    query = query.lower().strip()
    results = []

    for doc_id, doc in load_index().items():
        searchable = " ".join([
            doc_id,
            doc.get("title", ""),
            " ".join(doc.get("tags", [])),
            " ".join(doc.get("systems", [])),
            doc.get("owner", ""),
        ]).lower()

        if query in searchable:
            results.append({
                "id": doc_id,
                "score": int(doc.get("priority", 5)),
                **doc,
            })

    return sorted(results, key=lambda x: x["score"], reverse=True)[:limit]