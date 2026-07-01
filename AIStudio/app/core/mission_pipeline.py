from __future__ import annotations

from app.agents.architect import ArchitectAgent
from app.core.app_workflow import AppWorkflow
from app.core.context_builder import build_context
from app.core.mission import MissionContext
from app.core.project_scanner import (
    build_lit_code_context,
    load_lit_code_files,
    scan_project,
)


def build_prompt_for_workflow(
    mission: MissionContext,
    *,
    plan_validated: bool = False,
) -> str:
    if mission.workflow == AppWorkflow.CODEX_PROMPT:
        return f"""
Prépare un résultat de workflow prompt.

Produit uniquement :
- analyse
- risques
- plan
- prompt Codex

Ne modifie aucun fichier.

Mission utilisateur :

{mission.query}
"""

    if mission.workflow == AppWorkflow.AISTUDIO_CODE:
        if not plan_validated:
            return f"""
Mode : AISTUDIO_CODE

Mission utilisateur :

{mission.query}

Tu dois préparer une modification directe du projet Unity Lit si c'est sûr.

Pipeline obligatoire :
1. analyse
2. plan
3. attente de validation utilisateur

Tu ne dois pas générer de patch à cette étape.

Rappels :
- utilise uniquement le contexte fourni ;
- le LLM ne doit agir qu'après la collecte complète du contexte ;
- si un fichier existant n'est pas chargé en entier, ne l'invente jamais ;
- si un patch sûr est impossible, explique-le.
- les seuls chemins patchables sont Assets/, Packages/ et ProjectSettings/.

Ne produis aucun bloc fichier à cette étape.
"""

        return f"""
Mode : AISTUDIO_CODE

Mission utilisateur :

{mission.query}

Le plan a été validé par l'utilisateur.

Tu dois maintenant :
1. analyser brièvement si nécessaire ;
2. produire le patch complet uniquement pour le projet Lit ;
3. utiliser uniquement Assets/, Packages/ ou ProjectSettings/ ;
4. préserver toutes les parties non modifiées ;
5. ne jamais inventer le contenu d'un fichier existant.

Si le contenu complet d'un fichier existant n'est pas disponible, réponds exactement :
"Je ne peux pas produire un patch sûr tant que le fichier complet n'est pas chargé."

N'écris jamais de bloc fichier pour AIStudio/, app/, docs/, prompts/ ou README.md.

Produis uniquement des fichiers complets au format :

=== FILE: chemin/du/fichier ===
<<<FILE_CONTENT
contenu complet du fichier
FILE_CONTENT>>>
"""

    raise ValueError(f"Workflow inconnu : {mission.workflow}")


def run_mission(
    query: str,
    workflow: AppWorkflow,
    *,
    plan_validated: bool = False,
) -> MissionContext:
    mission = MissionContext(
        query=query,
        workflow=workflow,
        user_messages=[query],
    )

    _collect_context(mission)

    prompt = build_prompt_for_workflow(
        mission,
        plan_validated=plan_validated,
    )

    try:
        mission = ArchitectAgent().prepare(
            question=prompt,
            workflow=workflow,
            prepared_mission=mission,
        )
    except Exception as exc:
        mission.notes.append(f"LLM indisponible : {exc}")
        mission.answer = _build_local_fallback(
            mission,
            plan_validated=plan_validated,
            error=exc,
        )

    if mission.workflow == AppWorkflow.CODEX_PROMPT:
        mission.final_codex_prompt = mission.answer

    return mission


def _collect_context(mission: MissionContext) -> None:
    try:
        build_context(mission)
    except Exception as exc:
        mission.notes.append(f"Documentation indisponible : {exc}")

    try:
        mission.scanned_files = scan_project(mission.query)
    except Exception as exc:
        mission.notes.append(f"Scanner Unity indisponible : {exc}")

    if mission.workflow == AppWorkflow.AISTUDIO_CODE:
        try:
            mission.lit_files = load_lit_code_files(mission.scanned_files)
            mission.lit_code_context = build_lit_code_context(
                mission.lit_files
            )
        except Exception as exc:
            mission.notes.append(f"Chargement des fichiers Lit indisponible : {exc}")


def _build_local_fallback(
    mission: MissionContext,
    *,
    plan_validated: bool,
    error: Exception,
) -> str:
    if mission.workflow == AppWorkflow.CODEX_PROMPT:
        docs = _format_document_list(mission.loaded_documents)
        unity_files = _format_unity_file_list(mission.scanned_files)

        return f"""## 1. Compréhension de la demande

{mission.query}

## 2. Systèmes probablement concernés

À confirmer à partir des fichiers listés par le scanner.

## 3. Documentation utilisée

{docs}

## 4. Fichiers Unity probables

{unity_files}

## 5. Risques

- Le LLM n'a pas pu être appelé : {error}
- Ne modifier aucun fichier sans validation explicite.

## 6. Questions bloquantes

Aucune question bloquante identifiée automatiquement.

## 7. Approche recommandée

Limiter l'intervention aux fichiers réellement concernés, vérifier les dépendances Unity, puis tester dans l'éditeur.

## 8. Prompt Codex final

Tu travailles dans le projet Lit. Réponds à cette mission :

{mission.query}

Contrainte principale : ne modifie aucun fichier sans confirmation explicite et préserve les changements Git existants.
"""

    if not plan_validated:
        files = _format_lit_file_list(mission.lit_files)

        return f"""## Analyse

{mission.query}

## Fichiers concernés

{files}

## Risques

- Le LLM n'a pas pu être appelé : {error}
- Aucun patch ne doit être généré avant validation du plan.

## Plan

1. Vérifier les fichiers Lit listés.
2. Limiter la modification à Assets/, Packages/ ou ProjectSettings/.
3. Générer un patch complet uniquement après VALIDATE.

## Patch

Aucun patch produit à cette étape.
"""

    return f"""## Analyse

{mission.query}

## Patch

Je ne peux pas produire un patch sûr tant que le LLM est indisponible.

Erreur : {error}
"""


def _format_document_list(documents: list[dict]) -> str:
    if not documents:
        return "Aucun document chargé."

    return "\n".join(f"- {doc.get('file', doc)}" for doc in documents)


def _format_unity_file_list(files: list[dict]) -> str:
    if not files:
        return "Aucun fichier Unity identifié."

    return "\n".join(
        f"- {item.get('path', item)}"
        for item in files[:20]
    )


def _format_aistudio_file_list(files: list[dict]) -> str:
    if not files:
        return "Aucun fichier AIStudio identifié."

    return "\n".join(
        f"- {item.get('file', item)}"
        for item in files[:20]
    )


def _format_lit_file_list(files: list[dict]) -> str:
    if not files:
        return "Aucun fichier Lit chargé en entier."

    return "\n".join(
        f"- {item.get('path', item)}"
        for item in files[:20]
    )
