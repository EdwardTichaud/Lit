from app.core.mission_mode import MissionMode


def classify_mission(text: str) -> MissionMode:
    lowered = text.lower()

    patch_keywords = [
        "corrige le readme",
        "modifie le readme",
        "corrige la doc",
        "modifie la doc",
        "corrige aistudio",
        "modifie aistudio",
        "ajoute un log",
    ]

    analyse_keywords = [
        "analyse",
        "explique",
        "pourquoi",
        "diagnostic",
        "diagnostique",
        "comprends",
        "comprendre",
    ]

    codex_keywords = [
        "prépare",
        "prepare",
        "codex",
        "implémente",
        "implemente",
        "corrige le système",
        "modifie le système",
        "améliore",
        "ameliore",
        "ajoute un système",
    ]

    if any(keyword in lowered for keyword in patch_keywords):
        return MissionMode.AISTUDIO_PATCH

    if any(keyword in lowered for keyword in codex_keywords):
        return MissionMode.CODEX_PROMPT

    if any(keyword in lowered for keyword in analyse_keywords):
        return MissionMode.ANALYSE_ONLY

    return MissionMode.CODEX_PROMPT