import sys

from app.core.llm_tracking import print_llm_diagnostics
from app.core.mission_pipeline import run_mission


WELCOME = """
AIStudio

Générateur de prompt Codex pour Lit.

Écris la mission en une ou plusieurs notes, puis tape GO.
"""


def configure_console() -> None:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except AttributeError:
            pass


def build_request(history: list[str]) -> str:
    return "\n".join(history)


def print_mission_result(mission) -> None:
    print("\n========== DOCUMENTS UTILISÉS ==========\n")

    if not mission.loaded_documents:
        print("Aucun document chargé.")
    else:
        for doc in mission.loaded_documents:
            print(doc.get("file", doc))

    if getattr(mission, "scanned_files", None):
        print("\n========== FICHIERS UNITY PROBABLES ==========\n")
        for file in mission.scanned_files[:20]:
            print(format_file_summary(file, "path"))

    print_llm_diagnostics(mission)

    print("\n========== PROMPT CODEX ==========\n")
    print(mission.final_codex_prompt or mission.answer)


def format_file_summary(item, key: str) -> str:
    if not isinstance(item, dict):
        return str(item)

    path = item.get(key) or item.get("file") or item.get("path") or item
    score = item.get("score")

    if score is None:
        return str(path)

    return f"{path} (score {score})"


def main() -> None:
    configure_console()
    print(WELCOME)

    history: list[str] = []

    print("\nCommandes :")
    print("GO    -> générer le prompt Codex")
    print("RESET -> vider la mission")
    print("QUIT  -> sortir")

    while True:
        try:
            user_input = input("\nAIStudio > ").strip()
        except EOFError:
            print("\nFermeture.")
            break

        if not user_input:
            continue

        command = user_input.upper()

        if command in {"QUIT", "EXIT"}:
            print("Fermeture.")
            break

        if command == "RESET":
            history.clear()
            print("Mission réinitialisée.")
            continue

        if command == "GO":
            if not history:
                print("Aucune mission en cours.")
                continue

            mission = run_mission(build_request(history))
            print_mission_result(mission)
            continue

        history.append(user_input)
        print(f"Note ajoutée ({len(history)}).")


def run_chat() -> None:
    main()


if __name__ == "__main__":
    main()
