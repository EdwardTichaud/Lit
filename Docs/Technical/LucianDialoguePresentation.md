# Présentation des dialogues de Lucian

## Réglages auteur

Le profil `Assets/Animations/Autres/Dialogue/Lucian/Lucian_DialoguePresentation.asset` est affecté aux PlayerSettings de Lucian. Les autres personnages ne sont pas configurés par ce lot. Le contrat est réutilisable : chaque personnage pourra posséder son profil, ses clips et ses expressions derrière les mêmes noms d'états.

Une VoiceLineData contient une direction de jeu : intention, intensité et premier geste facultatif. Une étape Dialogue de StorySequence peut surcharger cette direction. Le menu du premier geste n'affiche que les gestes corporels disponibles. Sans indication, Lucian adopte un jeu explicatif et varie ses gestes sans répétition immédiate lorsqu'une alternative existe. Le système ne déduit pas les émotions du texte.

La parole vocale suit la durée réellement jouée par la source audio. Une ligne sans voix joue un geste pendant 2,5 secondes par défaut, puis passe à une attente attentive tant que le texte est affiché. Les expressions affectent seulement sourcils, yeux, nez et sourire ; elles n'ouvrent pas la mâchoire et ne remplacent pas le système de bouche fermée. Aucun lip-sync n'est ajouté.

## Animation et priorités

- `Dialogue Body` : pose complète uniquement lorsque le personnage est arrêté, possède son verrou moteur et que le clip a passé les mesures de stabilité.
- `Dialogue Upper Body` : dialogue mobile et assis ; conserve bassin, jambes et pieds de la couche de base.
- `Dialogue Free Arm` : lorsque la torche est équipée, anime uniquement le bras gauche. Le bras droit conserve la torche.

Les conversations ordinaires verrouillant le joueur utilisent l'arrêt progressif UCC. Les ouvertures déjà mises en scène avec fondu, assise, Timeline ou étape d'arrêt explicite conservent leur contrat. La direction vers l'interlocuteur passe par le bridge et le regard existants. Aucune translation Animator ni modification d'Animator.speed n'est ajoutée.

Combat, mort, saut, traversée et cinématique ont priorité. Les sessions sont identifiées par génération : la fermeture d'une ancienne réplique ne peut pas fermer la suivante. Les désactivations, changements de scène/personnage et pertes d'autorité libèrent les commandes possédées par le dialogue. La fin normale conserve le verrou moteur jusqu'à la disparition d'une pose complète. Les répliques spontanées ne verrouillent pas le déplacement.

Le propriétaire réseau choisit le geste ; les observateurs reçoivent état, génération, révision, phase temporelle, intensité et interlocuteur. Ils ne tirent pas un geste au hasard indépendamment.

## Provenance des clips

Les copies dédiées et leurs masques sont dans `Assets/Animations/Autres/Dialogue/Lucian`. Les originaux fournisseur et les animations de locomotion, saut, esquive et torche restent intacts. La copie Humanoid du modèle de mouvements utilise l'avatar réel de Lucian. Les conversions retirent uniquement la trajectoire physique mesurée, puis isolent les canaux des couches supérieures ; elles excluent événements gameplay et courbes faciales.

`HD Talk_M` et `_Talk_M` sont conservés dans le catalogue mais indisponibles pour les gestes corporels : leurs bras restent presque immobiles. Les gestes disponibles viennent de `Talk Serious`, `Sit Talk` et des boucles de conversation debout F/L/R. Les points d'entrée privilégient leurs passages effectivement animés. Les anciennes poses Await/Focus ne sont pas utilisées comme boucles d'attente : l'attente réutilise la respiration Idle existante.

Les gestes debout dont les appuis dépassent la tolérance de 3 cm sont limités au haut du corps. Le système ne modifie pas les pieds pour rendre ces clips artificiellement admissibles en plein corps.

## Outils et validation

- `Lit > Animation > Install Lucian Dialogue Presentation` : installation ciblée, conservation des réglages existants et des GUID des copies.
- `Lit > Animation > Validate Lucian Dialogue Presentation` : contrat des profils, sources, masques, états et prefabs ; également exécuté avant build.
- `Lit > Animation > Capture Lucian Dialogue Poses` : captures de contrôle dans le laboratoire, sans enregistrer les scènes utilisateur.
- Tests Editor : `DialoguePresentationTests` et `DialoguePresentationNativeRigTests`, puis régressions `StorySequenceAuthoringTests` et `CycleAutoDialogueTests`.

Les mesures et sorties temporaires se trouvent dans `Library/DialoguePresentation`. Une exécution de tests contenant zéro test n'est pas une validation.

### État de livraison

Installation effectuée par Unity et audit de trajectoire exécuté sur l'avatar réel. Les dernières sources runtime et Editor sont compilées dans le projet principal. Une seconde installation a conservé à l'identique les 87 fichiers suivis pour ce contrôle : résultat dans `Docs/Validation/2026-11-19/LucianDialogueIdempotence.json`.

**15 tests natifs Unity passent** dans un projet de validation isolé utilisant le runtime réellement compilé, l'avatar, les clips et les masques réels de Lucian. Ils couvrent sélection, intentions, posture, exclusions, anciennes sessions, neutralité de la bouche, révisions réseau, isolation des pieds/bassin debout et assis, conservation du bras droit, mouvement effectif de la main libre, fondus et retour à l'attente. Les positions comparées des jambes et du bras protégé sont identiques dans ces essais. Ce montage ne simule pas UCC, les collisions, les caméras ni une connexion host/client.

Résultats conservés dans `Docs/Validation/2026-11-19/LucianDialogueNativeTests.xml`. Après reprise de l'Editor, **56 tests passent dans le projet principal**, dont les tests de dialogue et les régressions StorySequence et CycleAutoDialogue : `Docs/Validation/2026-11-19/LucianDialogueProjectTests.xml`.

Les captures de poses ont été régénérées avec le vrai modèle, son avatar et ses matériaux, puis examinées. Elles sont conservées dans `Docs/Validation/2026-11-19/LucianDialoguePoses` : Idle, parole, question, assise, masque torche et attente. Le bake des meshes pour la capture évite les artefacts de cache de skinning de la première série. La simulation de cape est désactivée pour ces images fixes ; la capture du masque torche n'est pas une validation de l'allumage des VFX. Ces images ne valident pas les raccords en mouvement ni le ressenti en jeu.

La validation complète en Play Mode et host/client reste à effectuer. Contrôler conversation depuis la course, parole/écoute/attente, interruption, saut, mort, cinématique, assise et torche ; vérifier le retour des inputs maintenus, la neutralité de la bouche, les appuis et l'absence de commande moteur résiduelle. Ne pas considérer la validation visuelle en jeu acquise avant ce parcours.
