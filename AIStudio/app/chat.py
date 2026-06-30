from pathlib import Path

from app.core.app_workflow import AppWorkflow
from app.core.config import AI_STUDIO_ROOT
from app.core.llm_tracking import print_llm_diagnostics
from app.core.mission_pipeline import run_mission
from app.core.patch_applier import apply_file_blocks, extract_file_blocks


WELCOME = """
AIStudio

Choisis un mode :

1. Préparer un prompt pour Codex
2. Coder avec AIStudio
"""


def select_workflow() -> AppWorkflow:
    while True:
        choice = input("Choix 1 ou 2 > ").strip()

        if choice == "1":
            return AppWorkflow.CODEX_PROMPT

        if choice == "2":
            return AppWorkflow.AISTUDIO_CODE

        print("Choix invalide. Tape 1 ou 2.")


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
            print(file)

    if getattr(mission, "aistudio_files", None):
        print("\n========== FICHIERS AISTUDIO CHARGÉS ==========\n")
        for file in mission.aistudio_files[:20]:
            print(file)

    print_llm_diagnostics(mission)

    if mission.workflow == AppWorkflow.CODEX_PROMPT:
        print("\n========== PROMPT CODEX ==========\n")
        print(mission.final_codex_prompt or mission.answer)

    elif mission.workflow == AppWorkflow.AISTUDIO_CODE:
        print("\n========== RÉPONSE AISTUDIO ==========\n")
        print(mission.answer)


def try_apply_patch(mission) -> None:
    blocks = extract_file_blocks(mission.answer)

    if not blocks:
        print("\nAucun bloc de fichier applicable trouvé.")
        return

    print("\nFichiers qui seront modifiés :")
    for block in blocks:
        print(f"- {block['path']}")

    confirm = input("\nAppliquer les modifications ? (oui/non) ").strip().lower()

    if confirm not in {"oui", "o", "yes", "y"}:
        print("Application annulée.")
        return

    try:
        written = apply_file_blocks(
            Path(AI_STUDIO_ROOT),
            mission.answer,
        )

        print("\nFichiers modifiés :")
        for path in written:
            print(f"- {path}")

        print("\nÀ faire maintenant :")
        print("- relire les fichiers modifiés ;")
        print("- lancer python -m app.chat ;")
        print("- vérifier git diff.")

    except Exception as exc:
        print(f"Erreur pendant l'application : {exc}")


def main() -> None:
    print(WELCOME)

    workflow = select_workflow()

    print(f"\nMode choisi : {workflow.value}")

    history: list[str] = []
    last_mission = None
    plan_validated = False
    patch_ready = False

    print("\nCommandes :")
    print("GO       -> lancer l'étape courante")
    print("VALIDATE -> valider le plan en mode code AIStudio")
    print("APPLY    -> appliquer le dernier patch proposé")
    print("RESET    -> vider la mission")
    print("QUIT     -> sortir")

    while True:
        user_input = input("\nAIStudio > ").strip()

        if not user_input:
            continue

        command = user_input.upper()

        if command in {"QUIT", "EXIT"}:
            print("Fermeture.")
            break

        if command == "RESET":
            history.clear()
            last_mission = None
            plan_validated = False
            patch_ready = False
            print("Mission réinitialisée.")
            continue

        if command == "VALIDATE":
            if workflow != AppWorkflow.AISTUDIO_CODE:
                print("VALIDATE est disponible uniquement en mode Coder avec AIStudio.")
                continue

            if not history:
                print("Aucune mission en cours.")
                continue

            plan_validated = True
            patch_ready = False
            print("Plan validé. Tape GO pour générer un patch si c'est sûr.")
            continue

        if command == "APPLY":
            if last_mission is None:
                print("Aucune mission précédente.")
                continue

            if last_mission.workflow != AppWorkflow.AISTUDIO_CODE:
                print("APPLY est disponible uniquement en mode Coder avec AIStudio.")
                continue

            if not patch_ready:
                print("Aucun patch validé à appliquer. Lance d'abord GO après VALIDATE.")
                continue

            try_apply_patch(last_mission)
            continue

        if command == "GO":
            if not history:
                print("Aucune mission en cours.")
                continue

            request = build_request(history)

            last_mission = run_mission(
                query=request,
                workflow=workflow,
                plan_validated=plan_validated,
            )

            print_mission_result(last_mission)

            if workflow == AppWorkflow.AISTUDIO_CODE:
                if not plan_validated:
                    print("\nÉtape suivante : relis le plan, puis tape VALIDATE si tu l'acceptes.")
                    patch_ready = False
                else:
                    print("\nSi le patch proposé te convient, tape APPLY.")
                    patch_ready = True

            continue

        history.append(user_input)
        print(f"Note ajoutée ({len(history)}).")


if __name__ == "__main__":
    main()