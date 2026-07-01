from dataclasses import dataclass, field

from app.core.app_workflow import AppWorkflow


@dataclass
class MissionContext:
    query: str
    workflow: AppWorkflow

    user_messages: list[str] = field(default_factory=list)

    candidate_documents: list[dict] = field(default_factory=list)
    loaded_documents: list[dict] = field(default_factory=list)

    scanned_files: list[dict] = field(default_factory=list)
    lit_files: list[dict] = field(default_factory=list)
    lit_code_context: str = ""

    aistudio_files: list[dict] = field(default_factory=list)
    aistudio_code_context: str = ""

    llm_context: str = ""

    notes: list[str] = field(default_factory=list)
    llm_calls: list[dict] = field(default_factory=list)

    answer: str = ""
    final_codex_prompt: str = ""
