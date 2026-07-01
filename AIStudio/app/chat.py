from pathlib import Path
import sys

from app.core.app_workflow import AppWorkflow
from app.core.config import LIT_ROOT
from app.core.llm_tracking import print_llm_diagnostics
from app.core.mission_pipeline import (
    CONTEXT_EXTENSION_STEP,
    DEFAULT_LIT_FILE_LIMIT,
    run_mission,
)
from app.core.patch_applier import apply_file_blocks, extract_file_blocks, is_safe_path


WELCOME = """
AIStudio

Choisis un mode :

1. Préparer un prompt pour Codex
2. Coder avec AIStudio
"""


def configure_console() -> None:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except AttributeError:
            pass


def select_workflow() -> AppWorkflow | None:
    while True:
        try:
            choice = input("Choix 1 ou 2 > ").strip()
        except EOFError:
            return None

        command = choice.upper()

        if command in {"QUIT", "EXIT"}:
            return None

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
            print(format_file_summary(file, "path"))

    if getattr(mission, "lit_files", None):
        print("\n========== FICHIERS LIT CHARGÉS ==========\n")
        for file in mission.lit_files[:20]:
            print(format_file_summary(file, "path"))

    print_llm_diagnostics(mission)

    if mission.workflow == AppWorkflow.CODEX_PROMPT:
        print("\n========== PROMPT CODEX ==========\n")
        print(mission.final_codex_prompt or mission.answer)

    elif mission.workflow == AppWorkflow.AISTUDIO_CODE:
        print("\n========== RÉPONSE AISTUDIO ==========\n")
        print(mission.answer)


def print_context_extension_hint(mission) -> None:
    if mission.workflow != AppWorkflow.AISTUDIO_CODE:
        return

    candidates = get_context_extension_candidates(mission)

    if not candidates:
        return

    print(
        "\nContexte extensible : "
        f"{count_loaded_lit_files(mission)} fichier(s) Lit chargé(s) "
        f"sur une limite de {mission.lit_file_limit}."
    )
    print(
        "Si AIStudio indique qu'un fichier manque, tape EXTEND pour autoriser "
        f"{CONTEXT_EXTENSION_STEP} fichier(s) complet(s) supplémentaire(s) "
        "et relancer l'analyse avant validation."
    )
    print("Prochains candidats probables :")

    for path in candidates[:5]:
        print(f"- {path}")


def count_loaded_lit_files(mission) -> int:
    return sum(
        1
        for item in getattr(mission, "lit_files", [])
        if isinstance(item, dict) and item.get("content_loaded")
    )


def get_context_extension_candidates(mission) -> list[str]:
    known_paths = {
        str(item.get("path", "")).replace("\\", "/").strip()
        for item in getattr(mission, "lit_files", [])
        if isinstance(item, dict)
    }

    candidates: list[str] = []

    for item in getattr(mission, "scanned_files", []):
        if not isinstance(item, dict):
            continue

        relative_path = str(item.get("path", "")).replace("\\", "/").strip()

        if not relative_path or relative_path in known_paths:
            continue

        if not is_safe_path(relative_path):
            continue

        if not (Path(LIT_ROOT) / relative_path).is_file():
            continue

        candidates.append(relative_path)

    return candidates


def format_file_summary(item, key: str) -> str:
    if not isinstance(item, dict):
        return str(item)

    path = item.get(key) or item.get("file") or item.get("path") or item
    score = item.get("score")

    if score is None:
        return str(path)

    return f"{path} (score {score})"


