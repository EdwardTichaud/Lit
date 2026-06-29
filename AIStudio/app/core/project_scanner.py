from __future__ import annotations

from collections import defaultdict
from pathlib import Path
import re
import shutil
import subprocess
import unicodedata

from app.core.config import LIT_ROOT


SEARCH_GLOBS = [
    "*.cs",
    "*.asmdef",
    "*.inputactions",
    "*.controller",
    "*.anim",
]

STOPWORDS = {
    "avec",
    "aussi",
    "dans",
    "des",
    "doit",
    "doivent",
    "elle",
    "faire",
    "fonctionner",
    "les",
    "leur",
    "mon",
    "nous",
    "pour",
    "que",
    "qui",
    "sur",
    "systeme",
    "tous",
    "une",
    "utilise",
    "utiliser",
    "veux",
}

ALIASES = {
    "animation": ["animator", "animation", "trigger"],
    "animations": ["animator", "animation", "trigger"],
    "camera": ["camera", "CameraController", "LocalPlayerContext"],
    "combat": ["combat", "CombatSession", "CombatTurn"],
    "descente": ["ladder", "LadderController", "climb"],
    "echelle": ["ladder", "LadderController", "climb", "climbing"],
    "echelles": ["ladder", "LadderController", "climb", "climbing"],
    "grimper": ["ladder", "LadderController", "climb", "climbing"],
    "montee": ["ladder", "LadderController", "climb"],
    "input": ["input", "LocalPlayerInput", "LocalInputRouter", "PlayerInputs"],
    "interaction": ["interaction", "interactable", "ICharacterDetectedInteractable"],
    "interactions": ["interaction", "interactable", "ICharacterDetectedInteractable"],
    "multijoueur": ["netcode", "NetworkBehaviour", "ServerRpc", "ClientRpc", "NetworkVariable"],
    "network": ["netcode", "NetworkBehaviour", "ServerRpc", "ClientRpc", "NetworkVariable"],
    "netcode": ["netcode", "NetworkBehaviour", "ServerRpc", "ClientRpc", "NetworkVariable"],
    "opsive": ["opsive", "UCC", "UltimateCharacterLocomotion"],
    "sauvegarde": ["save", "persistence", "PersistentNetworkObject", "WorldStateManager"],
    "ucc": ["opsive", "UCC", "UltimateCharacterLocomotion", "LitOpsiveLocomotionBridge"],
}


def normalize_text(text: str) -> str:
    normalized = unicodedata.normalize("NFKD", text)
    ascii_text = normalized.encode("ascii", "ignore").decode("ascii")
    return ascii_text.lower()


def extract_search_terms(text: str, limit: int = 24) -> list[str]:
    normalized = normalize_text(text)
    raw_terms = re.findall(r"[a-zA-Z0-9_]{3,}", normalized)

    terms: list[str] = []

    for term in raw_terms:
        if term in STOPWORDS:
            continue

        terms.append(term)
        terms.extend(ALIASES.get(term, []))

    deduped: list[str] = []
    seen = set()

    for term in terms:
        key = term.lower()
        if key in seen:
            continue

        seen.add(key)
        deduped.append(term)

    return deduped[:limit]


def scan_project(query: str, limit: int = 15, min_assets_results: int = 5) -> list[dict]:
    terms = extract_search_terms(query)

    if not terms:
        return []

    rg_command = _find_rg_command()

    results = _scan_root("Assets", terms, rg_command)

    if len(results) < min_assets_results:
        package_results = _scan_root("Packages", terms, rg_command)
        results.update(package_results)

    cleaned_results = [_public_file_score(item) for item in results.values()]

    ranked = sorted(
        cleaned_results,
        key=lambda item: (item["score"], item["path"]),
        reverse=True,
    )

    return ranked[:limit]


def _find_rg_command() -> str:
    rg_command = shutil.which("rg")

    if not rg_command:
        raise RuntimeError(
            "ripgrep (rg) est introuvable. Installe rg ou ajoute-le au PATH avant de lancer l'analyse."
        )

    return rg_command


def _scan_root(root_name: str, terms: list[str], rg_command: str) -> dict[str, dict]:
    root_path = LIT_ROOT / root_name

    if not root_path.exists():
        return {}

    command = [
        rg_command,
        "--no-heading",
        "--line-number",
        "--ignore-case",
        "--fixed-strings",
    ]

    for glob in SEARCH_GLOBS:
        command.extend(["--glob", glob])

    for term in terms:
        command.extend(["-e", term])

    command.append(root_name)

    try:
        completed = subprocess.run(
            command,
            cwd=LIT_ROOT,
            capture_output=True,
            text=True,
            timeout=20,
            check=False,
        )
    except subprocess.TimeoutExpired:
        return {}

    if completed.returncode not in {0, 1}:
        return {}

    scored: dict[str, dict] = defaultdict(_new_file_score)

    for raw_line in completed.stdout.splitlines():
        parsed = _parse_rg_line(raw_line)

        if not parsed:
            continue

        path, line_number, content = parsed
        item = scored[path]
        item["path"] = path

        _score_match(item, root_name, path, line_number, content, terms)

    return dict(scored)


def _new_file_score() -> dict:
    return {
        "path": "",
        "score": 0,
        "matches": [],
        "_base_scored": False,
        "_matched_terms": set(),
    }


def _parse_rg_line(raw_line: str) -> tuple[str, int, str] | None:
    parts = raw_line.split(":", 2)

    if len(parts) != 3:
        return None

    path, raw_line_number, content = parts

    try:
        line_number = int(raw_line_number)
    except ValueError:
        return None

    return path, line_number, content.strip()


def _score_match(
    item: dict,
    root_name: str,
    path: str,
    line_number: int,
    content: str,
    terms: list[str],
) -> None:
    normalized_path = normalize_text(path)
    normalized_content = normalize_text(content)

    if not item["_base_scored"]:
        if root_name == "Assets":
            item["score"] += 30
        elif root_name == "Packages":
            item["score"] -= 10

        suffix = Path(path).suffix.lower()

        if suffix == ".cs":
            item["score"] += 20
        elif suffix in {".controller", ".anim", ".inputactions"}:
            item["score"] += 6

        item["_base_scored"] = True

    if re.search(r"\b(class|struct|interface|enum)\b", content):
        item["score"] += 10

    for term in terms:
        normalized_term = normalize_text(term)
        path_term_key = f"path:{normalized_term}"

        if normalized_term in normalized_path and path_term_key not in item["_matched_terms"]:
            item["score"] += 12
            item["_matched_terms"].add(path_term_key)

        if normalized_term in normalized_content and normalized_term not in item["_matched_terms"]:
            item["score"] += 6
            item["_matched_terms"].add(normalized_term)

    if len(item["matches"]) < 4:
        item["matches"].append({
            "line": line_number,
            "text": content[:220],
        })


def _public_file_score(item: dict) -> dict:
    return {
        "path": item["path"],
        "score": item["score"],
        "matches": item["matches"],
    }
