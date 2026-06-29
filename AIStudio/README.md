# AIStudio

AIStudio est le préparateur de mission technique du projet **Lit**.

Il ne modifie jamais directement le projet Unity.

Son rôle est de transformer une demande utilisateur en une mission technique claire, documentée et optimisée que Codex pourra exécuter efficacement.

Le but est de réduire :

* le temps d'analyse ;
* le nombre de tokens envoyés au LLM ;
* les appels API inutiles.

-------------------------------------------------------

# Utilisation

source .venv/Scripts/activate
python -m app.chat

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

L'écran suivant doit apparaître :

```text
AIStudio -- Préparateur de mission Codex

Commandes :
GO
RESET
QUIT
```

Si c'est le cas, l'installation est terminée.

---

# Première utilisation

Exemple :

```text
AIStudio > Je veux améliorer la montée et descente d'échelle.

AIStudio > Le personnage doit utiliser les bonnes animations.

AIStudio > Le personnage doit être orienté correctement.

AIStudio > GO
```

AIStudio :

1. analyse la demande ;
2. recherche la documentation pertinente ;
3. scanne les scripts Unity concernés ;
4. identifie les risques ;
5. pose des questions si nécessaire ;
6. génère un prompt optimisé pour Codex.

---

# Workflow recommandé

```text
Toi
 ↓
AIStudio
 ↓
Analyse
 ↓
Documentation
 ↓
Recherche scripts Unity
 ↓
Questions éventuelles
 ↓
Prompt Codex
 ↓
Codex
 ↓
Modification du projet
```

AIStudio prépare.

Codex développe.

---

# Architecture

```
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

Lancer l'analyse :

```text
GO
```

Quitter :

```text
QUIT
```

---

# Dépannage

## AIStudio indique :

```
Dépendance manquante : langchain-openai
```

Solution :

```bash
source .venv/Scripts/activate
python -m pip install -r requirements.txt
```

---

## AIStudio indique :

```
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

# Philosophie

AIStudio n'est pas un IDE.

AIStudio n'est pas un générateur de code.

AIStudio est un **préparateur de mission**.

Il fait le maximum du travail localement (documentation, recherche, sélection, analyse) afin que Codex reçoive un contexte ciblé et produise un patch de qualité avec un coût API minimal.
