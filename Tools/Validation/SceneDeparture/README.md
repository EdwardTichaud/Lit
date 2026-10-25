# Refus d'archivage avant un portail

`Editor/GameFlowDepartureFailureTests.cs` est une fixture EditMode native qui
exerce les deux vraies coroutines de départ de `GameFlowService`. Un provider
persistant lève volontairement une exception : le vrai `WorldStateManager`
refuse alors l'export. Le test exige le maintien de la scène, de son contexte
de présentation et de ses propriétaires, la disparition de l'overlay, la
restitution de la caméra et la libération du blocage des clics.

La fixture vit dans une scène Preview propre au test. Elle ne sauvegarde rien,
ne ferme aucune scène utilisateur et restaure les singletons empruntés.
L'état opaque de l'overlay et son snapshot de caméra sont injectés : ce test
ne prétend pas valider un fondu rendu, la physique ou le rebuild NavMesh réel.
Le provider doit avoir été appelé une seule fois ; les erreurs attendues sont
identifiées précisément, sans masquer les autres logs.

## Exécution isolée

Préparer d'abord la copie code-only avec
`Tools/Validation/CycleNetworkSmoke/Run.ps1 -RecoveryTestsOnly`. Ce lanceur
refuse les projets non marqués, copie les sources runtime et compile déjà les
outils Editor communs. Il ne s'exécute pas dans l'éditeur utilisateur.

Dans **cette copie marquée uniquement** (`Library/DistrictCycleRuntimeProject`),
copier ensuite cette fixture dans `Assets/Editor/GameFlowDepartureFailureTests.cs`
et le fichier de production `Assets/Editor/Validation/BootstrapDistrictPlayProbe.cs`
dans `Assets/Editor/BootstrapDistrictPlayProbe.cs`. Ne créer aucun fichier `.request`.
Lancer Unity 6000.6.4f1 sur la copie avec :

```text
-batchmode -nographics -runTests -testPlatform EditMode
-testFilter GameFlowDepartureFailureTests
-testResults <chemin absolu du rapport XML>
-logFile <chemin absolu du journal>
```

Attendre la fin du processus créé avant de lire le résultat ou de réutiliser
la copie. Un code de sortie de compilation réussi seul ne prouve pas que les
deux cas ont été exécutés : inspecter `total`, `passed`, `failed` et leurs noms.
Comparer les SHA256 de `GameFlowService.cs` et du probe avec les sources de
production ; ne pas conclure à partir d'une ancienne assembly.

## Vérification dans les vraies scènes

L'outil Editor de production expose :
`Lit > Validation > Play rejected portal departures (isolated save)`.
Le mode fichier `Library/StartupDistrictValidation/PlayProbe.request` contient
exactement `recovery`. Il faut importer les scripts au préalable ; le probe
refuse de tester un runtime qui n'expose pas le correctif.

Cette recette reprend Bootstrap, le vrai menu Solo et le portail de Maison.
Elle ajoute un seul objet possédant une identité volontairement non exportable,
sans changer les objets de jeu. Chaque refus doit conserver le même avatar,
restituer caméra/overlay/input et restaurer le NavMesh de la zone. Après retrait
de la seule fixture, le vrai départ est retenté. Les deux directions doivent
réussir avant le retour au menu. Les erreurs d'export ne sont attendues que
pour l'identité unique de cet essai, aux deux étapes prévues.

Les données vont exclusivement dans le dossier de sauvegarde de l'essai sous
`Library/StartupDistrictValidation/PlayRuns`. Bootstrap est une copie de travail,
les scènes d'origine ne sont jamais enregistrées. Le probe refuse de démarrer
en présence de scènes utilisateur modifiées. Les interactions du menu et
l'approche du portail restent automatisées : ce n'est pas une preuve de saisie
manuelle ni de parcours complet des cycles.

Les refus après déchargement de la scène précédente, les échecs de restauration
et les opérations natives bloquées ne sont pas couverts par cette fixture.

## Validation Editor du panneau QTE

`Editor/QTEPanelValidationTests.cs` vérifie qu'une validation sans changement
ne marque plus la scène, tout en conservant le remplissage et le marquage des
vrais défauts manquants. Cette fixture refuse de commencer sans le marqueur
du projet jetable : elle utilise exclusivement sa propre scène de test enregistrée.
Elle ne doit pas être lancée dans le projet utilisateur.

Copier aussi ce fichier dans `Assets/Editor` de la copie et filtrer sur
`^(GameFlowDepartureFailureTests|QTEPanelValidationTests)` pour les quatre cas.
Le runtime `QTEPanelController.cs` copié doit être la version de production
courante, comme `GameFlowService.cs`.

Le probe de production accepte également une demande `RefreshOnly.request`
dans son dossier Library : elle réalise seulement un import hors Play Mode,
sans sauvegarder les scènes ni lancer de test. Elle permet d'importer les
correctifs suivants sans devoir automatiser le menu de l'éditeur. Les garde-fous
sur les scènes modifiées restent appliqués avant toute recette Play.
