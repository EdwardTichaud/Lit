# AIStudio

AIStudio sert uniquement à préparer un prompt Codex pour le projet Unity **Lit**.

Il collecte un contexte local ciblé, sélectionne la documentation pertinente,
liste les fichiers Unity probables, puis demande au LLM de produire un prompt
Codex exploitable. Il ne modifie aucun fichier du projet, ne génère pas de patch
et n'applique jamais de changement automatiquement.

# Utilisation

```bash
source .venv/Scripts/activate
python -m app.chat
```

Dans la console, écris la mission en une ou plusieurs notes, puis tape `GO`.

```text
AIStudio > Je veux améliorer la montée et descente d'échelle.
AIStudio > Le personnage doit utiliser les bonnes animations.
AIStudio > GO
```

Commandes disponibles :

```text
GO    -> générer le prompt Codex
RESET -> vider la mission
QUIT  -> sortir
```

# Installation

## 1. Cloner le dépôt

```bash
git clone ...
cd AIStudio
```

## 2. Créer un environnement virtuel

Windows Git Bash :

```bash
python -m venv .venv
source .venv/Scripts/activate
```

PowerShell :

```powershell
python -m venv .venv
.venv\Scripts\Activate.ps1
```

## 3. Installer les dépendances Python

```bash
pip install -r requirements.txt
```

## 4. Installer ripgrep

Sous Windows :

```powershell
winget install BurntSushi.ripgrep.MSVC
```

Vérifier :

```bash
rg --version
```

## 5. Configurer l'API OpenAI

Créer un fichier `.env` à la racine d'AIStudio :

```env
OPENAI_API_KEY=sk-...
AI_STUDIO_MODEL=gpt-5.4
```

# Pipeline

```text
Mission utilisateur
│
▼
Collecte automatique du contexte
│
├── sélection documentaire
├── scan du projet Unity
└── construction du contexte LLM
│
▼
LLM
│
▼
Prompt Codex
```

Le résultat attendu contient :

- analyse ;
- risques ;
- plan ;
- tests à prévoir ;
- prompt Codex final.

# Dépannage

## Dépendance manquante : langchain-openai

```bash
source .venv/Scripts/activate
python -m pip install -r requirements.txt
```

## ripgrep est introuvable

```bash
rg --version
```

Si la commande échoue :

```powershell
winget install BurntSushi.ripgrep.MSVC
```

Puis redémarrer le terminal.
