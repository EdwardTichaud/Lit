from app.core.app_workflow import AppWorkflow
from app.core.code_scanner import build_aistudio_code_context, scan_aistudio_code
from app.core.context_builder import build_context
from app.core.llm_tracking import tracked_invoke
from app.core.mission import MissionContext
from app.core.models import get_llm
from app.core.prompts import load_prompt


class ArchitectAgent:
    def __init__(self):
        self.llm = get_llm()
        self.system_prompt = load_prompt("architect.md")

    def ask(self, question: str):
        return self.prepare(
            question=question,
            workflow=AppWorkflow.CODEX_PROMPT,
        )

    def prepare(
        self,
        question: str,
        workflow: AppWorkflow,
        prepared_mission: MissionContext | None = None,
    ):
        mission = prepared_mission or MissionContext(
            query=question,
            workflow=workflow,
            user_messages=[question],
        )

        if prepared_mission is None:
            mission = build_context(mission)

        if workflow == AppWorkflow.AISTUDIO_CODE:
            mission.aistudio_files = scan_aistudio_code(mission.query)
            mission.aistudio_code_context = build_aistudio_code_context(
                mission.aistudio_files
            )

        response = tracked_invoke(
            self.llm,
            [
                ("system", self.system_prompt),
                (
                    "user",
                    f"""
Question :

{question}

Mode :

{mission.workflow.value}

Documentation :

{mission.llm_context}

Fichiers Unity probables :

{mission.scanned_files}

Fichiers AIStudio pertinents :

{mission.aistudio_code_context}
""",
                ),
            ],
            mission,
            "architect_prepare",
        )

        mission.answer = response.content

        return mission