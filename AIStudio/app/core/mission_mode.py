from enum import Enum


class MissionMode(str, Enum):
    ANALYSE_ONLY = "ANALYSE_ONLY"
    CODEX_PROMPT = "CODEX_PROMPT"
    AISTUDIO_PATCH = "AISTUDIO_PATCH"