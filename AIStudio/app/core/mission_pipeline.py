from __future__ import annotations

from app.agents.architect import ArchitectAgent
from app.core.app_workflow import AppWorkflow
from app.core.context_builder import build_context
from app.core.documentation_indexer import write_documentation_index
from app.core.mission import MissionContext


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

Tu dois travailler uniquement sur AIStudio si c'est sûr.

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

Ne produis aucun bloc fichier à cette étape.
"""
        return f"""
Mode : AISTUDIO_CODE

Mission utilisateur :

{mission.query}

Le plan a été validé par l'utilisateur.

Tu dois maintenant :
1. analyser brièvement si nécessaire ;
2. produire le patch complet uniquement pour AIStudio ;
3. utiliser uniquement les fichiers autorisés ;
4. préserver toutes les parties non modifiées ;
5. ne jamais inventer le contenu d'un fichier existant.

Si le contenu complet d'un fichier existant n'est pas disponible, réponds exactement :
"Je ne peux pas produire un patch sûr tant que le fichier complet n'est pas chargé."

Produis uniquement des fichiers complets au format :

=== FILE: chemin/du/fichier ===
<<<FILE_CONTENT
contenu complet du fichier