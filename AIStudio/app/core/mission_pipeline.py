from __future__ import annotations

from app.agents.architect import ArchitectAgent
from app.core.context_builder import build_context
from app.core.mission import MissionContext
from app.core.project_scanner import scan_project


SCANNED_FILE_LIMIT = 20


def build_codex_prompt_request(mission: MissionContext) -> str:
    return f"""
Prépare un prompt Codex pour le projet Unity Lit.

Produit uniquement :
- analyse ;
- risques ;
- plan ;
- tests à prévoir ;
- prompt Codex final.

Ne modifie aucun fichier et ne produis aucun patch.

Mission utilisateur :

{mission.query}
"""


def run_mission(query: str) -> MissionContext:
    mission = MissionContext(
        query=query,
        user_messages=[query],
        scanned_file_limit=SCANNED_FILE_LIMIT,
    )

    _collect_context(mission)
    prompt = build_codex_prompt_request(mission)

    try:
        mission = ArchitectAgent().prepare(
            question=prompt,
            prepared_mission=mission,
        )
    except Exception as exc:
        mission.notes.append(f"LLM indisponible : {exc}")
        mission.answer = _build_local_fallback(mission, error=exc)

    mission.final_codex_prompt = mission.answer
    return mission


def _collect_context(mission: MissionContext) -> None:
    try:
        build_context(mission)
    except Exception as exc:
        mission.notes.append(f"Documentation indisponible : {exc}")

    try:
        mission.scanned_files = scan_project(
            mission.query,
            limit=mission.scanned_file_limit,
        )
    except Exception as exc:
        mission.notes.append(f"Scanner Unity indisponible : {exc}")


def _build_local_fallback(
    mission: MissionContext,
    *,
    error: Exception,
) -> str:
    docs = _format_document_list(mission.loaded_documents)
    unity_files = _format_unity_file_list(mission.scanned_files)

    return f"""## 1. Compréhension de la demande

{mission.query}

## 2. Systèmes probablement concernés

À confirmer à partir des fichiers listés par le scanner.

## 3. Documentation utilisée

{docs}

## 4. Fichiers Unity probables

{unity_files}

## 5. Risques

- Le LLM n'a pas pu être appelé : {error}
- Ne modifier aucun fichier sans validation explicite.

## 6. Questions bloquantes

Aucune question bloquante identifiée automatiquement.

## 7. Approche recommandée

Limiter l'intervention aux fichiers réellement concernés, vérifier les dépendances Unity, puis tester dans l'éditeur.

## 8. Prompt Codex final

Tu travailles dans le projet Lit. Réponds à cette mission :

{mission.query}

Contrainte principale : préserve les changements Git existants et limite l'intervention aux fichiers réellement concernés.
"""


def _format_document_list(documents: list[dict]) -> str:
    if not documents:
        return "- Aucun document chargé."

    return "\n".join(
        f"- `{doc.get('file', doc)}`"
        for doc in documents
    )


def _format_unity_file_list(files: list[dict]) -> str:
    if not files:
        return "- Aucun fichier probable."

    return "\n".join(
        f"- `{file.get('path', file)}` (score {file.get('score', 0)})"
        for file in files[:20]
    )
