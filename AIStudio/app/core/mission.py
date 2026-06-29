from dataclasses import dataclass, field


@dataclass
class MissionContext:
    query: str = ""
    user_messages: list[str] = field(default_factory=list)

    candidate_documents: list[dict] = field(default_factory=list)
    loaded_documents: list[dict] = field(default_factory=list)
    scanned_files: list[dict] = field(default_factory=list)

    llm_context: str = ""

    notes: list[str] = field(default_factory=list)
    llm_calls: list[dict] = field(default_factory=list)

    answer: str = ""
    final_codex_prompt: str = ""
