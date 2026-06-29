from app.core.llm_tracking import print_llm_diagnostics
from app.core.mission_brief import write_mission_brief


INTRO = "AIStudio -- Preparateur de mission Codex"
PROMPT = "AIStudio > "


def print_help() -> None:
    print("Commandes : GO pour analyser, RESET pour vider, QUIT pour sortir.")


def run_chat() -> None:
    user_messages: list[str] = []

    print(INTRO)
    print_help()

    while True:
        try:
            raw_message = input(PROMPT)
        except (EOFError, KeyboardInterrupt):
            print("")
            return

        message = raw_message.strip()

        if not message:
            continue

        command = message.upper()

        if command in {"QUIT", "EXIT"}:
            return

        if command == "RESET":
            user_messages.clear()
            print("Demande reinitialisee.")
            continue

        if command == "GO":
            if not user_messages:
                print("Aucune demande a analyser.")
                continue

            try:
                from app.agents.architect import ArchitectAgent

                agent = ArchitectAgent()
                mission = agent.prepare(user_messages)
            except Exception as exc:
                print(f"Analyse interrompue : {exc}")
                continue

            print("\n========== DOCUMENTS ==========\n")
            for doc in mission.loaded_documents:
                print(doc["file"])

            print("\n========== FICHIERS UNITY PROBABLES ==========\n")
            for file_info in mission.scanned_files:
                print(f"{file_info['score']:>4}  {file_info['path']}")

            print_llm_diagnostics(mission)

            brief_paths = write_mission_brief(mission)
            print("\n========== BRIEF ==========\n")
            print(f"Brief courant : {brief_paths['latest']}")
            print(f"Archive : {brief_paths['archive']}")

            print("\n========== PROMPT CODEX ==========\n")
            print(mission.final_codex_prompt or mission.answer)
            continue

        user_messages.append(message)
        print(f"Note ajoutee ({len(user_messages)}).")


def main() -> None:
    run_chat()


if __name__ == "__main__":
    main()
