# Jeu du couteau d'Enola — prototype solo

L'entrée facultative `Entree_Fracture_Enola` est près du point d'arrivée de
`Maison_Core`. Elle charge `Fracture_Cycle_Enola` avec le flux de zones de
`GameFlowService`, le vrai joueur et la caméra de session. La borne de sortie
ramène à la Maison. Aucun prérequis, jalon du cycle d'Étienne ni changement de
strate n'est ajouté. Les trois actions sont volontairement indisponibles en
multijoueur : la séquence n'a pas de contrat d'autorité réseau.

`WorldActionInteractable` est réutilisable : collider, ancre, portée, option de
lumière et événement `onInteract` recevant le personnage local. Il passe par la
sélection commune, les `RuntimeOutlineTarget` auteurs sur les renderers et le
prompt monde commun. Il revalide cible, portée, visibilité et contrôle avant
appel. Par défaut une Flame reste requise ; seul l'opt-out explicite est exempté
(utile dans cette fracture éclairée visuellement sans Flame).

Le tabouret lance un dialogue Enola, puis un fondu au noir. Le contrôleur tient
Lucian sur `StoolPoint` avec la pose UCC de traversée, joue le state existant
`AS_Left_KnifeRaisedIdle` en boucle. Le plan est calé devant le visage à partir
de la pose au repos et vise la zone entre les mains. Il reste fixe pendant
les coups, avec une ouverture plus large et un plan de coupe rapproché.
La caméra de jeu retrouve ses réglages à la sortie. Les coups réussis utilisent
les states existants entre les doigts, puis reviennent à l'idle.
Le contrôleur du défi utilise des copies Humanoid des animations source,
retargetées sur Lucian plutôt que liées aux os du mannequin. Son contrôleur
est rétabli à la sortie, sans modification de ses contrôleurs de combat.

Huit temps de 1,1 s, six réussites nécessaires. À chaque passage du curseur,
la fenêtre jaune change de position et de largeur. Elle reste fixe pendant
ce passage, et ses limites visuelles sont celles utilisées pour la validation.
Une tentative par temps ; les
appuis anticipés consomment le temps, et les temps omis n'ajoutent aucun point.
A / Entrée valide, B / Échap abandonne ou ferme le résultat. La fin, l'abandon
et la désactivation nettoient leurs propres verrous, la caméra, le fondu, le
dialogue et l'outline. Après une réussite, `KnowledgeReveal` accorde le Savoir
`enola.knife.rhythm` une seule fois, puis le système normal sauvegarde la partie.
L'asset correspondant sous `Resources/Knowledges` est résolu par son ID même
si cette scène n'est pas chargée au démarrage. Aucun gain répétable de loot/XP.

Installation auteur : `Lit / Enola / Install knife game and access`.
Validation isolée : `Lit / Enola / Test knife game (isolated save)` ; la recette
emploie de vrais boutons sur une manette virtuelle et des placements d'approche
déclarés. Les scripts et l'UI sont regroupés dans `Assets/Gameplay/EnolaKnifeGame`.

Recette réussie : `Library/StartupDistrictValidation/PlayRuns/734a6a9699584934833d55828ab07b27/result.json`.
Cadrage des deux poignets, huit zones HUD conformes aux limites de score,
8/8 au défi, abandon sans récompense, retour, rechargement du
Savoir depuis le disque en restant à la Maison, zéro erreur runtime, éditeur
restauré. Le libellé générique de fin du banc mentionne District_1 ; le champ
`scope` et les contrôles détaillés décrivent le parcours Enola réellement joué.
La capture montre une occlusion partielle du geste par le bord de table.
Pas de qualification du multijoueur.
