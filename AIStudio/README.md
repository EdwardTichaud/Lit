# AIStudio

AIStudio est l’orchestrateur de missions techniques du projet **Lit**.

Il ne modifie jamais directement le projet Unity Lit en mode patch local.
Pour Lit, il prépare un contexte ciblé et un résultat adapté au workflow demandé.
Pour son propre projet AIStudio, il peut proposer puis appliquer des patches contrôlés.

Le but est de réduire :

* le temps d'analyse ;
* le nombre de tokens envoyés au LLM ;
* les appels API inutiles ;
* les patches non fiables produits depuis mémoire.

-------------------------------------------------------

# Utilisation

```bash
source .venv/Scripts/activate
python -m app.chat
```

--------------------------------------------------------

# Installation

## 1. Cloner le dépôt

```bash
git clone ...
cd AIStudio
```

## 2. Créer un environnement virtuel

Windows (Git Bash) :

```bash
python -m venv .venv
source .venv/Scripts/activate
```

PowerShell :

```powershell
python -m venv .venv
.venv\Scripts\Activate.ps1
```

Vérifier que l'invite affiche :

```text
(.venv)
```

Toutes les commandes suivantes doivent être exécutées dans cet environnement.

---

## 3. Installer les dépendances Python

```bash
pip install -r requirements.txt
```

Vérifier ensuite :

```bash
python -m pip show langchain-openai
```

Le paquet doit être trouvé.

---

## 4. Installer ripgrep

Sous Windows :

```powershell
winget install BurntSushi.ripgrep.MSVC
```

Fermer puis rouvrir le terminal.

Vérifier :

```bash
rg --version
```

Cette commande doit afficher la version de ripgrep.

---

## 5. Configurer l'API OpenAI

Créer un fichier `.env` à la racine d'AIStudio :

```env
OPENAI_API_KEY=sk-...
AI_STUDIO_MODEL=gpt-5.4
```

---

# Vérification de l'installation

Depuis le dossier AIStudio :

```bash
source .venv/Scripts/activate
python -m app.chat
```

AIStudio propose alors un choix de workflow :

```text
AIStudio

Choisis un mode :

1. Préparer un prompt pour Codex
2. Coder avec AIStudio
```

---

# Nouvelle architecture logique

Une mission suit toujours ce pipeline :

```text
Mission utilisateur
│
▼
Mission Manager
│
▼
Mission Planner
│
▼
Collecte automatique du contexte
│
├── Documentation Selector
├── Unity Project Scanner
├── AIStudio Code Scanner
└── Context Builder
│
▼
Décision
│
├── Prompt Workflow
└── Code Workflow
│
▼
LLM
│
▼
Résultat
```

## Invariants

- les deux workflows partagent exactement la même collecte ;
- le LLM n’est jamais appelé avant la fin de la collecte ;
- le contexte construit est la seule entrée du LLM ;
- un patch AIStudio ne doit jamais être inventé depuis mémoire ;
- un fichier existant doit être patché uniquement si son contenu complet a été chargé.

---

# Workflows

## 1. Prompt Workflow

Produit uniquement :

- analyse ;
- risques ;
- plan ;
- prompt Codex.

Il ne produit aucun patch.

## 2. Code Workflow

Produit le flux suivant :

1. analyse ;
2. plan ;
3. attente de validation utilisateur ;
4. génération du patch ;
5. attente de confirmation ;
6. application automatique.

Le patch ne doit jamais être généré avant validation explicite du plan.

---

# Règles de patch AIStudio

Pour chaque fichier existant :

1. utiliser le contenu chargé par le scanner AIStudio ;
2. modifier ce contenu ;
3. produire le fichier complet ;
4. préserver toutes les parties non modifiées.

Si le contenu complet d’un fichier n’est pas dans le contexte, AIStudio doit répondre :

```text
Je ne peux pas produire un patch sûr tant que le fichier complet n'est pas chargé.
```

