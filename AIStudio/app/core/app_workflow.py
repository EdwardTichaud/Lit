from enum import Enum


class AppWorkflow(str, Enum):
    CODEX_PROMPT = "CODEX_PROMPT"
    AISTUDIO_CODE = "AISTUDIO_CODE"