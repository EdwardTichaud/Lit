from __future__ import annotations

from collections import defaultdict
from pathlib import Path
import re
import shutil
import subprocess
import unicodedata

from app.core.config import LIT_ROOT
from app.core.path_safety import is_safe_path


SEARCH_GLOBS = [
    "*.cs",
    "*.asmdef",
    "*.inputactions",
    "*.controller",
    "*.anim",
    "*.asset",
    "*.json",
    "*.prefab",
    "*.shader",
    "*.txt",
    "*.uxml",
    "*.uss",
    "*.yaml",
    "*.yml",
]

SCANNED_ROOTS = (
    "Assets",
    "Packages",
    "ProjectSettings",
)

EXPLICIT_PATH_RE = re.compile(
    r"\b(?:Assets|Packages|ProjectSettings)[\\/][^\s`\"'<>()]+"
)

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


def scan_project(query: str, limit: int = 20) -> list[dict]:
    terms = extract_search_terms(query)
    explicit_paths = extract_explicit_lit_paths(query)
    results: dict[str, dict] = {}

    for path in explicit_paths:
        results[path] = _explicit_path_result(path)

    if terms:
        try:
            rg_command = _find_rg_command()
        except RuntimeError:
            if results:
                return _rank_results(results, limit)
            raise

        for root_name in SCANNED_ROOTS:
            results.update(_scan_root(root_name, terms, rg_command))

    for path in explicit_paths:
        results[path] = _explicit_path_result(path)

    return _rank_results(results, limit)


def _rank_results(results: dict[str, dict], limit: int) -> list[dict]:
    cleaned_results = [_public_file_score(item) for item in results.values()]

    ranked = sorted(
        cleaned_results,
        key=lambda item: (item["score"], item["path"]),
        reverse=True,
    )

    return ranked[:limit]


def _explicit_path_result(path: str) -> dict:
    return {
        "path": path,
        "score": 10_000,
        "matches": [
            {
                "line": 0,
                "text": "Chemin cité explicitement dans la demande.",
            }
        ],
        "_base_scored": True,
        "_matched_terms": set(),
    }


def extract_explicit_lit_paths(text: str) -> list[str]:
    paths: list[str] = []
    seen: set[str] = set()

    for match in EXPLICIT_PATH_RE.finditer(text):
        path = match.group(0).replace("\\", "/").strip()
        path = path.rstrip(".,;:]}")

        if not is_safe_path(path):
            continue

        if path in seen:
            continue

        seen.add(path)
        paths.append(path)

    return paths


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
            encoding="utf-8",
            errors="replace",
            timeout=20,
            check=False,
        )
    except (OSError, subprocess.SubprocessError):
        return {}

    if completed is None:
        return {}

    stdout = completed.stdout or ""
    stderr = completed.stderr or ""

    if completed.returncode not in {0, 1}:
        return {}

    scored: dict[str, dict] = defaultdict(_new_file_score)

    for raw_line in stdout.splitlines():
        parsed = _parse_rg_line(raw_line)

        if not parsed:
            continue

        path, line_number, content = parsed
        path = path.replace("\\", "/")
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