AIStudio ne doit jamais inventer le contenu d’un fichier existant.

---

# Première utilisation

## Workflow prompt

Exemple :

```text
AIStudio > Je veux améliorer la montée et descente d'échelle.
AIStudio > Le personnage doit utiliser les bonnes animations.
AIStudio > Le personnage doit être orienté correctement.
AIStudio > GO
```

AIStudio :

1. collecte le contexte ;
2. analyse la demande ;
3. sélectionne la documentation pertinente ;
4. scanne les scripts Unity concernés ;
5. identifie les risques ;
6. génère un prompt Codex ciblé.

## Workflow code AIStudio

Exemple :

```text
AIStudio > Refonds le pipeline de mission.
AIStudio > GO
```

AIStudio :

1. collecte le contexte partagé ;
2. analyse ;
3. propose un plan ;
4. peut attendre `EXTEND` si le contexte Lit doit être élargi ;
5. attend `VALIDATE` ;
6. génère un patch complet ;
7. attend `APPLY` pour écrire les fichiers.

---

# Workflow recommandé

## Pour Lit

```text
Toi
 ↓
AIStudio
 ↓
Collecte de contexte
 ↓
Analyse
 ↓
Prompt Codex
 ↓
Codex
 ↓
Modification du projet Unity Lit
```

## Pour AIStudio

```text
Toi
 ↓
AIStudio
 ↓
Collecte de contexte
 ↓
Analyse + plan
 ↓
VALIDATE
 ↓
Patch complet
 ↓
APPLY
 ↓
Modification automatique d’AIStudio
```

---

# Architecture du dépôt

```text
app/

agents/
    architect.py

core/
    config.py
    context_builder.py
    document_selector.py
    documentation.py
    documentation_indexer.py
    llm_tracking.py
    mission.py
    mission_brief.py
    mission_pipeline.py
    models.py
    project_scanner.py
    prompts.py

chat.py

docs/
logs/
prompts/
```

---

# Commandes utiles

Lancer AIStudio :

```bash
python -m app.chat
```

Réinitialiser la mission :

```text
RESET
```

Lancer l’analyse ou l’étape courante :

```text
GO
```

Élargir le contexte Lit chargé et relancer un plan :

```text
EXTEND
```

Valider le plan en mode code AIStudio :

```text
VALIDATE
```

Appliquer le dernier patch proposé :

```text
APPLY
```

Quitter :

```text
QUIT
```

---

# Dépannage

## AIStudio indique :

```text
Dépendance manquante : langchain-openai
```

Solution :

```bash
source .venv/Scripts/activate
python -m pip install -r requirements.txt
```

---

## AIStudio indique :

```text
ripgrep (rg) est introuvable
```

Vérifier :

```bash
rg --version
```

Si la commande échoue :

```powershell
winget install BurntSushi.ripgrep.MSVC
```

Puis redémarrer le terminal.

---

## L'API OpenAI ne fonctionne pas

Vérifier :

* que `.env` existe ;
* que `OPENAI_API_KEY` est valide ;
* que le projet OpenAI possède du crédit ;
* que l'environnement virtuel est activé.

---

# Limites connues de ce dépôt

- AIStudio peut patcher uniquement ses propres fichiers autorisés.
- Les fichiers Unity Lit ne doivent pas être patchés directement en mode AIStudio.
- La suppression automatique de fichiers Python obsolètes n’est sûre que si leur rôle réel et leur contenu complet ont été chargés dans le contexte.

---

# Philosophie

AIStudio n'est pas un IDE.

AIStudio n'est pas un générateur aveugle de code.

AIStudio est un **orchestrateur de mission technique**.

Il fait le maximum du travail localement :

- planification ;
- sélection documentaire ;
- recherche ciblée ;
- lecture des fichiers pertinents ;
- construction d’un contexte compact ;
- contrôle du workflow.

Ainsi, le LLM intervient tard, sur un contexte ciblé, avec un coût réduit et un risque de patch plus faible.