from app.core.context_builder import build_context
from app.core.documentation_indexer import write_documentation_index
from app.core.llm_tracking import tracked_invoke
from app.core.mission import MissionContext
from app.core.models import get_llm
from app.core.project_scanner import extract_search_terms, scan_project
from app.core.prompts import load_prompt


class ArchitectAgent:

    def __init__(self):
        self.system_prompt = load_prompt("architect.md")

    def ask(self, question: str):
        return self.prepare([question])

    def prepare(self, user_messages: list[str]) -> MissionContext:
        query = "\n".join(message.strip() for message in user_messages if message.strip())

        mission = MissionContext(
            query=query,
            user_messages=list(user_messages),
        )

        write_documentation_index()
        mission = build_context(mission)
        mission.scanned_files = scan_project(mission.query)

        llm = get_llm()

        response = tracked_invoke(
            llm,
            [
                ("system", self.system_prompt),
                (
                    "user",
                    self._build_user_prompt(mission),
                ),
            ],
            mission,
            "architect_mission",
        )

        mission.answer = response.content
        mission.final_codex_prompt = response.content

        return mission

    def _build_user_prompt(self, mission: MissionContext) -> str:
        return f"""
Demande utilisateur consolidee :

{mission.query}

Messages utilisateur :

{self._format_user_messages(mission.user_messages)}

Documentation selectionnee :

{self._format_loaded_documents(mission)}

Contenu documentaire :

{mission.llm_context}

Fichiers Unity probables identifies par scan Python :

{self._format_scanned_files(mission.scanned_files)}

Mots-cles conseilles pour Codex :

{", ".join(extract_search_terms(mission.query, limit=32))}

Instructions Codex obligatoires :

- Avant toute modification, lire `AIStudio/docs/AGENTS.md`.
- Lire ensuite uniquement les documents selectionnes ci-dessus.
- Preserver les changements Git existants.
- Produire un patch minimal et expliquer les tests a effectuer.
"""

    def _format_user_messages(self, user_messages: list[str]) -> str:
        if not user_messages:
            return "- Aucun message."

        return "\n".join(f"- {message}" for message in user_messages)

    def _format_loaded_documents(self, mission: MissionContext) -> str:
        if not mission.loaded_documents:
            return "- Aucun document selectionne."

        lines = []

        for document in mission.loaded_documents:
            lines.append(
                f"- {document['file']} (score {document.get('score', 0)})"
            )

        return "\n".join(lines)

    def _format_scanned_files(self, scanned_files: list[dict]) -> str:
        if not scanned_files:
            return "- Aucun fichier probable trouve."

        lines = []

        for file_info in scanned_files:
            lines.append(f"- {file_info['path']} (score {file_info['score']})")

            for match in file_info.get("matches", [])[:2]:
                lines.append(f"  ligne {match['line']}: {match['text']}")

        return "\n".join(lines)