def try_apply_patch(mission) -> None:
    blocks = extract_file_blocks(mission.answer)

    if not blocks:
        print("\nAucun bloc de fichier applicable trouvé.")
        return

    unsafe_paths = [
        block["path"]
        for block in blocks
        if not is_safe_path(block["path"])
    ]

    if unsafe_paths:
        print("\nApplication refusée. Chemins interdits :")
        for path in unsafe_paths:
            print(f"- {path}")
        return

    unloaded_paths = find_unloaded_existing_paths(mission, blocks)

    if unloaded_paths:
        print("\nApplication refusée. Fichiers existants non chargés en entier :")
        for path in unloaded_paths:
            print(f"- {path}")
        return

    print("\nFichiers qui seront modifiés :")
    for block in blocks:
        print(f"- {block['path']}")

    try:
        confirm = input("\nAppliquer les modifications ? (oui/non) ").strip().lower()
    except EOFError:
        print("Application annulée.")
        return

    if confirm not in {"oui", "o", "yes", "y"}:
        print("Application annulée.")
        return

    try:
        written = apply_file_blocks(
            Path(LIT_ROOT),
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


def find_unloaded_existing_paths(mission, blocks: list[dict]) -> list[str]:
    loaded_paths = {
        str(item.get("path", "")).replace("\\", "/").strip()
        for item in getattr(mission, "lit_files", [])
        if item.get("content_loaded")
    }

    missing_paths: list[str] = []

    for block in blocks:
        relative_path = block["path"].replace("\\", "/").strip()
        target = Path(LIT_ROOT) / relative_path

        if target.exists() and relative_path not in loaded_paths:
            missing_paths.append(relative_path)

    return missing_paths


def main() -> None:
    configure_console()
    print(WELCOME)

    workflow = select_workflow()

    if workflow is None:
        print("Fermeture.")
        return

    print(f"\nMode choisi : {workflow.value}")

    history: list[str] = []
    last_mission = None
    plan_validated = False
    patch_ready = False
    lit_file_limit = DEFAULT_LIT_FILE_LIMIT

    print("\nCommandes :")
    print("GO       -> lancer l'étape courante")
    print("EXTEND   -> élargir le contexte Lit et relancer le plan")
    print("VALIDATE -> valider le plan en mode code AIStudio")
    print("APPLY    -> appliquer le dernier patch proposé")
    print("RESET    -> vider la mission")
    print("QUIT     -> sortir")

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
            last_mission = None
            plan_validated = False
            patch_ready = False
            lit_file_limit = DEFAULT_LIT_FILE_LIMIT
            print("Mission réinitialisée.")
            continue

        if command == "EXTEND":
            if workflow != AppWorkflow.AISTUDIO_CODE:
                print("EXTEND est disponible uniquement en mode Coder avec AIStudio.")
                continue

            if not history:
                print("Aucune mission en cours.")
                continue

            lit_file_limit += CONTEXT_EXTENSION_STEP
            plan_validated = False
            patch_ready = False
            request = build_request(history)

            print(
                "\nExtension autorisée : "
                f"chargement jusqu'à {lit_file_limit} fichier(s) Lit complet(s)."
            )
            print("Relance de l'analyse et du plan.")

            last_mission = run_mission(
                query=request,
                workflow=workflow,
                plan_validated=False,
                lit_file_limit=lit_file_limit,
            )

            print_mission_result(last_mission)
            print("\nÉtape suivante : relis le plan, puis tape VALIDATE si tu l'acceptes.")
            print_context_extension_hint(last_mission)
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
                lit_file_limit=lit_file_limit,
            )

            print_mission_result(last_mission)

            if workflow == AppWorkflow.AISTUDIO_CODE:
                if not plan_validated:
                    print("\nÉtape suivante : relis le plan, puis tape VALIDATE si tu l'acceptes.")
                    patch_ready = False
                    print_context_extension_hint(last_mission)
                else:
                    patch_ready = bool(extract_file_blocks(last_mission.answer))

                    if patch_ready:
                        print("\nSi le patch proposé te convient, tape APPLY.")
                    else:
                        print("\nAucun patch applicable n'a été produit.")
                        print_context_extension_hint(last_mission)

            continue

        if workflow == AppWorkflow.AISTUDIO_CODE and plan_validated:
            plan_validated = False
            patch_ready = False
            print("Nouvelle note ajoutée : validation du plan annulée.")

        history.append(user_input)
        print(f"Note ajoutée ({len(history)}).")


def run_chat() -> None:
    main()


if __name__ == "__main__":
    main()
