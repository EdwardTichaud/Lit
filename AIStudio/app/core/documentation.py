from pathlib import Path
import json
import re

from app.core.config import DOCS_DIR


INDEX_PATH = DOCS_DIR / "documentation_index.json"


def load_index() -> dict:
    if not INDEX_PATH.exists():
        raise FileNotFoundError(
            f"Index documentaire introuvable : {INDEX_PATH}"
        )

    return json.loads(
        INDEX_PATH.read_text(encoding="utf-8")
    )


def read_document(relative_file: str) -> str:
    path = DOCS_DIR / relative_file

    if not path.exists():
        return f"[FICHIER INTROUVABLE] {relative_file}"

    return path.read_text(
        encoding="utf-8",
        errors="replace"
    )


def tokenize(text: str) -> list[str]:
    """
    Découpe une phrase en mots significatifs.
    """

    words = re.findall(r"[a-zA-Z0-9_]+", text.lower())

    ignored = {
        "je",
        "veux",
        "modifier",
        "ajouter",
        "le",
        "la",
        "les",
        "de",
        "du",
        "des",
        "un",
        "une",
        "et",
        "pour",
        "dans",
        "sur",
        "avec",
        "systeme",
        "système",
    }

    return [
        w for w in words
        if w not in ignored and len(w) > 2
    ]


def find_by_text(query: str, limit: int = 5) -> list[dict]:

    keywords = tokenize(query)

    results = []

    for doc_id, doc in load_index().items():

        searchable = " ".join([
            doc_id,
            doc.get("title", ""),
            doc.get("file", ""),
            " ".join(doc.get("tags", [])),
            " ".join(doc.get("systems", [])),
            doc.get("owner", ""),
        ]).lower()

        score = 0

        for keyword in keywords:

            if keyword in searchable:
                score += 10

        if score > 0:

            results.append({
                "id": doc_id,
                "score": score,
                **doc,
            })

    results.sort(
        key=lambda x: x["score"],
        reverse=True,
    )

    return results[:limit]