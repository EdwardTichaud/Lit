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
        return self.prepare(question=question)

    def prepare(
        self,
        question: str,
        prepared_mission: MissionContext | None = None,
    ):
        mission = prepared_mission or MissionContext(
            query=question,
            user_messages=[question],
        )

        if prepared_mission is None:
            mission = build_context(mission)

        response = tracked_invoke(
            self.llm,
            [
                ("system", self.system_prompt),
                (
                    "user",
                    f"""
Question :

{question}

Documentation :

{mission.llm_context}

Fichiers Unity probables :

{mission.scanned_files}
""",
                ),
            ],
            mission,
            "architect_prepare",
        )

        mission.answer = (
            response.content
            if isinstance(response.content, str)
            else str(response.content)
        )

        return mission
