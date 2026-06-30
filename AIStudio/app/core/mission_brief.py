from __future__ import annotations

from datetime import datetime

from app.core.app_workflow import AppWorkflow
from app.core.config import LOGS_DIR
from app.core.mission import MissionContext


LATEST_MISSION_BRIEF = LOGS_DIR / "latest_mission_brief.md"


def write_mission_brief(mission: MissionContext) -> dict[str, str]:
    LOGS_DIR.mkdir(parents=True, exist_ok=True)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    archived_path = LOGS_DIR / f"mission_brief_{timestamp}.md"
    content = build_mission_brief(mission)

    LATEST_MISSION_BRIEF.write_text(content, encoding="utf-8")
    archived_path.write_text(content, encoding="utf-8")

    return {
        "latest": str(LATEST_MISSION_BRIEF),
        "archive": str(archived_path),
    }


def build_mission_brief(mission: MissionContext) -> str:
    workflow_title = _get_workflow_title(mission)

    return f"""# {workflow_title}

## Demande utilisateur

{mission.query or "_Aucune demande._"}

## Messages utilisateur

{_format_user_messages(mission.user_messages)}

## Documentation selectionnee

{_format_loaded_documents(mission.loaded_documents)}

## Fichiers Unity probables

{_format_scanned_files(mission.scanned_files)}

## Fichiers AIStudio charges

{_format_aistudio_files(getattr(mission, "aistudio_files", []))}

## Diagnostic API

{_format_llm_calls(mission.llm_calls)}

## Resultat

{_format_result(mission)}
"""


def _get_workflow_title(mission: MissionContext) -> str:
    if mission.workflow == AppWorkflow.AISTUDIO_CODE:
        return "Mission AIStudio"
    return "Mission Codex"


def _format_result(mission: MissionContext) -> str:
    if mission.workflow == AppWorkflow.CODEX_PROMPT:
        return mission.final_codex_prompt or mission.answer or "_Aucun prompt genere._"

    return mission.answer or "_Aucun resultat genere._"


def _format_user_messages(user_messages: list[str]) -> str:
    if not user_messages:
        return "- Aucun message."

    return "\n".join(f"- {message}" for message in user_messages)


def _format_loaded_documents(loaded_documents: list[dict]) -> str:
    if not loaded_documents:
        return "- Aucun document."

    lines = []

    for document in loaded_documents:
        lines.append(
            f"- `{document['file']}` (score {document.get('score', 0)})"
        )

    return "\n".join(lines)


def _format_scanned_files(scanned_files: list[dict]) -> str:
    if not scanned_files:
        return "- Aucun fichier probable."

    lines = []

    for file_info in scanned_files:
        lines.append(f"- `{file_info['path']}` (score {file_info['score']})")

    return "\n".join(lines)


def _format_aistudio_files(aistudio_files: list[dict]) -> str:
    if not aistudio_files:
        return "- Aucun fichier AIStudio."

    lines = []

    for file_info in aistudio_files:
        path = file_info.get("path") or file_info.get("file") or str(file_info)
        score = file_info.get("score", 0)
        lines.append(f"- `{path}` (score {score})")

    return "\n".join(lines)


def _format_llm_calls(llm_calls: list[dict]) -> str:
    if not llm_calls:
        return "- Aucun appel LLM."

    lines = []

    for call in llm_calls:
        lines.append(
            "- "
            f"{call['label']} | "
            f"modele: {call['model']} | "
            f"input: {call['input_tokens']} | "
            f"output: {call['output_tokens']} | "
            f"cout estime: ${call['estimated_cost_usd']:.6f} | "
            f"duree: {call['duration_seconds']} s"
        )

    return "\n".join(lines)