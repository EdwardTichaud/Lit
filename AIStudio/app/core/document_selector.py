from app.core.documentation import load_index, read_document
from app.core.project_scanner import extract_search_terms, normalize_text


ALWAYS_INCLUDE = {
    "architecture.md",
}

EXCLUDE_FROM_CONTEXT = {
    "AGENTS.md",
}


def select_documents(question: str, mission=None, limit: int = 6) -> list[dict]:
    index = load_index()
    terms = extract_search_terms(question, limit=32)

    scored_documents: list[dict] = []

    for doc_id, doc in index.items():
        file_name = doc.get("file", "")

        if file_name in EXCLUDE_FROM_CONTEXT:
            continue

        content = read_document(file_name)
        score = _score_document(doc_id, doc, terms, content)

        if file_name in ALWAYS_INCLUDE:
            score += 20

        if score <= 0:
            continue

        scored_documents.append({
            "id": doc_id,
            "score": score,
            **doc,
        })

    scored_documents.sort(
        key=lambda item: (item["score"], -item.get("priority", 5), item["file"]),
        reverse=True,
    )

    selected = _ensure_required_documents(scored_documents, index, limit)

    if mission is not None:
        mission.candidate_documents = scored_documents[:limit * 2]

    return selected


def _score_document(doc_id: str, doc: dict, terms: list[str], content: str = "") -> int:
    searchable_sections = {
        "strong": " ".join([
            doc_id,
            doc.get("title", ""),
            doc.get("file", ""),
            " ".join(doc.get("tags", [])),
            " ".join(doc.get("systems", [])),
            doc.get("owner", ""),
        ]),
        "medium": " ".join(doc.get("keywords", [])),
        "weak": content or doc.get("preview", ""),
    }

    strong = normalize_text(searchable_sections["strong"])
    medium = normalize_text(searchable_sections["medium"])
    weak = normalize_text(searchable_sections["weak"])

    score = 0

    for term in terms:
        normalized_term = normalize_text(term)

        if normalized_term in strong:
            score += 12

        if normalized_term in medium:
            score += 5

        if normalized_term in weak:
            score += 2

    priority = doc.get("priority", 5)
    score += max(0, 6 - int(priority))

    return score


def _ensure_required_documents(scored_documents: list[dict], index: dict, limit: int) -> list[dict]:
    selected = scored_documents[:limit]
    selected_files = {item["file"] for item in selected}

    for required_file in ALWAYS_INCLUDE:
        if required_file in selected_files:
            continue

        required_doc = _find_document_by_file(index, required_file)

        if required_doc is None:
            continue

        selected.insert(0, required_doc)
        selected = selected[:limit]

    return selected


def _find_document_by_file(index: dict, file_name: str) -> dict | None:
    for doc_id, doc in index.items():
        if doc.get("file") == file_name:
            return {
                "id": doc_id,
                "score": 20,
                **doc,
            }

    return None
