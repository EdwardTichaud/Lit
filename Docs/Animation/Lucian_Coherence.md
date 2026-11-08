# Lucian : coherence des animations

## Marche stabilisée dans le District

La référence et `Cadence01` ont parcouru le même segment de
`District_1_Core` via Bootstrap, torche rangée puis équipée. Le parcours
enregistré mesure 5,116 m dans les quatre prises. Il compte 165 images de
marche stabilisée par prise, à une vitesse médiane de 1,539 m/s. Le plan
mesuré par les rayons de semelle est constant sur ces images. La variante
joue 2,34 cycles sur cette fenêtre, contre 1,60 pour la référence, et
supprime le poids d'Idle (70,94 % dans la référence).

Dans la référence, selon le pied et l'état de la torche, 33 à 57 des 165
images montrent une semelle à plus de 2 cm sous le plan. La pénétration
maximale atteint 12,78 cm. Avec la variante, aucune image de cette fenêtre
ne dépasse le seuil de 2 cm et le minimum mesuré est à −0,01 cm.
Les fenêtres de proximité du sol restent trop larges pour qualifier
un appui complet. Elles incluent le passage du pied au-dessus du sol.
La plus grande fenêtre complète de gauche passe d'environ 99 à 67 cm
de trajectoire cumulée. Cette valeur ne doit pas être lue comme 67 cm
de glissement d'un pied chargé.

Sur le candidat, le même sommet du pied gauche (0:4887) se déplace de
1,36 à 1,37 cm entre les phases 0,32 et 0,54, puis de 8,90 à 8,91 cm
entre 0,54 et 0,69, sur deux cycles avec et sans torche. Ces intervalles
ont été repérés après lecture de la trace. Ils localisent un décalage
reproductible en fin de pas sans prouver quand le pied doit cesser de
porter le poids. Comparer cette portion à la source avant de modifier
le clip ou sa cadence.

L'assise automatique a été désactivée sur l'instance de test après un
premier essai interrompu par le relevé. Un essai de la variante a aussi
perdu le signal de la manette virtuelle. La répétition complète n'a pas
eu besoin de le réinjecter. Le décor occulte Lucian dans les captures
latérales. La mesure valide un gain vertical sur le passage, et la
validation visuelle, les autres allures et les actions restent ouvertes.
Le prefab, les données et le contrôleur de la copie ont été restaurés.
Rapports, traces et captures dans
`Lucian_Coherence_Contacts/final-pose/production-reference/district/`.

### Fin d'appui gauche

Un nouvel échantillonnage Unity à 240 intervalles par cycle compare sur
l'avatar CC5 le FBX Root fournisseur, le FBX Inplace d'origine et Stride03.
La marche avant possède un décalage de cycle de −0,039576. Une fois ce
décalage appliqué, la rotation de la cheville dans le District diffère
de moins de 0,80° de la copie isolée. La bascule de l'avant-pied est
donc conservée dans ce passage, y compris avec le solveur de terrain.

Les phases d'état 0,54–0,69 correspondent à 0,500–0,650 du clip.
Le sommet suivi reste le plus bas jusqu'à environ 0,64. Une partie du
recul arrive donc avant la bascule de l'appui sur la pointe. Dans la
source Root, le sommet recule de 5,62 cm sur cet intervalle avec sa
trajectoire native. Dans la copie projetée à sa cadence actuelle, il
recule de 9,86 cm. Le même point est presque stable dans la portion
précédente du candidat (−0,80 cm). Ces mesures sont des déplacements
longitudinaux signés et les bornes proviennent du relevé initial.

Une cadence uniforme correspond à environ 1,83 m/cycle sur la première
portion pour immobiliser ce point, contre environ 2,47 m/cycle sur la
seconde. Régler toute la boucle sur la seconde valeur déplacerait le
défaut vers le milieu de l'appui. Ce passage demande une retouche
localisée de l'appui ou de son timing, à vérifier avec l'appui opposé
et les raccords. Le clip et le runtime restent identiques. Voir
`production-reference/district/walk-release-source.json`.

### Copie pilote de la fin d'appui

`Lucian_Common_Walk_Loop_ReleasePilot.anim` retouche uniquement la
translation de l'objectif Humanoid du pied gauche. La correction commence
à la phase 0,50 et compense le recul jusqu'à 0,642. Elle se relâche après
la bascule sur la pointe, puis rejoint la pose d'origine avant 0,892.
Le décalage longitudinal maximal de la cheville est de 9,59 cm.

Sur le CC5, à distance de cycle identique, le déplacement cumulé du sommet
suivi passe de 9,60 à 0,81 cm entre 0,50 et 0,642. Les poses du pied droit
et du bassin sont identiques dans l'échantillonnage. La rotation du pied
gauche, les événements et la durée sont conservés. L'écart de cheville
avant et après la fenêtre de correction est inférieur à 0,001 mm et le
déplacement Root Motion mesuré est nul.

Les six paires de captures utilisent la même pose de départ, le même
cadrage et la même translation virtuelle. Elles montrent la retouche
du pied arrière, puis son retour pendant la phase de vol. Le clip est
archivé sous Animation de Lucian et reste sans consommateur. Le test
Play Mode devra utiliser un export d'objectifs régénéré, puis vérifier
l'appui opposé et le mélange marche/course. Rapport et captures dans
`Lucian_Coherence_Contacts/final-pose/walk-release-pilot/`.

### Pilote avec UCC sur sol plat

Le pilote a remplacé la seule marche avant d'exploration dans la copie.
L'export des objectifs et les deux validations de 80 combinaisons chacun
passent. En Play Mode, 12 séquences et 2 143 images ont été enregistrées.
Sur les deux portions de marche lente qui couvrent le point d'appui suivi,
le déplacement cumulé passe de 8,64–8,89 cm à 0,35–0,44 cm, pour une
vitesse médiane identique à 0,0001 m/s près (1,489 m/s).

Sur les mélanges entre 2,48 et 4,47 m/s, aucune semelle ne passe à plus
de 2 cm sous le sol échantillonné et le Root Motion reste désactivé.
Ces mesures portent sur la hauteur des appuis pendant les mélanges et
ne qualifient pas leur glissement ni leur rendu. Le processus a terminé
avec un code 1 à cause d'une exception de l'index de recherche Unity
Editor. Les 12 séquences ont terminé. Le rapport brut conserve l'erreur.
Le profil, le support et l'override de la copie ont été restaurés octet
pour octet. Le pilote reste non branché dans le jeu. Traces et comparaison
dans `walk-release-pilot/play/`.

### Pilote dans le District et intégration en prévisualisation

Les deux versions du contrôleur de cadence ont parcouru le même segment
du District, avec et sans torche. Les deux prises ont la même vitesse
médiane de 1,539 m/s et 165 images de boucle. La différence de distance
enregistrée est de 1,4 cm sur 5,1 m. Sur deux occurrences de la même
fenêtre de phase dans chaque prise, le déplacement du sommet 0:4887
passe de 8,90–8,91 cm à 0,42–0,43 cm. Les semelles restent à moins
de 0,02 cm de pénétration maximale sur ces boucles. Les deux processus
ont terminé avec succès et le déplacement automatique par l'Animator
était désactivé pour les prises.

Les captures appariées montrent l'ombre du personnage. Le rendu des jambes
en jeu reste à vérifier. Le dossier `production-reference/district-phase/`
contient les mesures, traces et captures. Les données d'objectifs, le profil
et l'override produits par Unity pour cet essai ont été reportés à l'identique
dans `Cadence01`. Le clip corrigé y est désormais utilisé. La configuration
de Lucian dans le jeu n'a pas été basculée. Les départs, arrêts et actions
nécessitent leur propre validation.

Le même parcours a ensuite été capturé par la caméra de jeu, à la phase
0,651 du clip dans les quatre prises. Les images montrent Lucian et
l'éclairage de la torche équipée. Le recul de la caméra et la cape
empêchent de juger les pieds. Le rendu rapproché des meshes de contrôle
n'est pas exploitable. Les traces reproduisent 8,90 cm de déplacement
avant et 0,42–0,43 cm après, à 1,539 m/s. La validation visuelle des
appuis reste limitée aux captures sur le modèle CC5 isolé. Captures et
rapports du jeu dans `production-reference/district-rendered/`.

## Non-régression du saut, de l'attaque et de l'esquive

La version de `Cadence01` avec le clip de marche corrigé a rejoué le contrat
du saut avec Domain Reload activé. L'apex de la capsule est de 2,730 m torche
rangée et 2,729 m torche équipée. L'écart à la référence est inférieur à
1 mm. Aucun objectif IK de locomotion n'est maintenu en vol et aucun
Root Motion gameplay n'est relevé. Le test a enregistré 1 576 images,
puis renvoyé le code 1 pour l'exception UnityEditor.Search déjà observée.

Le blocage d'entrée pendant la réception laisse encore environ 16–17 cm
de déplacement planaire pendant la portion verrouillée, en référence
comme avec le pilote. Les entrées redeviennent actives pendant que le
clip de réception est encore visible. Le parcours valide l'apex et la
restitution des commandes, pas une réception immobile. Aucun réglage
de saut ou de réception n'a été modifié. Voir `walk-release-pilot/contract/`.

### Maintien du verrou de réception

Le diagnostic de la réception a relevé de brèves pertes de contact UCC
après la prise du verrou. L'ancien contrôleur libérait le verrou à chaque
perte, puis la commande maintenue revenait alors que le clip continuait.
L'option `holdLandingThroughContactLoss` est activée dans les données de
Lucian et sa prévisualisation. Le premier contact reste nécessaire pour
acquérir le verrou. Ensuite, tant que l'état de réception participe au
fondu et que le verrou reste exclusivement détenu, UCC conserve la
commande planaire nulle. La gravité et les collisions restent dans UCC.

Dans l'essai avec Domain Reload, le déplacement planaire après la prise
du verrou passe de 16 cm à 0 cm, torche rangée comme équipée. Le verrou
reste actif jusqu'à la fin du clip. Un premier échantillon au sol précède
encore la prise du verrou. L'apex reste à 2,729 m à 1 mm près, sans objectif
IK de locomotion maintenu en vol. La course reprend avec l'entrée
maintenue dans les deux prises. Unity renvoie toujours le code 1 pour
l'exception UnityEditor.Search. Rapports dans
`walk-release-pilot/landing-scoped/`. Pentes, plateformes mobiles et
interruptions restent à vérifier.

### Cadence face à un mur

Dans le laboratoire UCC sur sol plat, avec la torche équipée, le stick est
maintenu à la même valeur pendant l'approche, le blocage et la reprise.
Un mur statique temporaire est placé devant Lucian après l'approche,
puis désactivé. Il n'y a aucune écriture de position pendant le parcours.
La vitesse médiane est de 1,489 m/s avant et après le mur, avec une
cadence de 0,829. Une fois bloqué, Lucian ne parcourt aucune distance
planaire sur les 30 dernières images et joue uniquement Idle.
`LitSpeed` revient à zéro malgré le stick maintenu. La boucle de marche
reprend après retrait du mur. Aucun Root Motion gameplay n'est relevé.
Les trois séquences se terminent, avec le code 1 lié à UnityEditor.Search.
Ce contrôle couvre un obstacle frontal statique. Voir
`walk-release-pilot/wall/summary.json`.

### Départ de marche vers la boucle

Le raccord `Walk_Start → Locomotion` de Lucian déclenchait la boucle à
une phase dont les appuis divergeaient du clip de départ. Sur le CC5
isolé, le plus grand pas par image passe de 13,66 à 4,28 cm en décalant
l'entrée de la boucle à 0,775. Mélanger les objectifs IK aux mêmes
phases sans décaler l'entrée donne 14,02 cm. La sortie du clip et la
durée du fondu restent aux valeurs auteur.

En Play Mode sur sol plat, à environ 1,489 m/s, le pic mesuré par rapport
à la capsule passe de 13,85 à 4,56 cm. À la dernière bascule du fondu,
le pic passe de 7,24 à 2,82 cm. Les mesures couvrent quatre paires
d'images par prise à environ 60 Hz. Les 80 cas de préparation et les 80
cas de vérification du support passent. Les douze parcours Play Mode
se terminent, avec le code 1 causé par UnityEditor.Search.

La phase et la signature du jeu de données ont été reportées dans
la prévisualisation `Cadence01`. L'essai couvre le départ avant à
allure lente. Il ne qualifie pas les départs en course, les arrêts,
les diagonales ou le rendu en jeu. Rapports et mesures dans
`Lucian_Coherence_Contacts/final-pose/walk-start-handoff/`.

### Départs en course et en sprint

Le contrôleur de prévisualisation utilise le même clip de boucle
`Lucian_Common_Run_Stride02` dans `Run_Start` et dans `Locomotion`.
Le fondu repart à la phase zéro. Dans un essai isolé sur le CC5, un
décalage à 0,70 réduit le déplacement maximal par image de 18,53 à
6,34 cm à la cadence de course, et de 24,10 à 11,64 cm à la cadence
de sprint. Le fondu et la vitesse de l'état source sont conservés.

UCC exerce effectivement `Run_Start` au départ en sprint. Le pic
de mouvement des pieds par rapport à la capsule passe de 24,36 à
11,74 cm torche rangée et de 23,80 à 11,64 cm torche équipée. La
vitesse physique reste à 8,74 m/s. Le plus bas des deux pieds reste
plus haut pendant le fondu retouché, jusqu'à 12,8 cm sur les images
mesurées. Il faut vérifier ce passage de suspension avant d'intégrer
le décalage. Les deux essais ont terminé leurs huit séquences avec
le code 1 causé par UnityEditor.Search. Aucun Root Motion gameplay.

Au départ en course sans sprint, UCC atteint 5,30 m/s et la boucle
est correctement demandée à `LitSpeed = 3,25`. Le départ joue
encore `Walk_Start`, car `ResolveLocomotionTier` renvoie 1,1 sans
sprint. La transition existante `WalkStartToRun` exige un seuil
supérieur à 2,5 et ne reçoit jamais ce signal. Le raccord de départ
ordinaire conserve un pic d'environ 16,5–16,9 cm. La correction
suivante devra accorder la sélection du départ à l'allure obtenue
dans le seul chemin calibré. Le décalage de sprint reste un essai
de la copie et n'a pas été intégré. Voir `final-pose/run-start-handoff/`.

#### Sélection de l'allure au départ

Le chemin calibré transmet maintenant le niveau course pendant un
départ si la vitesse obtenue place le mélange à dominante course.
Cette valeur permet à la transition existante `WalkStartToRun` de
jouer sans sprint. Aucun paramètre physique ne change.

En Play Mode, le départ en course normale passe par `Run_Start`
torche rangée et équipée, avec un pic de 5,94–6,01 cm par image
sur le fondu final vers la boucle. Sur l'ensemble du départ,
le pic passe de 27,27 à 23,62 cm torche rangée et de 16,55 à
14,08 cm torche équipée. Le raccord initial reste visible. À
1,49 m/s, le départ conserve `Walk_Start`. Les médianes de vitesse
en course et en sprint restent à 5,30 et 8,74 m/s. Aucun Root
Motion gameplay. Dix séquences enregistrées avec l'erreur d'index
UnityEditor.Search déjà observée.

Le décalage 0,70 vers la boucle et son certificat d'appui ont été
intégrés avec la sélection d'allure dans la prévisualisation
`Cadence01`. Le rendu de la suspension en sprint et le premier
fondu `Walk_Start → Run_Start` restent à qualifier. Mesures dans
`final-pose/run-start-handoff/comparison-tier.json`.

#### Pose sortante du repos

La première secousse venait de l'arbre `Locomotion`. Il passait
d'Idle à la boucle de marche alors qu'Unity mélangeait déjà la
pose sortante avec `Walk_Start`. Le chemin mesuré conserve
maintenant le poids sortant durant ce seul fondu de départ.
La sélection du niveau de locomotion et le déplacement UCC
continuent de suivre la vitesse obtenue.

Au premier fondu, le plus grand déplacement de pied relatif à la
capsule passe de 23,62 à 3,49 cm en course et de 12,64 à 2,99 cm
en marche lente. En sprint, le même pic reste sous 3 cm, torche
rangée comme équipée. Les vitesses médianes de marche, course et
sprint restent identiques à 0,001 m/s près. Les dix séquences ont
été enregistrées. Le code 1 provient de UnityEditor.Search.
L'écart résiduel maximal, 14,57 cm en course, se produit dans
le fondu distinct `Walk_Start → Run_Start`. Mesures dans
`final-pose/run-start-handoff/comparison-outgoing.json`.

### Comparaison visuelle à 60 Hz sur sol quadrillé

Deux passages dans le laboratoire FractureGraybox, avec le vrai Lucian,
le solveur UCC, la cape et la même caméra rapprochée. Une suite d'entrées
fait jouer repos, départ en marche, passage en course, arrêt, équipement
de la torche, puis le même trajet torche équipée. La référence utilise
la configuration du jeu, l'autre passage utilise `Cadence01`. Le test a
produit 800 relevés et 50 images par version, au même temps après l'entrée
et donc à des phases de clip éventuellement différentes.

- En marche stabilisée à 1,49 m/s, la référence mélange environ 72 %
  d'Idle et 28 % de marche. L'aperçu joue à 100 % la boucle de marche
  réparée. La différence se voit sur la longueur du pas et le transfert
  de poids. Les images fixes complètent les mesures de contact existantes.
- À 5,30 m/s, la référence joue la boucle de marche à 100 %. L'aperçu
  joue la boucle de course à 100 %, avec une inclinaison du buste et
  une foulée nettement plus marquées, torche rangée et équipée.
- Au relâchement après cette course, les deux versions entrent dans
  `Walk_Stop`. Dans l'aperçu, le niveau reste à 1,1 pendant le freinage
  puis tombe à zéro avant l'entrée. `Run_Stop` et `Walk_Stop` pointent
  actuellement vers le même arbre dans le controller d'aperçu. Valider
  un mouvement de freinage adapté à la course et son raccord par phase
  avant de modifier la sélection de l'état.
- Pendant l'équipement de la torche à l'arrêt, la couche de base reste
  à 100 % sur `Common_Idle_Inplace` et la vitesse est nulle dans les
  deux versions. La cape masque par moments le torse et les jambes.
  Les images montrent le geste et la torche en main. Elles ne qualifient
  pas son éclairage.

Planches, rapports et relevés dans
[`visual-flat-comparison`](Lucian_Coherence_Contacts/final-pose/visual-flat-comparison/).
Les rapports gardent l'exception d'index de `UnityEditor.Search` du premier
passage et le contrôle de saut hérité de la sonde. Cette recette de locomotion
ne comporte aucun saut. Les huit séquences ont été exercées dans chaque
version. Elle ne valide ni le départ en course depuis l'arrêt, ni les
raccords de combat, ni le réseau. Le projet principal n'a reçu aucun
changement de gameplay pour cette comparaison.

#### Arrêt après une course et continuité des appuis

Les transitions du contrôleur d'aperçu choisissent `Walk_Stop` sous le
niveau 1,5 et `Run_Stop` au-dessus de 2,5. En course ordinaire, le niveau
reste à 1,1 au début du freinage. Les deux états partagent le même
`TwinSword_Common_Walk_Stop_Inplace`. Aucun autre clip d'arrêt de course
n'a été identifié dans les ressources de Lucian.

Les deux parcours UCC répètent la marche, la course et l'arrêt torche
rangée et équipée, puis le sprint et son arrêt torche équipée. La seule
différence du test est le maintien des appuis qualifiés pendant le fondu,
avec l'option existante du banc Editor. Le tableau mesure le plus grand
déplacement d'une cheville relative à la capsule entre deux images à
60 Hz, sur les 27 premières images après relâchement. Ce n'est pas une
mesure du glissement d'un point de semelle en appui.

| Arrêt | Aperçu courant | Appuis continus | Distance UCC dans les deux passages |
| --- | ---: | ---: | ---: |
| Course, torche rangée | 30,81 cm | 22,10 cm | 54,83 cm |
| Course, torche équipée | 30,88 cm | 18,48 cm | 53,49 cm |
| Sprint, torche équipée | 41,11 cm | 21,78 cm | 65,06 cm |

Le pic courant tombe sur la première image du fondu, au passage de la
correction calibrée à la solution de secours. Dans le test, les appuis
restent qualifiés sur les 27 images et le fondu commence à la même image.
Le pic résiduel se produit toujours pendant le fondu, malgré la continuité
de la correction. Pour cette phase de course, le raccord entre la foulée
et le geste d'arrêt demande donc encore une retouche du pas de replacement.
Le même clip doit être évalué avec l'autre pied d'appui avant de choisir
une variante de freinage. Les données de semelle de ce parcours ne
suffisent pas pour valider un appui fixe.

Deux passages de 1 000 images et dix séquences chacun, avec l'exception
d'index `UnityEditor.Search` signalée au démarrage. Relevés dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).
Aucun asset ou script de gameplay modifié par l'essai.

#### Phase de la foulée au début du freinage

Six relâchements à 5,29 m/s ont été déclenchés à des moments différents
de la boucle. Le fondu démarre au cinquième relevé dans tous les cas,
alors qu'il reste 28,66 à 29,66 cm à parcourir à la capsule. La sonde
conserve les appuis calibrés sur chacune des 27 premières images.

| Phase réelle au début du fondu | Semelles à ±2 cm du plan, image précédente | Distance horizontale de la cheville la plus éloignée de sa pose de repos, relative à la capsule | Pic de mouvement d'une cheville sur une image |
| ---: | --- | ---: | ---: |
| 0,202 | Aucune | 50,96 cm | 27,31 cm |
| 0,360 | Aucune | 55,49 cm | 14,07 cm |
| 0,536 | Gauche | 48,66 cm | 22,62 cm |
| 0,692 | Aucune | 89,17 cm | 20,86 cm |
| 0,869 | Aucune | 100,16 cm | 17,34 cm |
| 0,047 | Droite | 64,20 cm | 22,45 cm |

Les quatre cas sans semelle à ±2 cm sont des poses de transition sur un
personnage UCC au sol. Certains peuvent être des suspensions normales de
la foulée. Une hauteur instantanée ne suffit pas pour identifier un appui.
La retouche doit conserver la descente déjà en cours dans ces cas, puis
faire suivre le pas de replacement du transfert de poids. Les phases
0,536 et 0,047 fournissent deux départs réellement différents pour
construire et mesurer le raccord. Les essais montrent qu'un seul pas
imposé, ou un verrouillage immédiat du pied le plus bas, serait inadapté
à une partie de la boucle.

19 séquences, 1 298 relevés, exception d'index `UnityEditor.Search`
au démarrage. Rapport et traces `phase-sweep*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).
L'essai reste dans la copie Unity et ne qualifie pas encore les poses
retouchées ni leur sélection en jeu.

#### Deux clips d'arrêt de course sur l'avatar CC5

`Lucian_Run_Stop_LeftSupport_Pilot.anim` et
`Lucian_Run_Stop_RightSupport_Pilot.anim` sont dans le dossier
`Animation/CoherencePilot` de Lucian. Les poses de départ proviennent de
la boucle de course calibrée aux phases 0,5158 et 0,0273. Le premier
pied conserve son appui pendant que l'autre descend, puis le bassin
accompagne le transfert et le premier pied rejoint la pose de repos.
Le déplacement mesuré de la capsule est représenté dans les courbes
des jambes pour cet essai. Aucun delta ne déplace la capsule.

Les deux clips conservent la durée de 0,667 s et les événements du clip
d'arrêt de référence. À la lecture isolée sur l'avatar, le déplacement
Root extrait est nul. Sur le trajet de capsule de référence, la
projection horizontale de la semelle en appui varie au plus de 0,95 mm
et la pénétration maximale entre clés de 0,43 mm. Le réimport reproduit
les poses mesurées. Ces valeurs comparent les clips à leur trajet
d'auteur, sans UCC ni mélange de contrôleur.

Les planches montrent le buste qui se redresse et les deux séquences
de replacement. Elles montrent les clips isolés, au même temps écoulé,
avec une pose de départ différente pour chaque mouvement. Un artefact
d'éclairage est visible sur la première image du clip source dans la
planche gauche. Les deux variantes restent des essais autonomes.
La comparaison suivante doit faire jouer chaque variante en Play Mode
avec la vraie pose sortante du Blend Tree et la capsule pilotée par UCC.
Les phases sans appui, l'annulation, les pentes et la torche active
restent à qualifier avant de raccorder ces clips au jeu.

Planches et résultats `authored-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Arrêts retouchés en Play Mode avec UCC

La copie Unity remplace uniquement le clip d'arrêt via un override
temporaire. Les deux passages utilisent les mêmes fondus, les mêmes appuis
continus et le même réglage UCC. Les deux poses sortantes sont choisies à
des phases comparables, vers 0,54 pour l'appui gauche, torche rangée, et
0,05 pour l'appui droit, torche équipée. Le fondu commence à la cinquième
image dans les deux cas. Aucun Root Motion gameplay.

| Mesure | Gauche, actuel → retouché | Droit, actuel → retouché |
| --- | ---: | ---: |
| Pic du déplacement d'une cheville par rapport à la capsule | 17,48 → 14,68 cm | 21,68 → 15,97 cm |
| Trajet maximal d'un sommet de semelle suivi près du sol, entre l'image précédant le fondu et la 15e | 12,92 → 14,84 cm | 40,80 → 26,93 cm |
| Plus grande hauteur de la semelle censée porter le poids, même intervalle | 3,45 → 5,90 cm | 1,67 → 0,002 cm |
| Distance parcourue par la capsule dans les 43 premières images | 53,92 → 54,83 cm | 54,83 → 53,92 cm |

Le trajet de semelle est calculé par fenêtre continue du même identifiant
de sommet. Il ne compare pas des fenêtres d'appui artistique de durée
identique. Le même clip peut réduire le pic de la jambe libre tout en
dégradant le pied supposé porter le poids, comme à gauche. Les images
montrent une inclinaison du buste conservée plus longtemps dans les deux
cas et un pied gauche parfois visiblement en l'air durant l'arrêt gauche.
Les écarts de distance UCC restent inférieurs à 1 cm et le pilote ne
modifie aucun réglage physique.

415 relevés et 10 captures par passage. Le passage avec les clips
retouchés est enregistré sans erreur. Celui du clip actuel conserve
l'exception d'indexation UnityEditor.Search. La qualité des appuis n'est
pas validée et les clips restent des essais autonomes. Le prochain
raccord doit être évalué à partir du mélange de course réellement affiché
durant le freinage. Les mesures isolées des clips seules ne prédisent
pas l'appui du mélange Animator/UCC.

Planches `play-left.png`, `play-right.png`, rapports et relevés
`play-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Garder la foulée sortante durant le fondu d'arrêt

La recette Editor conserve le poids de la boucle de course dès le
relâchement et tant que l'état sortant est `Locomotion`. La cadence
continue de suivre la vitesse obtenue. Les clips d'arrêt retouchés,
les fondus, les appuis continus et les réglages UCC sont les mêmes
que lors de la comparaison précédente. Le poids de la course à la
première image du fondu passe d'environ 61 % à 100 %. Le fondu commence
toujours au cinquième relevé. Les phases sortantes varient de moins de
0,005 cycle entre les passages.

| Mesure | Gauche, mélange → foulée conservée | Droit, mélange → foulée conservée |
| --- | ---: | ---: |
| Pic du déplacement d'une cheville relatif à la capsule | 14,68 → 8,77 cm | 15,97 → 10,32 cm |
| Trajet maximal d'un sommet de semelle suivi près du sol au début du freinage | 14,84 → 8,97 cm | 26,93 → 7,00 cm |
| Plus grande hauteur de la semelle censée porter le poids | 5,90 → 0,41 cm | 0,002 → 0,07 cm |
| Distance parcourue par la capsule dans les 43 premières images | 54,83 → 53,92 cm | 53,92 → 54,83 cm |

Le pied gauche reste maintenant près du plan dans le cas qui présentait
le soulèvement. Il reste 7 à 9 cm de trajet horizontal sur les points
suivis dans les fenêtres mesurées. Ces valeurs dépassent le seuil de
3 cm retenu pour un appui. Les clips ont été choisis explicitement par
la recette pour deux phases connues. Les phases sans contact au sol,
la sélection des clips en jeu, les reprises et les terrains restent
à traiter. La recette n'a modifié aucun script, contrôleur ou profil
du projet principal.

415 relevés et 10 captures avec l'exception d'index `UnityEditor.Search`
au démarrage. Un artefact d'ombre est présent sur la première capture
de la planche gauche. Résultats et planches `holdrun-*` et
`play-holdrun-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Synchroniser les courbes avec le début du freinage

Entre les relevés 4 et 5, la capsule avance d'environ 7 cm et le
fondu démarre avec un poids nul pour le clip d'arrêt. Les courbes
d'appui du clip retouché commençaient à zéro déplacement comme si
la capsule venait de partir. Le prototype avance d'un pas à 60 Hz
la courbe de progression utilisée à l'auteur. Les paramètres UCC,
la phase sortante, le poids des clips, la durée, les événements et
le début du fondu restent les mêmes dans les deux cas mesurés.

| Mesure | Gauche, avant → courbe avancée | Droit, avant → courbe avancée |
| --- | ---: | ---: |
| Trajet maximal du même sommet de semelle en début de freinage | 8,97 → 5,43 cm | 7,00 → 3,51 cm |
| Distance UCC dans les 43 premières images | 53,92 → 53,92 cm | 54,83 → 54,83 cm |
| Plus grande hauteur de la semelle d'appui | 0,41 → 0,41 cm | 0,07 → 0,07 cm |

L'écart résiduel se concentre dans le fondu. Le plus grand écart
longitudinal par rapport au point précédant le fondu passe de
4,86 à 3,07 cm à gauche et de 3,57 à 1,78 cm à droite. La position
finale du sommet suivi varie de moins de 1 mm entre les deux essais.
Le pic de déplacement des chevilles change peu. Les clips avancés
passent la validation isolée avec un Root extrait nul. Le test
en Play Mode contient 415 relevés et l'exception d'index
`UnityEditor.Search`. La variante reste dans la copie de test.
La cible de 3 cm et les autres phases de course restent à qualifier.

Rapport et traces `lead-*` et `play-lead-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Limite de la retouche à deux images d'avance

Avec une avance de deux pas à 60 Hz, les phases testées précédemment
respectent le seuil de 3 cm. Les distances UCC, les phases sortantes,
le déclenchement du fondu et la hauteur d'appui sont identiques au
passage avec une image d'avance. Les deux clips restent Inplace.

| Variante | Phase de course avant le fondu | Trajet du sommet suivi près du sol | Déplacement longitudinal signé entre les relevés 4 et 15 |
| --- | ---: | ---: | ---: |
| Gauche, phase de référence | 0,5373 | 2,79 cm | +0,78 cm |
| Droite, phase de référence | 0,0458 | 2,67 cm | +0,19 cm |
| Gauche, relâchement plus tôt | 0,4929 | 17,34 cm | −17,03 cm |
| Droite, relâchement plus tôt | 0,9822 | 26,76 cm | −25,65 cm |

Pour chaque pied, le sommet suivi porte le même identifiant dans les
quatre prises. Les phases voisines restent très proches du sol et
la capsule parcourt environ 54 cm. Le défaut qui réapparaît est
longitudinal. Les courbes atteignent une position de pied fixée par
la phase d'auteur. Cette position ne correspond plus au contact
effectivement affiché lorsque la foulée est relâchée plus tôt.

Le réglage par délai seul ne suffit donc pas. Une variante devrait
adapter la pose d'entrée au contact obtenu pendant la foulée, puis
relâcher cette correction avant le pas de replacement. Les phases
sans contact demandent encore de conserver une pose de descente avant
d'entrer dans le raccord. Le choix n'est pas activé en jeu.

12 séquences et 825 relevés. Exception d'index `UnityEditor.Search`
au démarrage. Comparaison, courbes mesurées et traces `two-ticks-*`
et `play-two-ticks-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Adapter le placement du pied au début du fondu

Dans la copie Unity, le test lit une fois la position longitudinale
de l'objectif du pied sur la pose sortante. Il la compare à celle de
la phase ayant servi à qualifier le clip d'arrêt. La différence est
appliquée à l'objectif local du pied porteur. Elle s'efface pendant
le pas de replacement. Le mouvement utilise toujours UCC et aucun
point du monde n'est mémorisé. Le banc choisit explicitement le pied
et applique la correction au raccord `Locomotion → Walk_Stop` sur sol plat.

| Phase mesurée avant le fondu | Trajet du sommet suivi, avant → placement adapté |
| ---: | ---: |
| Gauche, 0,5373 | 2,79 → 2,79 cm |
| Droite, 0,0458 | 2,67 → 2,74 cm |
| Gauche, 0,4929 | 17,34 → 2,02 cm |
| Droite, 0,9822 | 26,76 → 2,90 cm |

Les quatre prises franchissent le seuil de 3 cm dans la fenêtre
mesurée, avec hauteur de semelle inférieure à 5 mm. Les distances UCC,
les phases et l'instant d'entrée correspondent au passage sans
adaptation. Le pic de mouvement de la jambe libre est inchangé.
Cela ne valide ni le transfert tardif, ni les autres phases, ni le
choix automatique du clip. Les transitions pendant une reprise ou
une action et le comportement en pente restent ouverts.

12 séquences et 825 relevés. Exception d'index `UnityEditor.Search`
au démarrage. Aucune nouvelle capture pour cette mesure. Rapports et
traces `offset-comparison.json` et `play-offset-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).
Les scripts du projet principal et le contrôleur du jeu sont intacts.

#### Choix automatique du premier pied d'appui

La recette Editor décide au moment où le bridge déclenche l'arrêt,
avec le clip de course à plein poids. Elle lit la hauteur des
semelles dans les données calibrées à la phase sortante. Si une
seule semelle est à ±2 cm du sol, elle choisit le clip correspondant
et active l'adaptation de sa pose. Sinon, le clip d'arrêt actuel
reste la solution de secours. Le banc change un override sur
l'instance de test, sans modifier le contrôleur.

Huit relâchements ont réellement joué le clip sélectionné. Les
quatre phases à appui unique conservent un trajet de semelle de
2,02 à 2,90 cm dans la première fenêtre mesurée, torche rangée et
équipée. Les quatre phases sans appui proche du sol prennent toutes
la sortie de secours. Le déclenchement reste au cinquième relevé
et le Root Motion gameplay est désactivé.

Cette recette prouve la sélection des deux clips pour les phases
couvertes et l'absence de réutilisation du choix précédent dans les
cas de secours. Elle ne corrige pas encore le raccord de suspension.
Le prochain branchement doit sélectionner les variantes par
paramètre du contrôleur plutôt que remplacer les clips pendant la
lecture. Reprises, pentes, obstacles et sortie vers une action
restent à qualifier.

25 séquences et 1 710 relevés. Exception d'index `UnityEditor.Search`
au démarrage. Résultats `selection-comparison.json` et traces
`play-selection-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).
Aucun changement du contrôleur utilisé en jeu.

#### Sélection par le contrôleur Animator d'aperçu

Dans la copie Unity, un paramètre de présentation sélectionne les
arrêts gauche et droit dans l'état `Run_Stop`. Au centre du nouvel
arbre, le mouvement d'arrêt d'origine reste disponible. Les entrées
qualifiées réutilisent la durée du fondu vers `Walk_Stop`. Les deux
nouveaux clips utilisent le rapport de vitesse des états pour reproduire
le timing du test précédent. L'ancien état et ses transitions restent
la sortie de secours quand la phase n'a pas d'appui unique qualifié.
Le contrôleur ne change plus de clip par override en cours de lecture.

Les deux validations des appuis du contrôleur comportent 80 mélanges
chacune et passent. En Play Mode, les quatre relâchements qualifiés
entrent dans `Run_Stop` au cinquième relevé. Ils reproduisent exactement
les résultats de 2,02 à 2,90 cm de trajet de semelle de la recette
précédente. Les quatre autres entrent dans `Walk_Stop` au même moment
et jouent le clip habituel. Aucun Root Motion gameplay.

Les données de choix et de placement sont encore injectées par la
recette Editor. Le contrôleur testé est archivé avec ces données dans
la copie Unity. Le branchement doit encore déplacer ces données dans
le profil d'aperçu, borner leur durée à un seul arrêt et qualifier
les reprises, les actions et les changements de sol. Le jeu utilise
toujours sa configuration précédente.

25 séquences et 1 710 relevés. Exception d'index `UnityEditor.Search`
au démarrage. Résultats `controller-comparison.json` et traces
`play-controller-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Profil d'arrêt intégré à Cadence01

Le profil d'aperçu contient les courbes des deux arrêts et de leur
sortie vers le repos. La sélection et l'adaptation font désormais
partie du chemin de présentation normal. La recette Play Mode
utilise uniquement les commandes simulées après le chargement.
Elle n'installe ni callback de choix ni override de clip.

Les huit relâchements de référence reproduisent exactement les
choix et les mesures précédentes. Les quatre arrêts qualifiés
donnent 2,02 à 2,90 cm de trajet du point d'appui. Les quatre autres
jouent le clip de secours et tous reviennent à Locomotion. Le test
enregistre 1 710 relevés, sans Root Motion gameplay, et l'exception
d'index UnityEditor.Search connue au démarrage.

Le profil, le contrôleur, leurs appuis signés et les deux clips
testés sont intégrés à `Cadence01`. La recherche de la référence
au profil sérialisé retourne uniquement `LucianCadence.asset`.
Le joueur de production n'a pas été basculé. La correction
s'active uniquement avec le profil configuré et sur un plan
compatible. Les interruptions, les pentes, les obstacles, les
raccords de suspension et le rendu final restent à vérifier.
Le seuil d'acceptation global du profil reste désactivé.

Rapport `preview-comparison.json` et traces `play-preview-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Reprise et saut pendant l'arrêt

Le test rejoue la reprise au huitième relevé, la reprise au
dix-septième et un saut au dix-septième. Sans le nouveau raccord,
la reprise précoce quitte le fondu d'arrêt au deuxième relevé
après la nouvelle entrée. Avec le premier contrôleur d'aperçu,
`Run_Stop` participe jusqu'au onzième. Les deux nouvelles routes
étaient placées avant les routes de reprise dans la liste
d'interruption ordonnée.

Les routes d'arrêt de course sont maintenant insérées après les
routes de reprise et avant l'arrêt de secours. Le contrôleur
d'aperçu quitte à nouveau le fondu au deuxième relevé et le
paramètre d'appui retombe à zéro. Sur la reprise tardive il quitte
l'arrêt au septième relevé dans les deux configurations. UCC
atteint 5 m/s aux mêmes relevés (11 et 12). Le raccord précoce
présente encore un pic relatif de cheville de 15,83 cm contre
13,60 cm dans la référence. La reprise tardive donne 18,30 cm
dans les deux prises.

Sur le saut, le sélecteur est libéré au cinquième relevé, avant
le départ du sol au huitième. L'apex est de 2,7285 m avec et sans
le raccord. Le pic relatif de cheville est de 39,78 cm contre
37,97 cm dans la référence. Il se produit après la sortie de
l'arrêt. Le prochain arrêt choisit un nouvel appui et restitue le
sélecteur à zéro. Aucun Root Motion gameplay.

Les 160 contrôles d'appui ont repassé avec le nouvel ordre.
Le contrôleur et ses données d'appui ont été intégrés uniquement à
`Cadence01`. Trois passages de 15 séquences et 1 056 relevés,
chacun avec l'exception UnityEditor.Search au démarrage. Le rendu
du raccord et les interactions avec attaques, pentes et obstacles
restent ouverts. Données `interrupt-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Arrêt en pente et collision contre un mur

Deux parcours chargent l'aperçu courant dans le laboratoire, avec
un sol à 8° dans le premier et un mur sur sol plat dans le second.
Le banc envoie uniquement les entrées de manette après le placement
initial. Aucune translation corrective pendant les parcours.

Sur la pente, UCC obtient 4,82 m/s en montée et 6,08 m/s en descente.
Le sélecteur d'appui reste à zéro pendant les deux arrêts et les
vitesses reviennent à zéro. Les distances planaires de freinage
mesurées sont 49,5 cm en montée et 61,4 cm en descente. Ce test
vérifie la sortie de secours, sans qualifier les contacts visuels
de la semelle sur une pente.

Face au mur, Lucian atteint 5,29 m/s avant la collision. Sur les
30 dernières images avec le stick maintenu contre le mur, la vitesse
obtenue et LitSpeed sont à zéro et Idle a le plein poids. Le
relâchement ne déclenche aucun arrêt de course calibré. Il reste
2,6 mm de déplacement planaire relevé. En s'éloignant du mur,
Lucian retrouve 5,29 m/s et sa boucle de course. Aucun Root Motion
gameplay dans les deux parcours.

545 relevés sur pente avec l'exception d'index UnityEditor.Search,
617 relevés au mur sans erreur. Pas de captures sur ces deux
parcours. Ils ne couvrent pas la transition sol plat vers pente
pendant un arrêt déjà engagé, les obstacles mobiles ou les actions.
Aucun asset de gameplay modifié par l'essai. Rapport
`terrain-comparison.json` et traces `terrain-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Arrêt du vrai joueur dans le District

Deux passages Bootstrap solo démarrent au point d'apparition de
`District_1_Core`, avec des sauvegardes temporaires et les commandes
de manette. Le second utilise le profil et le contrôleur d'aperçu
sur le prefab du joueur dans la copie. Entre les deux prises
torche rangée et équipée, le banc revient au point de départ.
Aucune correction de position pendant la prise.

| Mesure | Contrôleur actuel | Aperçu Cadence01 |
| --- | --- | --- |
| Vitesse au relâchement | 5,296 m/s dans les deux prises | 5,296 m/s dans les deux prises |
| Distance de freinage torche rangée | 53,56 cm | 54,27 cm |
| Distance de freinage torche équipée | 52,89 cm | 54,27 cm |
| Début du fondu | Cinquième relevé | Cinquième relevé |
| État d'arrêt | Walk_Stop | Run_Stop, appui gauche puis droit |

Dans l'aperçu, le sommet de semelle reste suivi pendant 12 images
de la fenêtre d'appui mesurée, du relevé avant le fondu au quinzième.
Son trajet est de 2,88 cm à gauche et 2,86 cm à droite, avec une
semelle à moins de 4 mm au-dessus du plan. Dans la référence, les
mêmes côtés ne restent suivis que trois images consécutives.
Leurs distances ne sont pas directement comparables à cette
fenêtre complète. Les prises de référence relâchent une image
plus tôt et les boucles affichent des phases différentes.
Les vitesses et l'instant du fondu sont néanmoins identiques,
avec un écart de distance de freinage inférieur à 1,4 cm.

Les deux passages terminent avec succès et sans Root Motion gameplay.
Le cadrage rapproché dans le décor n'a pas montré les pieds et ne
permet pas de valider visuellement le rendu du District. Les rapports
Bootstrap, dont le libellé de fin hérité mentionne la Maison,
confirment le manifeste du District et les scènes chargées.
Résultats et traces `district-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).
La configuration du joueur du projet principal reste la référence.

#### Attaque et esquive pendant l'arrêt dans le District

Le joueur chargé par Bootstrap court jusqu'au même type de phase
qualifiée. L'attaque de base est demandée pendant l'arrêt sur
appui gauche, torche rangée. L'esquive directionnelle est demandée
pendant l'arrêt sur appui droit, torche équipée. Dans les deux cas,
la phase de `Run_Stop` à la demande est 0,2575 et le banc appelle
les façades gameplay qui déclenchent réellement les actions.

Les deux actions sont actives au premier relevé. Au troisième,
`Run_Stop` ne participe plus et le sélecteur d'appui vaut zéro.
Les verrous sont libérés au relevé 54 pour l'attaque et au relevé
59 pour l'esquive. L'esquive parcourt 3,485 m dans le sens opposé
à la course et revient en mode Exploration. Aucun Root Motion
gameplay pendant les deux prises.

Ce passage vérifie l'admission, l'interruption de présentation et
la restitution du contrôle. L'attaque est lancée sans cible et
ne qualifie pas un impact, un enchaînement ou la réception de dégâts.
Il n'y a pas de mesure de rendu du raccord. Les fichiers
`district-actions-*` contiennent le rapport, la comparaison et les
relevés. La configuration du joueur du projet principal reste intacte.
Voir [`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Planches des poses réellement jouées dans le District

La caméra de contrôle affiche les meshes skinnés du vrai joueur
échantillonnés pendant la prise Bootstrap, à six instants identiques
après relâchement. Le décor, la cape et les accessoires ne sont pas
rendus sur les planches. Le sol de contrôle est calé sur le point de
contact UCC et l'éclairage est fixé à l'identique dans les deux prises.
Les entrées avant relâchement ont les mêmes durées dans chaque paire.
La torche est bien équipée dans la deuxième paire, mais son accessoire
et sa lueur ne sont pas inclus dans la capture.

Les images montrent que le raccord préserve davantage l'inclinaison
de course et donne un pas de replacement avant le repos. Elles montrent
aussi un défaut restant. Le pied porteur conserve longtemps son
orientation inclinée après l'arrêt physique. Au relevé 28, l'orientation
de la cheville par rapport à sa pose finale diffère encore de 121° à
gauche et de 51° à droite dans les deux prises de l'aperçu. Ce sont des
écarts d'orientation en trois dimensions, pas des angles anatomiques
de flexion. UCC est déjà sous 0,05 m/s au relevé 12. Le faible trajet
de semelle ne suffit donc pas à qualifier le rendu. La retouche doit
articuler le déroulé du pied et le transfert du poids pendant le
freinage, en conservant un point d'appui crédible.

Planches [torche rangée](Lucian_Coherence_Contacts/final-pose/run-stop-support/visual-district-stop-1.png)
et [torche équipée](Lucian_Coherence_Contacts/final-pose/run-stop-support/visual-district-stop-3.png).
Rapports et relevés `visual-district-*` dans le même dossier.
Aucun changement du joueur du projet principal.

#### Première retouche du déroulé du talon

Les courbes des clips pilotes déplacent l'orientation autour du
point de semelle de la pose de départ, puis rejoignent la pose
d'arrivée pendant le pas de replacement. La durée et les événements
sont conservés. Unity mesure séparément le trajet horizontal du
même sommet et le changement du sommet le plus bas pendant que
la surface d'appui évolue.

À gauche, le prototype redresse le vecteur cheville → orteil de
45,5° vers le repos. Le trajet horizontal du sommet suivi sur
le trajet d'auteur est inférieur à 0,02 mm et l'écart au plan
entre clés reste inférieur à 0,2 mm. Le Root Motion extrait est
nul et le clip réimporté reproduit les poses. Il s'agit d'un
contrôle de clip isolé, avec la progression de capsule utilisée
pour écrire les courbes.

À droite, le déroulé de 42,8° produit 7,83 cm d'écart entre
l'objectif et la cheville obtenue. Le transfert de bassin plus
prononcé testé ensuite augmente l'écart à 14,59 cm. La pose doit
être réécrite autour du moment où le poids passe d'un pied à
l'autre. Une simple augmentation du déplacement de bassin
n'est pas retenue. Les deux variantes droites restent rejetées
et aucune retouche du talon n'est reliée à l'aperçu ou au jeu.

Mesures `heel-left.json`, `heel-right-initial.json` et
`heel-right.json` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).

#### Retouche du transfert pour l'arrêt sur le pied droit

Le test suit désormais le même sommet droit que dans les relevés du
District, `0:5085`. Il compare le déroulé avant, pendant et après le
transfert du poids. L'essai qui avance le contact gauche à la phase
0,32 et engage le replacement droit après la phase 0,40 garde ce
sommet à 0,52 mm de trajet horizontal sur le parcours d'auteur.
Il reste 2,14 cm d'écart de cheville dans le retour du pied droit.

Un fléchissement visuel du bassin de 2,5 cm, centré sur la transition
entre les deux appuis, suffit pour retrouver les objectifs sur
l'avatar. La variante de 4 cm passe aussi, mais celle de 2,5 cm
est retenue pour la prochaine recette. La pénétration maximale
entre les clés est inférieure à 0,3 mm et le Root Motion extrait
reste nul. Les événements et la durée du clip restent conservés.
Le passage d'un pied à l'autre arrive environ 73 ms plus tôt
que dans le clip droit actuel de Cadence01.

Les planches comparent les clips isolés avec le même avatar
et la même caméra. Les courbes restent dans la copie de test.
Il faut vérifier les mélanges, la durée de l'adaptation locale
du pied et l'aspect en mouvement avec UCC avant de remplacer
les clips d'aperçu.

[Déroulé gauche](Lucian_Coherence_Contacts/final-pose/run-stop-support/heel-left.png)
et [transfert droit](Lucian_Coherence_Contacts/final-pose/run-stop-support/heel-right.png).
Mesures `heel-right-*` et relevés de pose dans le même dossier.

#### Déroulé du pied intégré à Cadence01

Les deux clips d'arrêt d'aperçu ont été mis à jour sous les mêmes
noms et GUID. Le profil et les données d'appui ont été régénérés
et les 160 contrôles Unity des mélanges passent. Aucun changement
de contrôleur, de code runtime ou de réglage UCC.

Les huit phases sont rejouées avec le vrai Lucian. Les quatre
raccords qualifiés conservent un trajet de semelle de 2,03 à
2,90 cm entre le début du fondu et le quinzième relevé. Les quatre
autres gardent l'arrêt de secours. Les distances de freinage
reproduisent les mêmes valeurs de 53,92 à 54,83 cm que le test
précédent pour chaque phase qualifiée.

Au relevé 28, l'écart d'orientation 3D de la cheville par rapport
au repos passe de 124,65° à 107,02° à gauche et de 55,45° à 28,86°
à droite. Cela mesure une évolution vers la pose d'arrivée, sans
mesurer à lui seul l'angle de déroulé du pied. Le raccord gauche
reste plus éloigné de la pose finale que le droit et demande un
contrôle du rendu et de la répartition de la torsion.

25 séquences et 1 710 relevés, exception d'index UnityEditor.Search
au démarrage. Aucune nouvelle capture avec UCC pour ce lot. Les
planches `heel-left.png` et `heel-right.png` restent celles des
clips isolés. Mesures `heel-preview-comparison.json` et
`play-heel-*` dans
[`run-stop-support`](Lucian_Coherence_Contacts/final-pose/run-stop-support/).
Le joueur en production conserve son profil et le seuil global
d'acceptation du profil d'aperçu reste désactivé.

#### Huit directions avec la configuration actuelle de Cadence01

Les deux passages utilisent le vrai Lucian dans FractureGraybox,
une cible éloignée et les mêmes entrées à 70 % du stick. Les huit
directions sont effectivement obtenues à moins de 5° de la
direction locale demandée. La vitesse stabilisée va de 2,20 à
3,81 m/s selon la direction et correspond entre les deux profils.
Les mêmes phases de temps sont capturées, avec et sans torche.
Les captures peuvent montrer des phases de clip différentes.

La rotation entre la ligne des épaules et celle des hanches est
plus faible avec l'aperçu dans la plupart des directions. Par
exemple, le maximum mesuré passe de 57,9° à 40,4° vers la gauche,
et de 38,4° à 22,8° en arrière droite, torche rangée. La cape
masque certains mouvements sur les planches.

Les genoux donnent un résultat différent. Le plan de flexion est
mesuré sur les positions de la hanche, du genou et de la cheville,
en ne comparant que des images successives avec au moins 20° de
flexion. En arrière droit avec torche, le genou gauche change de
101,5° entre les phases 0,596 et 0,617 de l'état. Le mélange reste
à 95 % sur le même clip, UCC est au sol et l'IK calibré reste
actif sur les deux images. En recul droit sans torche, un saut
d'orientation de 103,4° touche le genou droit sur un clip arrière
à pratiquement 100 %. Ces écarts géométriques ne sont pas des
angles de rotation anatomique de l'os. Ils isolent une instabilité
sur les poses finales, y compris hors changement de direction.
Les clips concernés doivent être comparés sur l'avatar avec et
sans objectifs natifs avant de modifier la cadence ou l'input.

Deux passages de 1 394 relevés, 17 séquences et 32 captures
chacun. L'aperçu passe sans erreur, la référence garde l'exception
d'index UnityEditor.Search. Aucun Root Motion gameplay.
[Mesures](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/comparison.json),
[poses sans torche](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-plain-64.png)
et [avec torche](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-torch-64.png).
Aucun asset de gameplay modifié par cette comparaison.

#### Origine du retournement du plan de flexion

Les trois cycles Inplace de course arrière, arrière droit et
latéral droit ont été évalués à 240 intervalles sur le vrai
avatar. La mesure recale les phases de Play Mode avec les offsets
du profil. Les versions source et copie donnent les mêmes plans
de flexion avec les seules courbes musculaires (FK).

Les objectifs IK source peuvent déjà faire changer de côté le plan
d'un genou presque tendu. Dans les copies, les objectifs de pied
produisent la même bascule avec une flexion nettement plus grande,
jusqu'à environ 58° sur le cas arrière droit. Le phénomène se
reproduit avec les objectifs calibrés appliqués à l'avatar isolé.
UCC, la torche et les changements de direction ne sont pas nécessaires
pour reproduire ce défaut.

La recette construit une orientation de genou à partir de la pose
FK source. Elle interpole entre les orientations voisines quand
le genou est trop tendu pour définir son plan de manière stable.
Les objectifs de pieds restent identiques. Le maximum observé
entre deux phases espacées d'environ une image de jeu, sur les
poses avec au moins 20° de flexion, passe de 113,5° à 29,4° en
arrière, de 106,4° à 26,0° en arrière droit et de 93,4° à 6,1°
en latéral droit.

Les deux commandes Editor se terminent avec succès. Il faut
encore vérifier comment les orientations se mélangent entre
directions et comment le guidage coopère avec le solveur UCC.
Aucun changement de runtime, de clip ou de profil utilisé en jeu.
[Comparaison source, copie et guidage](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-source-comparison.json).

#### Mélanges directionnels avec guidage des genoux

Sur la copie isolée, les 16 cycles de combat fournissent des orientations
mesurées sur leurs sources. Le prototype les pondère avec les mêmes phases
et poids que les objectifs de pied. Les 17 séquences de Play Mode couvrent
Idle puis les huit directions, torche rangée et équipée, avec 1 394 relevés.
Les vitesses médianes UCC et les directions locales stabilisées sont
identiques à la précision du rapport. Le Root Motion reste désactivé.

Sur deux images consécutives avec au moins 20° de flexion, le pire écart
arrière passe de 103,4° à 29,9°, l'arrière droit avec torche de 101,5° à
25,0°, et le latéral droit avec torche de 87,2° à 5,5°. En revanche, en
avant avec torche, le genou gauche passe de 74,8° à 126,1°, avec des poids
marche/course stables à 32/68. Cet écart invalide le guidage universel par
moyenne de cycles. Le contrôleur utilisé en jeu conserve sa configuration.
La trace qualifie le mouvement du plan de flexion, sans mesurer directement
le glissement de semelle. L'exception UnityEditor.Search subsiste dans le
rapport du lot, qui a tout de même enregistré les 17 séquences.
[Mesures comparées](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-runtime-comparison.json).

Un deuxième passage capture les poses consécutives sur le modèle du jeu
dans le laboratoire. En avant, images 58 et 59, le solveur reproduit à
0,02° près la rotation demandée de 126°. Le défaut se trouve donc dans la
consigne issue du mélange, avant la résolution IK. Sur deux diagonales
avant, aucune consigne n'est appliquée aux pires images du genou droit.
La correction suivante doit fournir une direction continue et cohérente
dans l'espace de flexion de la jambe, y compris là où la moyenne des plans
devient inutilisable. Ces résultats ne qualifient pas la variante pour le
jeu.
[Trace consigne et pose](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-requested-vs-pose.json)
et [captures consécutives](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-forward-torch-sequence.png).

#### Angles de flexion dans un repère commun

Le deuxième prototype mesure un angle signé autour de l'axe cuisse–pied,
avec l'avant du personnage et une légère ouverture latérale comme repère.
À la génération, l'angle est borné à ±50° et lissé sur le cycle. Pendant le
jeu, Unity fournit les mêmes poids et phases que pour les pieds. Mélanger
ces angles évite l'annulation de normales opposées. Le pilote ne modifie
que les consignes de genou, après calcul des objectifs de pied.

Dans les 17 séquences du laboratoire, à allure stabilisée, le maximum
entre deux images à 60 Hz pour un genou fléchi d'au moins 20° descend à
17,5° toutes directions et torches confondues, contre 126,1° pour le
premier guidage. En avant avec torche, le genou gauche culmine à 13,7°.
Les vitesses médianes et directions physiques restent identiques à la
précision du rapport. Le Root Motion reste désactivé. Le cas de bascule
visible sur le modèle est repris aux mêmes images dans les deux variantes.

Les 34 premières images de chaque essai contiennent encore un écart de
46,6° avant activation du guidage, puis de 38,3° à sa prise de relais en
avant torche rangée. En latéral gauche avec torche, un écart de 32,5° se
produit alors que le guidage est déjà actif. Restent à qualifier le
raccord depuis Idle, les appuis au sol et les interruptions en jeu.
Le prototype reste dans la copie isolée. Unity a enregistré 1 394 relevés
avec l'exception UnityEditor.Search habituelle.
[Comparaison mesurée](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-anatomy-comparison.json)
et [captures à 60 Hz](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-anatomy-forward-torch.png).

#### Prise de relais depuis Idle

Un essai supplémentaire réutilise le dernier plan de flexion effectivement
affiché et rejoint la consigne en 0,10 s de temps acteur. Au départ avant
torche rangée, le premier écart sous guidage passe de 38,3° à 27,5°.
La pire variation stabilisée reste 17,5°. Les objectifs de pied locaux
sont identiques sur 1 308 relevés comparables à 0,001 cycle près. Les
1 394 relevés du lot indiquent le Root Motion désactivé.

La secousse initiale de 46,6° se produit une image plus tôt, pendant que
le fondu ne donne déjà plus que 44 % de poids à Idle. L'état courant reste
Idle et la présentation applique encore son traitement de pied d'origine.
À l'image suivante, l'état de locomotion et les objectifs mesurés prennent
le relais avec un poids de correction au sol de 0,87 à gauche. Cette
discontinuité précède la correction des genoux. Il faut mesurer les poses
et les objectifs pendant le fondu Idle–locomotion, puis valider leur
raccord et les appuis dans le District. Les sources et la configuration
de jeu restent intactes. Le lot a produit ses relevés malgré l'exception
UnityEditor.Search.
[Comparaison du raccord](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-entry-comparison.json)
et [captures du départ](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-entry-forward.png).

#### Appuis pendant le fondu de départ

Deux prises de 17 séquences avec le véritable modèle comparent le guidage
à partir de la locomotion et le guidage dès le fondu. Le second ne change
que les consignes de genou. Au départ avant torche rangée, il réduit le saut
de plan de flexion de 46,6° à 22,8° sur la quatrième image, puis laisse un
écart de 29,7° à l'image suivante. Le changement latéral gauche avec torche
atteint 36,5° contre 32,5° dans la référence. Les deux prises présentent un
léger décalage horizontal et leurs phases ne coïncident pas sur tous les
relevés. Les écarts de position absolue ne mesurent donc pas le glissement.

Le sol du laboratoire est à Y=9,5 m. Pendant la quatrième image, la semelle
gauche monte à environ 12,5 cm au-dessus du plan et celle de droite descend
de 6,5 cm sous le plan, avec ou sans guidage. L'objectif UCC de cheville
gauche monte d'environ 15,8 cm en une image dans la référence, alors que le
poids de correction est encore à 0,93. La pose du genou seule ne règle pas
ce raccord. Le prochain essai doit faire coopérer les objectifs d'appui
d'Idle et de la foulée entrante pendant le fondu, puis vérifier les semelles
image par image. Le guidage anticipé reste écarté de la configuration du jeu.
La référence a enregistré l'exception UnityEditor.Search, la prise du
candidat a terminé sans erreur enregistrée.
[Mesures des semelles](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-fade-comparison.json)
et [captures avant/après](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/knee-fade-forward.png).

#### Mélange des appuis mesurés au départ de combat

L'essai réutilise les objectifs mesurés de CombatIdle et de la foulée,
pondérés par les clips actifs pendant le fondu. Il couvre 18 séquences
par prise, dont les deux départs avant depuis un arrêt complet. Pour les
images comparées, les vitesses UCC coïncident et le Root Motion reste
désactivé.

Sur l'image 4 torche rangée, la semelle droite passe de 6,5 cm sous le sol
à 1,3 cm au-dessus. Le résultat se retrouve avec la torche équipée. En
revanche, la semelle gauche monte plus vite, de 12,5 à 20,0 cm à cette même
image. Le plan de flexion gauche pivote de 54,4° entre deux images contre
46,6° dans la référence. Dès la première image de locomotion seule, la
semelle gauche est déjà à 37,5 cm du sol dans les deux prises.

Le fondu sait préserver l'appui droit, mais la pose entrante de milieu de
foulée ne fournit pas un premier pas naturel. Le prototype de mélange seul
est écarté. Le prochain essai doit choisir une phase entrante compatible
avec l'appui d'Idle ou créer un raccord de premier pas à partir des poses
sources, en conservant la réponse UCC mesurée. La semelle levée ne doit
pas être ramenée au sol par l'IK pour masquer le raccord. Le candidat a
terminé sans erreur enregistrée, la référence conserve l'exception
UnityEditor.Search. Aucun asset de jeu n'a été modifié dans cet essai.
[Mesures](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-start-goals-comparison.json),
[torche rangée](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-start-goals-forward.png)
et [torche équipée](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-start-goals-forward-torch.png).

#### Choix d'une phase de premier pas

Le balayage de 120 phases compare les appuis de la foulée avant à ceux
d'Idle sur les quatre premières images, en reprenant le mélange mesuré
de 32 % marche et 68 % course. La meilleure correspondance horizontale
trouvée entre à 44,2 % du cycle et change de pied porteur. En Play Mode,
cette phase s'applique au départ avant torche équipée. Elle réduit la
variation du genou gauche de 56,5° à 41,1° sur la première image mobile,
avec des vitesses UCC identiques. Au premier état de locomotion seul, la
semelle libre atteint 39,0 cm, contre 37,5 cm au départ de référence.

Au premier départ torche rangée, la phase mesurée reste à zéro. Le
controller de prévisualisation possède sa propre transition de CombatIdle
vers CombatLocomotion conditionnée par CombatMoveMagnitude, avec un
fondu fixe de 0,03 s et un offset nul. Le bridge ne pilote sa demande
que lorsque son drapeau de présentation Idle est actif. Toute retouche
du départ devra couvrir ces deux voies. La différence observée entre
prises ne prouve aucun effet de la torche sur la phase du cycle.

Sur les données de semelles du mélange avant testé, choisir la phase
qui minimise la hauteur maximale donne encore 25,1 cm pendant les
quatre premières images. Optimiser seulement la première image donne
19,1 cm, puis un pic de 33,6 cm dans le même intervalle. Ce cas demande
un raccord de premier pas propre avec transfert d'appui progressif.
Le choix de phase seul reste écarté. Les mesures portent sur le modèle
et le mélange avant testés, sans qualification des autres directions.
Le candidat a terminé sans erreur enregistrée. La référence conserve
l'exception UnityEditor.Search. La configuration du jeu est intacte.
[Balayage des hauteurs](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-start-height-scan.json),
[comparaison Play Mode](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-start-phase-comparison.json)
et [captures](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-start-phase-forward-torch.png).

#### Premier clip de raccord Inplace

Le clip d'essai reconstruit les courbes Humanoid entre CombatIdle et une
pose avant obtenue en combinant les cycles marche/course à 32/68. Les
références de semelles proviennent du profil calibré. Le pied droit
porte pendant tout ce premier montage et sa trajectoire locale tient
compte du déplacement UCC mesuré. Les sources et le contrôleur du jeu
restent hors de cet essai.

Unity a généré et mesuré le clip de 83 ms sur l'avatar de Lucian. La
trajectoire racine est nulle et la semelle porteuse reste à moins de
0,1 mm du plan sur les images mesurées. Le bassin rejoint la pose cible à
8,4 mm et 0,42°. Cependant, la position finale demandée par la foulée et
le déplacement de capsule imposent près de 40 cm de glissement si le
même pied reste porteur. Le pied libre monte encore de 11,4 cm sur une
image et la variation du plan de flexion atteint 32,2°. Le clip reste
isolé et n'est pas qualifié pour le contrôleur de prévisualisation.

Les critères de pose d'arrivée et de tenue d'un unique pied d'appui
sont incompatibles dans cette première construction. Le raccord suivant
doit inclure la pose d'atterrissage du premier pied et le transfert de
poids, puis rejoindre la foulée sur le nouveau pied porteur. Les mesures
ci-dessous utilisent un déplacement de référence dérivé de la trace UCC,
et doivent être confirmées en Play Mode sur une prise appariée.
[Mesures du clip hors Play Mode](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-forward-first-step-offline.json).

#### Premier pas avec transfert d'appui

Deux clips Inplace de 167 ms posent le pied gauche avant de libérer
l'appui droit. Le bassin rejoint la même pose entrante, à 3,5 mm et 0,94°.
Le transfert ramené de 83 à 67 ms et la libération immédiate du pied
droit réduisent la dérive de son appui de 7,3 cm à moins de 1 mm.
L'appui gauche dérive de 6,2 mm et l'écart vertical maximal d'une
semelle porteuse reste sous 2 mm. La trajectoire racine est nulle.
Le calcul applique la trajectoire UCC de référence au clip hors Play
Mode. Il ne constitue pas encore une vérification des contacts en jeu.

La fin du raccord reste à corriger. L'écart à l'objectif de cheville
de sortie atteint 11,4 cm et la semelle libre monte de 8,6 cm sur une
image. Les scripts et les sources du jeu sont préservés. Les deux clips
restent dans la copie isolée. La prochaine qualification doit comparer
la pose finale à la vraie pose du Blend Tree sur l'avatar, puis vérifier
le raccord dans le laboratoire et le District.
[Premier transfert](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-forward-support-transfer.json)
et [transfert avancé](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-forward-early-transfer.json).

#### Pose de sortie issue du Blend Tree

Le controller de prévisualisation est évalué sur l'avatar à la phase
0,5583, avec les paramètres relevés en jeu. Unity confirme 67,87 % de
course et les objectifs de pied sont résolus avec le profil mesuré.
Face à cette référence, le transfert précédent arrive à 0,45 mm à
gauche et 2,11 mm à droite. L'écart de 11,4 cm venait donc de l'objectif
de cheville reconstruit par approximation des deux clips.

Le montage recalé sur la vraie pose reproduit la position et la rotation
des pieds et du bassin à la précision de l'échantillonnage. En appliquant
la trajectoire UCC de référence hors Play Mode, la dérive d'appui mesurée
est de 0,22 mm à droite puis de 1,34 mm à gauche. La hausse du pied libre
atteint encore 8,7 cm en une image et dépasse le seuil d'essai de 8 cm.
Le mouvement du premier pas doit être jugé en mouvement dans le laboratoire,
notamment lors du lever et de la pose du pied, avant validation dans le
District. La comparaison vérifie la pose de sortie du Blend Tree avec
ses objectifs mesurés hors jeu, sans valider le solveur UCC en temps réel.
Le clip reste isolé et le test Unity s'est terminé sans erreur.
[Mesures et comparaison à la pose réelle](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-forward-tree-exit.json).

#### Premier pas avant dans le laboratoire UCC

Le clip a été branché dans le controller de la copie isolée. Le premier
passage a révélé que Moving Legs reprenait progressivement les jambes
jusqu'à un poids de 1 pendant le nouveau pas. Après avoir déclaré le pas
comme état corps entier, le poids est resté nul sur les 20 images du
clip mesurées dans les deux départs. La couche de torche reste visible.
Le clip rejoint le Blend Tree à la phase 0,5583. Il n'est pas joué au
changement vers l'arrière. Les vitesses UCC des images de départ se
recoupent à environ 0,01 m/s près et le Root Motion reste désactivé.

Le pied droit porteur reste au plan du sol au lieu de pénétrer de 6,5 cm
pendant le départ. Le premier lever du pied gauche culmine à 8 cm avant
la pose, puis il prend l'appui. La hauteur de la semelle porteuse reste
à moins de 2 mm du plan pendant les contacts mesurés. En horizontal,
le point le plus bas de la semelle droite se déplace de 1,4 cm torche
rangée et 3,9 cm torche équipée, au-delà du seuil de 3 cm dans ce dernier
cas. Le pied gauche reste sous 2 mm. Le point le plus bas est une
approximation de contact. Une trace du même vertex est nécessaire pour
isoler le glissement réel et confirmer l'écart entre les deux prises.

Les deux prises ont enregistré 410 relevés avec l'exception
UnityEditor.Search. L'essai ne qualifie ni l'interruption par saut ou
attaque, ni les autres départs, ni le District. La variante est prête
pour poursuivre ces mesures dans la copie isolée.
[Mesures Play Mode](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-firststep-play-comparison.json),
[captures torche rangée](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-firststep-play-plain.png)
et [captures torche équipée](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-firststep-play-torch.png).

#### Glissement du premier appui et interruptions

Le suivi du même vertex droit `0:5401` confirme 1,40 cm de dérive torche
rangée et 3,94 cm torche équipée. Sur les mêmes quatre intervalles de
pose, la capsule avance respectivement de 22,96 cm et 20,42 cm. Les
2,54 cm d'écart de déplacement expliquent l'écart horizontal de semelle.
La pose avance au même rythme malgré cette différence au démarrage.
Le prochain raccord doit indexer la progression du clip sur le mouvement
effectivement obtenu par UCC, avec un comportement défini pour un arrêt
ou un obstacle, sans appliquer de mouvement à la capsule.

Le saut scripté et une demande de présentation `BasicSkill_1` acceptent
d'interrompre le clip à 20 %. Dès l'image suivante, les objectifs du
premier pas ne sont plus appliqués. L'état sortant disparaît après
quatre images de fondu et ne reprend pas la main. Le saut décolle sept
images après la demande et atteint 2,73 m au-dessus du départ mesuré.
L'attaque vérifie la priorité de la présentation, sans exercer le chemin
d'entrée du joueur, les dégâts ou l'impact. Le Root Motion reste
désactivé et Unity a enregistré les neuf séquences malgré l'exception
UnityEditor.Search. Le District et la cinématique restent à vérifier.
[Suivi des semelles et interruptions](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-firststep-interrupt-comparison.json).

#### Premier pas synchronisé à la distance UCC

La copie de test convertit la distance parcourue le long du départ en
phase du clip. Le corps et les objectifs de pied utilisent la même phase.
L'Animator rejoint ensuite la foulée à la phase mesurée de sortie. La
première prise avait figé la phase à zéro car le bridge abandonnait sa
demande avant l'entrée effective dans l'état. Le passage suivant laisse
le temps à l'Animator de confirmer l'entrée.

Sur les deux départs avant, les distances UCC restent de 22,96 et
20,42 cm pendant le premier appui. La dérive du vertex droit descend
sous 0,1 mm dans les deux cas. Le vertex gauche reste sous 1,5 mm
pendant le second appui. Le saut et la présentation d'attaque retirent
toujours les objectifs du pas dès l'image suivante, et le saut atteint
2,73 m. Le Root Motion reste désactivé.

Le mur bloque la capsule après 6,6 cm, fige la phase à 0,12 puis fait
sortir le clip à l'image 11. Il ne se relance pas malgré le stick tenu.
Au relâchement du stick, le clip sort à l'image 9, la vitesse retombe à
zéro à l'image 11. Le fondu de relâchement conserve la dernière phase
pendant que la capsule freine encore. Les appuis de ce fondu restent
donc à vérifier séparément. Ces mesures couvrent le laboratoire, pas
le District, les pentes ou la cinématique. Unity a enregistré 1 066
relevés avec l'exception UnityEditor.Search.
[Contacts et interruptions](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/combat-firststep-distance-comparison.json)
et [traces arrêt et mur](Lucian_Coherence_Contacts/final-pose/combat-direction-visual/firststep-distance-abort.csv).

Dans le District, l'ancien appel de l'attaque de base se faisait en mode
Exploration. Le parcours place explicitement l'instance de test en mode
Combat, condition du chemin d'attaque libre, puis utilise la compétence
authentique et le contrôleur d'esquive. Référence et variante terminent
l'attaque et l'esquive Inplace, libèrent les verrous UCC et rétablissent
le mode Exploration. L'esquive parcourt 3,5013 m en référence et 3,5007 m
avec la variante. L'écart est inférieur à 1 mm. Les deux tests se terminent
avec le code 0. Ce passage valide le déclenchement et la sortie d'une
attaque et d'une esquive. Les impacts sur cible, les enchaînements,
les interruptions et la Timeline restent à exercer. Rapports dans
`production-reference/district-actions/`.

## Contre cinématique dans Iluvilirae_Test

Le parcours standard refuse `CombatLab_CounterSkill_2` avant le
premier impact, avec le Lucian de référence comme avec Cadence01.
L'ennemi configuré dans la scène, `ChargedEnemy_Lab`, ne résout pas
l'état `Base Layer.Hit_1` demandé par les signaux. Le prefab Juggernaut
de production porte cet état et utilise le même avatar, mais n'a pas
terminé l'initialisation de navigation dans ce laboratoire.

Un essai ciblé a conservé la navigation et les attaques de l'ennemi de
laboratoire. Une copie de son contrôleur, dans le projet jetable,
a reçu le vrai clip `Hit_1` du Juggernaut de production. Avec cette
fixture et `LucianCadence(Clone)`, le rig lance la Timeline, joue ses
quatre signaux caméra puis signale une fin naturelle et la restitution
de la caméra. L'assertion sur l'observation du jeton de mouvement de
Lucian échoue. Elle empêche de qualifier la restitution du contrôleur
et des commandes dans ce parcours. Les signaux caméra ne prouvent pas
le nombre d'impacts ni les dégâts. Le test s'arrête avec le code 1.

La scène et les données de prévisualisation ont été restaurées dans la
copie. Les résultats sont dans `walk-release-pilot/counter/`.

Le contrôle de suivi échantillonne aussi le lancement synchrone du rig.
Le vrai acteur `LucianCadence(Clone)` possède son jeton de mouvement et
l'Animator autorise le Root au lancement de la Timeline. Après sa fin
naturelle, le même contrôleur gameplay est restauré et le jeton et
`Animator.applyRootMotion` sont désactivés. Cette mesure confirme le
transfert et la restitution d'autorité d'animation.

Le parcours échoue ensuite au contrôle des effets d'impact. Le détecteur
de particules retrouve zéro instance des quatre attendues. Il ne mesure
pas les dégâts, et le test s'arrête avant le contrôle final des verrous
d'entrée. Ce résultat ne valide pas le contre complet. La Timeline,
ses signaux et les ressources du jeu n'ont pas été modifiés.
Voir `walk-release-pilot/counter/handoff-timeline.log`.

## Départ de marche dans la Maison

La variante complète `Cadence01` a ensuite été chargée dans la même copie
pour les deux prises de 54 images. Seuls le contrôleur, le profil et les
composants de présentation du prefab Lucian ont été remplacés dans la copie.
Les 15 images de boucle stabilisée par prise donnent une vitesse médiane
de 1,512 m/s avant et après, et un déplacement de 1,251 m sur la fenêtre
enregistrée. Le poids médian d'Idle passe de 70,94 % à 0 %. La cadence du
candidat est de 0,842 sur les clips locomotion. Les résultats sont identiques
torche rangée et équipée. L'Animator reste Inplace dans les quatre captures.

Cela vérifie la disparition d'Idle parasite à cette vitesse, avec la même
distance physique. Les 15 images ne couvrent pas un cycle complet. Elles
ne qualifient ni le glissement, ni la pose de course, ni les raccords
ou les actions. Les empreintes du prefab, des données Lucian et du
contrôleur de la copie correspondent à la source après restauration.
Voir `production-reference/fixed/candidate-comparison.json` et la trace
de la variante dans `production-reference/fixed/candidate/`.

La répétition à durée fixe a joué 54 images de marche par prise, avec la
même origine et le même stick. Distance UCC à la fin de la prise
1,282 m en référence et 1,289 m avec le masque, torche rangée ou équipée.
Les huit captures ont été prises avec l'application du Root Motion
de l'Animator désactivée.

À état, phase, vitesse et position proches, 47 images sont appariées
torche rangée et 53 torche équipée. Pour la torche équipée, le nombre
d'images à plus de 2 cm sous le sol passe de 29 à 22 à gauche et de 10 à
9 à droite. Torche rangée, il passe de 11 à 7 à gauche et de 19 à 29 à
droite. Des écarts de pose des chevilles apparaissent dans les deux cas.
Cette comparaison ne valide pas le masque seul. Elle ne mesure pas le
glissement horizontal sur un appui continu et ne vérifie ni la course
ni les actions. La cape masque les pieds dans les captures de jeu.
Le contrôleur de la copie est restauré et son empreinte correspond au
contrôleur source. Voir les rapports, traces, captures et mesures dans
`Lucian_Coherence_Contacts/final-pose/production-reference/fixed/`.

Le stick virtuel est injecté par le parcours Bootstrap, depuis le même point
d'apparition dans chaque prise. La torche est équipée par le système de jeu.
Les prises sont indépendantes. Le seul repositionnement du personnage a lieu
entre les prises dans la copie de validation, avant la reprise des entrées.

Dans la référence, torche rangée comme équipée, la boucle mélange environ
71 % d'Idle et 29 % de marche alors qu'UCC avance à 1,51 m/s. Cette observation
concerne la marche à stick partiel, sur ce passage. Elle explique pourquoi
isoler la torche ne suffit pas à synchroniser le cycle et la distance parcourue.
La vérification suivante doit comparer la cadence du candidat déjà isolé à
cette même vitesse dans le jeu.

La première comparaison appariée sur les phases donne un effet différent pour
chaque pied. Torche équipée, sur 37 paires, les passages à plus de 2 cm sous
le sol passent de 19 à 5 à droite et de 2 à 17 à gauche. La prise torche
rangée n'apparie que 13 images de départ et montre aussi un écart à gauche.
Les prises ont le même départ et le même stick, mais des nombres d'images
différents. Le sol est irrégulier et la cape cache les pieds dans la vue du jeu.
Ces mesures ne qualifient ni l'effet du masque seul ni la qualité des appuis.
Les rapports, traces et images sont dans
`Lucian_Coherence_Contacts/final-pose/production-reference/matched/`.

À l'import du contrôleur Lucian, Unity signale aussi trois références de
transition sans destination. Les trois sous-objets concernés n'apparaissent
dans aucune liste de transitions du fichier. Les raccords actifs de départ
et d'arrêt référencent les états existants. Les avertissements sont donc
documentés séparément du problème d'appui.

## Première comparaison dans la Maison

Un instantané jetable du projet a démarré la Maison par Bootstrap avec une
sauvegarde temporaire. Le parcours général a validé le point d'apparition
et le contact au sol, puis s'est arrêté au lancement de l'attaque de base.
Ce parcours n'a donc exercé ni la course ni le saut.

Un parcours séparé a joué la marche avec le stick virtuel, torche rangée
puis équipée. Le contrôleur de référence et la variante avec le seul
masque bras/doigts ont tous deux terminé et produit quatre captures
Inplace. Les trajectoires UCC ont été exercées, mais les prises ne sont
pas appariées en phase et les durées de marche obtenues diffèrent.
Le cadrage frôle aussi la géométrie de la Maison. Cette première
comparaison ne permet pas d'attribuer une amélioration des jambes au
masque en jeu. Pour le prochain contrôle, partir du même point et de
la même direction pour chaque prise, enregistrer à cadence fixe et
utiliser un cadrage qui laisse les deux pieds visibles.

La modification n'a concerné que la copie, dont le contrôleur a ensuite
été restauré à l'identique. Captures, traces et rapports dans
`Lucian_Coherence_Contacts/final-pose/production-reference/`.

## Variante avec transfert de poids

Une copie séparée du raccord FL rapproche progressivement le bassin du
pied d'appui. Les poses d'entrée et de sortie du premier pilote restent
identiques. À 0,267 s, la distance horizontale bassin–cheville d'appui
passe de 26,02 à 15,17 cm. Le minimum de semelle entre les images
échantillonnées s'écarte de 0,105 mm du plan. Aucun déplacement physique
ou lacet Root Motion mesuré. Le premier pilote reste identique.

Le fichier et les sept vues comparables sont archivés. Cette mesure
géométrique ne prouve pas l'équilibre du corps en jeu. Le clip est
encore isolé et demande un essai de raccord en Play Mode avec UCC.
Voir `Lucian_Coherence_Contacts/final-pose/brake-pilot/weight-transfer/`.

## Comparaison visuelle du clip pilote

La planche `Lucian_Coherence_Contacts/final-pose/brake-pilot/visual/index.html`
compare sept instants avec les mêmes poses de départ et d'arrivée, la même
durée et le même cadrage. Le témoin interpole les deux pieds au sol. Le
pilote lève le pied gauche puis replace le droit. À 0,267 s, le pied
gauche diffère de 11,45 cm, avec un bassin identique entre les deux poses.
Les maillages sont figés à l'instant de chaque capture pour éviter les
images réutilisées par le rendu. La cape et l'équipement sont absents de
cette vue de diagnostic.

Le pied gauche se dégage visiblement. Le bassin reste toutefois à environ
26 cm de la cheville droite pendant la phase d'appui droit. C'est une
mesure de position, pas une mesure du centre de masse. Le transfert de
poids doit être travaillé et vu en mouvement avant d'intégrer le clip.
Cette comparaison isolée n'est pas une validation du rendu en jeu.

## Clip pilote d'arrêt diagonal avant gauche

`Lucian_CombatBrake_FL_RightSupport_Pilot.anim` est un nouveau clip
Humanoid de 0,667 s dans `Lucian/Animation/CoherencePilot`. Il part de
la phase 0,896 de la marche avant-gauche et se termine sur la phase
0,308 de CombatIdle. La descente du pied gauche de Walk_Stop donne la
forme du pas. La montée est reconstruite à partir de cette courbe.
Le pied droit garde son appui jusqu'à la repose du gauche, puis se
replace à son tour. Les courbes sont intégrées au clip et le bassin
rejoint la pose finale de combat.

Sur 121 poses échantillonnées dans Unity, la trajectoire horizontale
de la semelle est inférieure à 0,02 mm sur les intervalles d'appui
définis. La semelle la plus basse reste sur le plan du rig. Le bassin
correspond aux deux poses de référence aux extrémités. Le déplacement
et la rotation physiques extraits valent zéro. La relecture du clip
sérialisé reproduit les mêmes positions et rotations mesurées.

La pose reste à examiner en mouvement, notamment le transfert de poids
et le croisement des jambes. Le pilote couvre une seule phase et un
seul appui d'entrée. Aucun contrôleur ne référence encore ce clip.
Les mesures intermédiaires et finales sont archivées dans
`Lucian_Coherence_Contacts/final-pose/brake-pilot/`.

## Source du pas de replacement en combat

Unity a échantillonné la marche diagonale avant gauche, Walk_Stop et les
deux repos sur le même avatar. Le critère minimise la plus grande distance
horizontale entre les deux paires de pieds, sur 241 échantillons par clip.
Les angles de bassin sont relevés après la recherche. Ce test isolé ne
représente pas les mélanges directionnels ou les phases arbitraires en jeu.

Le meilleur raccord direct vers CombatIdle (phase source 0,896) laisse
2,73 cm au pied droit, 25,02 cm au pied gauche et 62,27° de différence au
bassin. Le pied droit est le meilleur candidat à l'appui dans cette
phase. La seule sélection d'une phase ne suffit pas pour le pied gauche
ou le bassin. Walk_Stop dure 0,667 s. Sa semelle gauche varie de 11,66 cm
en hauteur contre 1,28 cm à droite, ce qui donne une référence de levée
et de repose à gauche. Le clip entier laisse toutefois au mieux 13,76 cm
au raccord d'entrée et 18,08 cm vers CombatIdle à sa sortie.

Le mouvement d'arrêt commun est donc une source de timing pour le pas
gauche de cette diagonale. Il faut retoucher les positions finales et le
transfert de poids vers la pose de combat. Les phases où le pied gauche
porte le poids demandent une variante de raccord, à mesurer séparément.
Aucune réutilisation directe du clip n'est appliquée. Rapport
`Lucian_Coherence_Contacts/final-pose/brake-donor-audit.json`.

## Réponse de l'arbre d'exploration au freinage

Le test limite la variation du poids marche–repos dans l'arbre de
présentation. La cadence se recalcule sur le mélange affiché. Les
déclencheurs de départ et d'arrêt continuent d'utiliser la vitesse
obtenue. Le banc utilise 140 ms pour le passage du seuil marche à zéro.

| Cas | Pic par image sans réponse progressive | Pic avec réponse progressive |
| --- | ---: | ---: |
| Arrêt en exploration | 21,57 cm et 53,78° | 8,56 cm et 18,96° |
| Arrêt après déverrouillage | 28,35 cm et 61,99° | 8,70 cm et 18,44° |

Les vitesses UCC sont identiques sur les 763 échantillons et Unity ne
rapporte aucune erreur. L'effet mesuré concerne la secousse à l'arrêt.
Le trajet cumulé de semelle dans les fenêtres proches du sol reste
presque identique (maximum 26,78 cm sur l'arrêt en exploration). Le
déverrouillage en mouvement présente toujours un pic de 21,92 cm lorsque
la couverture IK change. Le ralentissement du mélange ne résout pas ces
écarts. Il reste une option de test, avec données dans
`Lucian_Coherence_Contacts/final-pose/handoffs/exploration-brake/`.

En combat, le repère Animator confirme que les pieds se replacent en
sens opposés. Sur la diagonale avant gauche étudiée, le pied gauche
avance de 56 cm et le pied droit recule de 38 cm dans ce repère, avec
un déplacement latéral opposé. Le défaut provient du changement de
posture et demande un raccord qui montre comment les pieds se replacent.

## Durée du fondu d'arrêt sous verrouillage

Le test conserve la foulée sortante et demande un fondu visuel de 140 ms
vers CombatIdle. La première image de fondu apparaît au même instant dans
les deux recettes. La destination devient le seul état à 200 ms après le
début du test, contre 99 ms avec le fondu court. Les vitesses UCC sont
identiques sur les 763 échantillons. Aucun Root Motion ni erreur Unity.

| Mesure pendant le fondu | Fondu court | Fondu de 140 ms |
| --- | ---: | ---: |
| Pic de déplacement d'un pied en une image | 23,35 cm | 8,08 cm |
| Pic de rotation d'un pied en une image | 25,11° | 9,76° |
| Écart horizontal entre les positions initiale et finale du pied gauche | 61,16 cm | 60,54 cm |
| Écart horizontal entre les positions initiale et finale du pied droit | 59,35 cm | 59,12 cm |

Le fondu réduit la secousse mais étale un changement de posture d'environ
60 cm par pied. Le suivi des mêmes sommets de semelle près du plan enregistre
30,21 cm à gauche sur quatre intervalles et 44,91 cm à droite sur sept
intervalles. L'appui reste hors cible. Le raccord testé nécessite une
retouche d'animation, avec un pas de replacement identifiable et un appui
pendant le changement de posture. La seule source d'arrêt trouvée dans les
animations de Lucian est la marche commune. Il reste à comparer sa pose
avec les appuis de combat avant de choisir la source du raccord.

La durée de 140 ms reste une option de la recette Editor. Aucun changement
de contrôleur ni de profil utilisé en jeu. Relevés dans
`Lucian_Coherence_Contacts/final-pose/handoffs/locked-brake-fixed-fade/`.

## Arrêts et déverrouillage testés séparément

Quatre variantes du même parcours Play Mode terminent avec 763 images
chacune, sans erreur. Les entrées sont exercées avec la torche équipée et
un pas de capture de 60 Hz. La comparaison porte sur les poses finales
après UCC, pendant les 450 premières millisecondes de chaque cas.

| Parcours | Plus grand déplacement de pied de référence | Variante avec appuis et foulée sortante conservée |
| --- | ---: | ---: |
| Arrêt en exploration | 29,66 cm | 21,57 cm |
| Arrêt sous verrouillage | 79,72 cm | 23,35 cm |
| Déverrouillage en mouvement | 26,98 cm | 21,92 cm |
| Arrêt après déverrouillage | 29,33 cm | 28,35 cm |

À l'arrêt sous verrouillage, le zéro de direction fait sélectionner le clip
de repos au centre de l'arbre CombatLocomotion avant la fin du fondu. Fournir
les objectifs du seul état CombatIdle laisse encore cette rupture intacte.
Conserver la direction et le mélange d'allure du clip sortant pendant ce
fondu ramène le pic de rotation de 73,31° à 25,11°. Les deux états restent
qualifiés sur l'image du pic résiduel. Le poids de la foulée avant-gauche
descend alors de 56 % à 23 % en une image. Il reste donc un raccord trop
rapide à traiter, même avec les deux solutions IK disponibles.

Le maintien des paramètres est réservé à la recette Editor et à l'arrêt
sous verrouillage. Le déverrouillage en mouvement est un cas séparé. Les
relevés et traces sont archivés dans
`Lucian_Coherence_Contacts/final-pose/handoffs/`. Les assets du candidat
ont été vérifiés à l'identique et aucune validation en jeu n'est acquise.

## Fondu des appuis avec UCC sur la plateforme du test

La recette Editor charge les appuis d'arrêt et de repos en mémoire. Pour
chaque état, le solveur évalue sa propre phase, puis mélange les objectifs
avec les poids des clips renvoyés par Unity. Les objectifs passent par la
correction UCC existante. La recette est bornée au sol plat et aux états
de locomotion et Walk_Stop. Les assets du candidat sont identiques avant
et après le test.

Sur 2 144 images sans erreur ni Root Motion, les objectifs restent qualifiés
pendant les 180 images du parcours d'arrêt. À la dernière bascule de
Walk_Stop vers le repos, le pied gauche parcourt 7,1 mm. Le prototype qui
ne couvrait que l'arbre repos–marche donnait 17,6 cm à cet endroit. Le
candidat de référence avec UCC donnait déjà 0,63 mm sur cette seule bascule.

Le flux complet échoue encore au changement combat → exploration. Le pied
gauche parcourt 30,7 cm en une image, contre 0,95 cm pour la capsule.
Les objectifs sont actifs avant et après. Sur l'image du changement,
LitSpeed passe de 1,35 à 0,29 et la destination contient 78 % de repos.
La rupture ne se résume donc plus à l'activation de l'IK. Les essais
suivants doivent séparer un arrêt en exploration, un arrêt sous verrouillage
et la sortie du verrouillage en mouvement, puis traiter le changement de
poids à la bascule. Aucune intégration en jeu.

Relevés dans `Lucian_Coherence_Contacts/final-pose/stop-support-play/`.

## Fondu complet Walk_Stop → Locomotion

Un contrôleur temporaire reprend les deux motions et le fondu automatique du
candidat. Unity fournit à chaque échantillon le poids et la phase de chaque
état. Les trois variantes utilisent la même horloge, les mêmes poids et le
même avatar, avec une destination au repos. Les objectifs d'arrêt sont
échantillonnés avec Foot IK et leur phase est bornée à la fin du clip. Les
objectifs de repos utilisent la pose osseuse au sol mesurée précédemment.

| Variante isolée | Plus grand déplacement du pied par échantillon pendant le fondu | Déplacement à la dernière bascule | Plus grande rotation |
| --- | ---: | ---: | ---: |
| Réglages actuels du contrôleur | 18,12 cm | 18,12 cm | 11,38° |
| Objectifs mélangés aux phases des deux états | 1,60 cm | 1,75 mm | 0,94° |
| Même mélange avec pose d'arrêt recalée sur le plan | 1,46 cm | 1,58 mm | 0,94° |

Dans la dernière variante, la semelle la plus basse reste à moins de 0,73 mm
du plan sur les 121 poses. Le recalage vertical est commun aux deux pieds,
ce qui préserve l'écart de hauteur issu du geste d'arrêt. Le bassin suit
exactement la même pose dans les trois variantes. Les valeurs sont des pas
entre échantillons, avec 120 intervalles sur la durée de la source Walk_Stop.

Le test isole la sortie automatique vers Idle. Les entrées dans Walk_Stop,
les interruptions et l'interaction UCC sur terrain réel restent à éprouver.
La méthode n'est pas activée sur le candidat ou en production. Rapport
`Lucian_Coherence_Contacts/final-pose/stop-transition-goals.json`.

## Prototype d'appuis continus dans l'arbre repos–marche

La référence est échantillonnée sur le squelette de Lucian. Le prototype
conserve les orientations et positions horizontales des pieds de repos.
Il recale leur hauteur sur le plan mesuré du rig, car les os non corrigés
placent eux aussi les semelles sous ce plan. Les sept poids de marche à
121 poses chacun passent la vérification isolée. L'erreur de hauteur est
au plus 2,22 mm. L'écart horizontal au repos est inférieur à 0,03 mm et
aucun déplacement du bassin n'est mesuré.

En Play Mode, 2 143 images sont enregistrées sans erreur ni Root Motion.
Les 2 046 images avec objectifs qualifiés sont toutes associées à la
pose finale correspondante. Les vitesses médianes restent identiques
dans la précision affichée. Au repos stabilisé, les semelles se trouvent
à moins de 0,02 mm du sol sur les 60 dernières images. En revanche, la
plus grande fenêtre proche du sol à l'arrêt passe de 10,89 à 31,98 cm
de trajet cumulé. Les bornes de détection ont aussi changé avec la hauteur.
À la sortie de Walk_Stop, le pied gauche parcourt 17,55 cm en une image
alors que la capsule est immobile. Cette secousse coïncide avec le retour
des objectifs qualifiés.

Les deux assets du candidat ont été restaurés à l'identique. L'outil
Editor reste en lecture seule. Pour traiter ce raccord, il faut évaluer
les poses source et destination aux phases de chaque état pendant le
fondu. Le test à poids fixes ne couvre pas le changement de solveur
entre les états. Les relevés et les deux essais isolés sont archivés dans
`Lucian_Coherence_Contacts/final-pose/idle-support/`.

## Origine de la rupture IK entre repos et marche

L'audit Unity compare les mêmes clips, poids et phases de l'arbre d'exploration
sur l'avatar réel, avec la seule option Foot IK de l'état activée ou désactivée.
Six poids de marche sont mesurés à 121 poses chacun. À 22 % de marche, l'écart
entre les deux solutions atteint 18,53 cm au pied et 9,20 cm à la semelle.
À 98 % il atteint 27,16 cm au pied. Les hanches sont identiques sur tous ces
échantillons. Cette amplitude confirme que la sélection de la solution IK
peut provoquer une rupture de la taille de celle observée en Play Mode.

Sur la marche seule, les mêmes objectifs scriptés donnent la même pose avec
les deux valeurs de l'option Foot IK (moins de 0,002 mm d'écart). Réutiliser
simplement les objectifs natifs sur les mélanges partiels est toutefois
inadapté à ce clip de repos. Sa semelle native se trouve vers 0,007 m dans
le repère du modèle, contre 0,100 m pour le plan de référence du test.
En Play Mode, la pose de repos actuelle avec UCC présente environ 1 cm de
pénétration, cohérente avec le décalage des hanches mesuré précédemment.

La suite consiste à qualifier le repos depuis sa pose osseuse actuelle et à
mesurer le mélange continu avec les objectifs de marche sur le vrai avatar,
avant de l'appliquer au candidat. L'essai isolé ne qualifie pas encore le
raccord complet avec les états Walk_Start, CombatLocomotion ou Walk_Stop.
Rapport dans `Lucian_Coherence_Contacts/final-pose/idle-blend-fidelity.json`.

## Sortie du déplacement de combat

L'essai `play-cadence-mixed-gaits-contacts-20261105-173331` conserve la
direction de la foulée sortante pendant le fondu. Sur 2 146 images sans erreur,
les poids de cette foulée restent stables, mais la rupture de pose persiste.
Le pied droit parcourt 19,37 cm à la bascule, puis le pied gauche parcourt
30,11 cm en une image à l'intérieur de l'état Locomotion. La correction testée
a été retirée.

Dans cet essai, l'entrée dans Locomotion présente une image de marche seule
avec les objectifs IK qualifiés. Dès l'image suivante, le mélange devient
78 % Idle et 22 % marche, et ces objectifs sont désactivés. Le rythme des
mises à jour physiques diffère légèrement entre les deux exécutions, ce qui
rend une comparaison sur le seul changement d'état insuffisante. Le prochain
contrôle doit isoler cette bascule de qualification à paramètres identiques.

## Pose finale après UCC

Sur l'avatar de Lucian, la comparaison des 18 cycles isolés à 121 phases par
cycle donne des hanches et un buste identiques à la référence native, avec un
écart maximal de 3,83 mm sur les os et de 0,202° sur la rotation des pieds.
Ce contrôle isole le solveur des transitions et du déplacement UCC.

En Play Mode, la pose finale enregistrée après UCC révèle un décalage vertical
de 10 mm entre le pied et son objectif natif. UCC applique l'espacement de sa
capsule aux hanches après la résolution IK. Le candidat compense cet
espacement dans les seuls objectifs natifs qualifiés.

L'essai complet après correction couvre 12 parcours et 2 115 images, sans
erreur Unity. Les deux passes IK et la pose finale concordent sur les 1 733
images avec objectifs qualifiés, en exploration, en combat et avec la torche.
L'écart vertical pied–objectif natif est passé d'environ −10 mm à moins de
0,07 mm en valeur absolue. Les vitesses physiques médianes sont identiques à
0,001 m/s près. Le candidat reste isolé.

| Fenêtres proches de la surface | Maximum avant | Maximum après |
| --- | ---: | ---: |
| Exploration, marche lente stable | 27,22 cm | 15,96 cm |
| Exploration, raccord démarrage → marche lente | 18,61 cm | 20,08 cm |
| Combat, diagonale avant gauche avec torche | 3,35 cm | 1,40 cm |
| Arrêt, sortie de combat | 10,87 cm | 10,89 cm |

Ces fenêtres suivent les sommets de semelle communs proches du sol. La
correction verticale change leurs bornes de détection. Les valeurs renseignent
sur le glissement résiduel et nécessitent une comparaison visuelle par phase.
Le plus grand écart de marche lente après correction chevauche deux états
Animator. En boucle, le déplacement physique reste de 1,7966 m/cycle. La
reconstruction des appuis de la copie seule estime des vitesses de semelle
différentes selon les phases (médianes de 1,85 et 2,36 m/s sur le pied gauche),
avec une dérive latérale cumulée jusqu'à 1,97 cm dans les fenêtres étudiées.
Le contrôle suivant doit confronter les mêmes sommets et les mêmes phases sur
le clip isolé et en Play Mode avant une retouche de la cadence ou des poses.

Au changement d'état Walk_Start → Locomotion, le pied gauche se déplace de
12,51 cm entre deux images espacées de 17 ms, contre 2,46 cm pour la capsule.
La sortie de combat présente un mouvement du pied droit de 18,01 cm et 43°
en 15 ms, avec seulement 0,38 cm de déplacement physique. Le premier
raccord coïncide avec la reprise des objectifs qualifiés. Le second présente
une rupture avec ces objectifs inactifs de part et d'autre. Les deux raccords
doivent donc être évalués séparément.

Le contrôle de saut suivant totalise 1 553 images sans erreur ni Root Motion.
Les deux sauts produisent chacun 59 images en vol, sans objectifs IK natifs
maintenus. L'apex de la capsule est de 2,729 m sans torche et 2,728 m avec
torche, proche de la référence de 2,730 m.

Relevés et analyses reproductibles dans
`Lucian_Coherence_Contacts/final-pose/` (avant, après, fidélité osseuse et
reconstruction des appuis de marche). Les deux parcours graphiques terminent
sans erreur, avec 23 captures chacun. Huit paires sont conservées dans
`final-pose/visual/index.html`. Les phases diffèrent entre versions et la
cape reste déformée sur la plateforme du test. L'essai de remise à zéro du
tissu a été retiré du probe après les captures.

## Cadence des allures melangees et limites de pose (4 novembre, 21:32 UTC)

Diagnostic mesure : Unity partage la phase sur la duree ponderee des clips.
Moyenner les vitesses marche/course sous-estimait la cadence jusqu'a 10,14 %.
Exemple frontal a 50 % : duree 0,966667 s, distance de reference 2,731407 m,
vitesse native effective 2,825592 m/s. L'ancien calcul demandait une cadence
0,8986 au lieu de 1 pour ce mouvement de reference.

Le profil opt-in calcule desormais la distance ponderee par cycle divisee par
la duree ponderee. Exploration et combat, meme correction dans Evaluate et
CadenceForPresentedBlend. Les profils sans matchSimpleDirectionalBlend gardent
leur contrat precedent. Aucun changement de vitesse physique, de clip, de
controller, d'Animator.speed ou de chronologie des actions. Le prebuild refuse
les timeScale non unitaires et les bindings de cadence incompatibles avec ce
modele, au lieu de recalibrer silencieusement des reglages auteur.

Tests Unity passes : 4 tests, 192 cas reels du controller (32 directionnels et
160 phases/allures), erreur de cadence de reference <=2,4e-7. Ce gate valide
l'horloge par rapport aux references mesurees ; il ne prouve pas que chaque
reference de clip possede deja un appui physique correct. Verification native
independante des phases sur 18 cycles : ecart d'objectif <=1,96 mm, rotation
<=0,22 degre. Les offsets actuels ont le bon signe ; les supprimer ou les inverser
introduirait des erreurs jusqu'a plusieurs dizaines de centimetres. Cette
verification porte sur les objectifs natifs, pas encore sur tous les os apres UCC.

Recettes analogiques 210807/212006 : 2146/2144 releves, zero erreur. Stick
intermediaire, exploration/combat et diagonales avec torche. Vitesses obtenues
~1,489, 2,481, 3,476 et 4,468 m/s. Nouveau diagnostic de patchs : sommets communs
entre deux minima de semelle, couverture et duree explicites, aucune commande de
pose. Collecte dans +/-2 cm, analyse supplementaire jusqu'a +2 mm pour separer
les points souleves du contact de surface. Tous les chiffres restent provisoires :
le recorder LateUpdate n'a pas encore un ordre explicitement garanti apres les
dernieres ecritures de pose UCC. Aucun seuil ni fenetre n'est masque pour valider.

Plusieurs cas s'ameliorent (ex. FR a mi-stick : max de fenetre de surface ~8,39
vers 5,86 cm ; FL ~5,25 vers 3,39 cm), mais d'autres restent mauvais ou variables.
Marche lente pure encore jusqu'a ~27 cm sur cette mesure ; arret/Idle ~11 cm.
Ces nouveaux essais couvrent des situations absentes du parcours plein stick,
et interdisent de presenter les anciens maxima ~3-5 cm comme une borne globale.
Pas de validation artistique, pas de nouvelles captures de ce lot, pas d'activation
en production. Les mesures minimales de semelle sont souvent vers -1 cm en appui ;
UCC ecrit aussi les hanches dans LateUpdate. La contribution exacte doit etre
mesuree avant de modifier des reglages ou des trajectoires de clips.

Recette de non-regression 213151 : 1573 releves, deux sauts reellement exerces,
59 frames aeriennes chacun, zero maintien IK natif en vol ; apex 2,730125 et
2,729076 m, aucun changement de leurs parametres. Source/evenements intacts.
Preuves : Lucian_Coherence_Contacts/gait-timing/ (avant/apres, tests, phases,
regression et empreintes des entrees).

Prochain controle prioritaire : comparer les poses osseuses finales des cycles
seuls, natif Foot IK active versus calcul actuel Foot IK d'etat desactive,
puis mesurer les ecarts objectif/os apres les passes UCC. Verifier explicitement
l'ordre d'echantillonnage final. Les simples comparaisons de formules ne prouvent
pas cette fidelite. Ne pas retoucher les clips ou reconstruire l'Avatar avant
cette comparaison. Idle et transitions restent incomplets dans les donnees.

## Directions natives des courses diagonales (4 novembre, poursuite)

Un nouvel audit du contact de surface est confronte a Unity sur le vrai Avatar :
628/626 sommets, 240 intervalles par clip, seuls sommets communs a moins de
2 mm des minima des deux poses adjacentes, masques d'appui sources explicites.
Les vitesses de cheville seules ne suffisent pas a calibrer le contact au sol.

Sources fournisseur et copies donnent la meme direction : course FL environ
-30 degres au lieu de -45, FR environ +35 au lieu de +45. Vitesse de surface
mesuree ~4,02 / 4,07 m/s, contre 3,31 / 3,43 dans le profil. Differences entre
source et copie <0,02 m/s : pas de preuve d'une rotation perdue a la conversion.
La reconstruction Python independante confirme le decalage, mais reste un
calcul sur donnees preparees ; Unity constitue ici la verification primaire.

Correction ciblee : positions des deux clips dans le Blend Tree de course et
leurs references de vitesse dans le candidat. Aucun mouvement physique ou clip
n'est reecrit. Premier essai refuse par le gate puis restaure : quatre des
80 melanges requierent des sommets absents du premier jeu compact. Erreur
objectif/prototype jusqu'a 1,37 mm, meme si les hauteurs restent sous 4 mm.
Les seuils ne sont pas assouplis. Regeneration explicite du domaine de melange
avec empreinte du controller precedent, sources et rig inchanges, certificat
precedent valide ; remplacement des donnees seulement apres le gate complet.
Rollback sauvegarde aussi les donnees completes, pas seulement leurs hashes.
Preuves initiales : Lucian_Coherence_Contacts/surface-calibration/.

Application suivante terminee : 80/80 cas de preparation puis 80/80 de
comparaison runtime passent. Jeu compact regenere : 35 sommets gauche et 34
droite. Erreur de hauteur maximale 3,27 mm, ecart objectifs runtime/prototype
<=0,14 micrometre, rotations identiques. Le gate initial n'a pas ete assoupli.
Le certificat de donnees est invalide pendant la migration et restaure seulement
apres verification complete ; GUID, clips et courbes corporelles conserves.
Reapplication approuvee : aucun changement d'octets ou d'horodatages des trois
assets concernes, controle explicite dans surface-calibration-idempotence.txt.
Les empreintes de sources, rig et masques figent la mesure approuvee, sans
recalibration numerique a chaque installation ni ecrasement d'un reglage auteur.

Le premier parcours recalibre a revele une limite du calcul de cadence : sa
fenetre fixe de 45 degres additionnait plusieurs voisins, meme sur le point
avant exact. Sur des points non uniformes, cela accelere incorrectement le pas.
Le profil candidat utilise maintenant les deux voisins geometriques du
SimpleDirectional2D. Option explicite matchSimpleDirectionalBlend, desactivee
par defaut pour les autres profils. Deux tests sur le vrai controller, 16 angles
par allure, comparent les references aux poids Unity : 32 cas concordent a
0,001 m/s pres. Ce test concerne chaque allure separee ; le modele de vitesse
pendant leur melange doit encore etre confronte aux durees effectives des cycles.

Recette finale 205454 : 3049 releves, aucune erreur, 2096 applications de support,
zero Root gameplay. Vitesses stabilisees 5,2948 / 8,7402 m/s, identiques au
parcours precedent avec/sans torche. Maximum angulaire 32,82 degres, sans rotation
ajoutee par UCC sur le plan. Aucune nouvelle capture graphique de ce lot.
La mesure large proche-sol (qui inclut le deroule et du balancement) passe en
mediane d'environ 20,7 a 12,0 cm en FR et 28,0 a 13,4 cm en FL ; indicateur de
progression seulement, pas validation du glissement d'appui. Sous masques de
source, exploration encore jusqu'a 3,37 cm, avant combat 4,90 cm. Le critere
<=3 cm reste non atteint et les melanges diagonaux ne sont pas qualifies par
l'analyse qui exige un clip dominant a 98,5 %. L'observation en sequence et une
mesure runtime des vrais patchs de contact restent necessaires.

Preuves archivees dans surface-calibration : application, gates initial/refait,
tests Unity, idempotence et traces avant/apres interpolation. Controle des
ressources protegees repete : 229 identiques, cinq scripts deja volontairement
modifies, aucun manquant. Production toujours non activee, supportValidated=false.

## Raccord directionnel et correction de terrain (4 novembre, 20:21 UTC)

Cause runtime localisee par objectifs avant/apres le solveur : les changements
immediats de direction/allure produisent jusqu'a 139,72 degres par image dans
le parcours canonique (cible eloignee, aucun changement de yaw acteur). La
seule suppression de l'ecriture concurrente input n'elimine pas ce defaut.

LocomotionPresentationResponse borne maintenant la direction de presentation
et le changement marche/course. Candidat seulement : 360 degres/seconde et
0,18 seconde pour traverser toute la plage marche/course. La cadence est
recalculee avec l'allure et la direction effectivement presentees. Aucun
changement de vitesse physique. Les appels repetes dans une image repartent
du meme instant initial ; ils ne consomment pas plusieurs fois le delta acteur.
Reset sur sortie de locomotion, absence d'evaluation, changement de profil ou
desactivation. Sept tests Editor passent (temps local, pause et multi-appels).

Premiere recette avec raccord (201758) : maximum 53,22 degres. Instrumentation
montre un objectif natif variant de 14,01 degres transforme en 53,22 degres par
le redressement UCC du pied. Dans le seul chemin NativeFootSupportSet qualifie,
le solveur applique desormais l'inclinaison du contact physique UCC a la rotation
native, au lieu de perdre son pitch/roll avec LookRotation. Position, bassin,
poids/lissages de contact et comportement des autres actions restent sous UCC.
La normale employee est celle du contact UCC de la capsule : l'adaptation
individuelle de chaque pied sur des surfaces differentes reste a verifier.

Recette 202140 : 3050 releves, 2096 applications, zero erreur et Root gameplay
inactif. Maximum reel 35,51 degres (course), combat <=29,48 degres. Sur le plan,
UCC n'ajoute plus de rotation : ecart maximal objectif avant/apres 0,000018 degre.
Ces extrema incluent les retours rapides du pied et ne prouvent pas leur qualite
artistique. Vitesses UCC stabilisees 5,2948 / 8,7402 m/s, torche coherente.

Appuis source detectes : exploration medianes 2,20-3,00 cm, maxima 3,20-3,48 cm ;
combat avant encore jusqu'a 4,89 cm. Le critere d'appui <=3 cm n'est donc pas
atteint partout. Raccords Idle/actions,
pentes/marches/murs, temps, reseau et jeu restent a qualifier. Verifier notamment
le fallback IK lorsque des clips Idle non exportes participent au melange : le
controle des seuls echantillons support_blend=true ne couvre pas ce passage.

Preuves conservees : Lucian_Coherence_Contacts/direction-ownership,
direction-response, direction-surface, support-runtime-contacts,
support-runtime-contract et support-data-verification. Le dernier contient les
7 tests et le controle des 234 ressources protegees : 229 identiques, aucun
manquant ; cinq scripts changent pour les corrections courantes/precedentes.
Les sources d'animation/Avatar protegees restent identiques.

Recette de non-regression 202342 : 1573 releves sans erreur, 59 frames aeriennes
par saut, zero maintien IK natif en vol ; apex 2,729864 / 2,729896 m avec torche
rangee/active. Preuves dans direction-surface-jump. Le fallback vers des objectifs
non qualifies pendant les melanges Idle est maintenant refuse : retour au
solveur UCC habituel, sans forcer leurs poids a un. La fidelite visuelle de ce
raccord reste a qualifier ; ce garde-fou ne remplace pas l'export d'Idle.

Comparaison graphique 202743 / 202835 terminee : 1508 / 1510 releves, aucune
erreur ; 23 captures de chaque version, torche et deux sauts exerces. Galerie
locale : [avant/apres](Lucian_Coherence_Contacts/visual-comparison/index.html).
Le quadrillage represente des cases de 50 cm. Vues inspectees : FR, R et arret
torche. Le dernier montre les deux pieds poses et une posture de jambes stable
sur cette seule image ; les courses comportent des phases de vol prevues.
Les phases ne sont pas synchronisees : aucune conclusion pixel a pixel, ni
validation du glissement dynamique a partir de ces seules captures.

Limite visuelle nouvelle : cape fortement deformee, notamment en deplacement
lateral et apres arret. Ne pas assimiler cette deformation a une torsion du rig.
Verifier le reset Cloth apres placement initial de la fixture avant de retoucher
le gameplay. Unity 6000.6 fournit Cloth.ClearTransformMotion dans son XML local ;
l'essai n'a pas encore ete fait. Ombres de contact insuffisamment lisibles malgre
LightShadows.Soft : le sol quadrille seul ne leve pas cette limite. L'activation
en production reste bloquee par les appuis, les transitions et la recette complete.

## Integration candidate et limites runtime (4 novembre, suite)

Le calcul anatomique du prototype ci-dessous est maintenant exporte dans
NativeFootSupportSet et branche au candidat Cadence01 uniquement. Les 18 cycles
conservent 121 phases et 35/33 sommets utiles des semelles. Verification sur les
80 cas : ecart runtime/prototype <=0,14 micrometre en position et 0 degre en
rotation. Les empreintes lient sources, courbes, phases, rig, mesh et controller.
Les quaternions du rig sont normalises et quantifies a 1e-6 pour ignorer deux
differences de serialisation <2e-9, sans tolerer un changement de pose visible.
Application explicite et deuxieme application sans ecriture verifiees.

Le candidat utilise Foot IK d'etat desactive pour Locomotion/CombatLocomotion,
avec le solveur UCC unique et ses corrections conservees. Aucun nouveau moteur,
foot-lock, BakeMesh runtime ou changement de vitesse physique. Les jeux de
donnees perimes bloquent la validation ; les installateurs protegent le contrat.
Les dependances des scenes de build sont controlees avant build.

Recettes completes `play-cadence-support-contacts-20261104-200029` et
`play-cadence-contract-20261104-200203` : 3047/1597 releves, 2094/877 applications
du calcul, aucune erreur runtime. Deux sauts exerces, aucune retention IK native
en vol ; deux entrees Play dans le meme processus. Pas de capture graphique
sur ces recettes, donc aucune validation visuelle revendiquee.

La mesure dynamique contredit une qualification globale : pas maximal de pied
99,73 degres dans le repere Animator. Les incidents correspondent a des poids
directionnels changeant jusqu'a 85 % par image. Le bridge possede aussi une
ecriture directionnelle input susceptible de remplacer celle sur vitesse.
Garde ajoutee pour laisser la direction au chemin mesure dans le seul profil
calibre. La cible de fixture est eloignee pour eviter de la depasser pendant
les essais. Une nouvelle recette mesure les objectifs avant/apres UCC pour
distinguer conflit de pilotage, raccord rapide et correction de terrain.

Les rapports de 200029 montrent des appuis exploration medianes 2,17-2,98 cm,
maxima 3,18-3,58 cm ; certains cas combat restent plus mauvais. Les extrema de
hauteur de course incluent les vols intentionnels et ne prouvent pas seuls un
flottement. Production toujours non activee, supportValidated=false. Actions,
terrain, temps, reseau et validation visuelle complete restent ouverts.

## Cycles complets : contacts et orientations (prototype Editor)

Controle nouveau : 16 directions x 5 melanges marche/course, 241 poses par cas,
deux cycles sur le vrai Avatar/mesh et les arbres du candidat. Les sources de
forme sont echantillonnees sur 121 phases. Les sommets sont apparies entre
clips ; aucun BakeMesh, raycast, verrouillage au sol ou deplacement physique
n'est utilise dans la formule testee. Cette execution reste un prototype
Editor, pas une integration du nouveau calcul au runtime.

La conversion entre rotation d'objectif IK et rotation d'os est mesuree sur
les 18 clips : offset constant, deviation 0 degre. Employer directement la
rotation d'objectif pour reorienter une forme exprimee dans le repere de l'os
etait une erreur du premier prototype, corrigee avant les controles suivants.
Le premier rapport non calibre est conserve pour ne pas le confondre avec
une mesure correcte de la methode.

Les hauteurs seules ne suffisent toujours pas : l'interpolation quaternion
directe, et les orientations Humanoid deja melangees avec Foot IK actif,
peuvent produire une bascule de 179,77 degres au melange course FR/R (67,5 degres).
Le pas de correction Y atteint alors 16,26 cm. Convention de signe constante
essayee puis rejetee : elle deplace le probleme (45,69 degres, 10,65 cm).
Conserver uniquement les orientations musculaires supprime cette bascule mais
garde des raccords trop rapides. Aucun de ces essais n'est autorise au runtime.

La variante anatomique desactive le Foot IK de l'etat dans un controller
temporaire en memoire, puis combine les objectifs de rotation dans l'espace
logarithmique relatif a la pose musculaire courante. Une source seule conserve
son orientation native. L'ecart maximal d'une source au repere anatomique est
152,50 degres : le calcul reste a distance de la coupure a 180 degres dans ces
80 cas. La forme melangee sert ensuite a conserver les hauteurs de semelle des
sources en ne corrigeant que le Y des objectifs. Le controller temporaire est
detruit apres le test ; aucun controller sauvegarde n'est modifie.

Resultat de cette variante sur 80/80 cas : erreur de hauteur <=3,93 mm,
pas maximal d'orientation 11,38 degres (179,77 auparavant), pas de correction
Y <=1,26 cm, racine et bassin inchanges. La lecture des rotations FK dans
OnAnimatorIK correspond exactement a la pose FK evaluee (0 degre d'ecart) :
aucun deuxieme Animator n'est necessaire pour porter ce calcul au runtime.

Verification de Run_R original/copie, deux cycles et 480 intervalles : sans
Foot IK, rotations identiques ; avec Foot IK natif, ecart maximal 0,352 degre.
Il n'y a donc pas de preuve d'une perte de rotation dans cette copie. Les
raccords du melange doivent etre traites sans reconstruire arbitrairement le rig.

Prochaine application : preparer les donnees compactes des seuls sommets
utiles (identites communes), hauteurs et offset IK/os ; verifier GUID, sous-assets,
empreintes des clips/Avatar/mesh, courbes d'objectifs et phases avant application
explicite au candidat. Coupler les donnees, la rotation anatomique et la
desactivation du Foot IK integre des seuls etats qualifies : ne pas activer
une partie isolee de ce contrat. Garder une seule passe de presentation UCC,
la liberation pour actions/saut/cinematiques et l'absence de Root gameplay.
Puis verifier variations continues des poids, UCC reel, murs/pentes/temps,
actions et captures comparables avant toute integration en production.

Preuves : `Lucian_Coherence_Contacts/continuous-contact-prototype/`.
Les essais compares restent reproductibles dans LucianContactBlendAudit.
Les variantes rejetees sont limitees a l'Editor ; le runtime n'a recu aucune
nouvelle politique de quaternion pendant ces essais.

## Melange des contacts : faisabilite mesuree sur une pose

`LucianStrideBlendAudit.InspectWorstWalk` isole le melange marche R/BR a
112,5 degres, phase d'etat 0,733336. References sur le meme Avatar, phases
sources correspondantes (erreur d'alignement <=1/4800 cycle) : pied gauche
BR a +2,06 mm, R a -0,001 mm. Le melange place pourtant les deux semelles a
+39,81/+40,77 mm. Les objectifs IK root sont atteints : leur interpolation de
position fonctionne, mais elle ne conserve pas le contact sous les rotations
et la forme de pied melangees. Les sources n'appuient pas sur le meme endroit
du pied (talon/avant-pied), meme lorsque la jambe de support est la meme.

Le controle apparie les memes sommets de mesh entre les deux poses (628 a
gauche, 626 a droite), dans le repere de chaque pied. Leur forme melangee,
reorientee par l'objectif de rotation, predit la hauteur reelle a <0,001 mm
pres a gauche et 1,01 mm a droite. Cela explique pourquoi le seul alignement
des centres d'appui et le melange des objectifs de cheville ne suffisent pas.

Essai Editor sur cette pose : conserver la hauteur de semelle moyenne des
sources, en corrigeant seulement le Y des objectifs IK, donne +1,03/+9,90 mm.
Racine et bassin strictement inchanges ; angles internes des genoux environ
126/121 degres (avant 117/114). Aucun reglage physique, aucune translation
d'acteur, aucun asset d'animation modifie par cet audit.

**Ce test n'est pas une correction de cycle livree.** Ses deltas constants sont
calcules pour cette unique pose et ne doivent pas etre appliques ailleurs.
La prochaine etape est de verifier une representation continue des contacts
sur phases/poids de melange, sans BakeMesh au runtime ni verrouillage de pieds
dans le monde. Il faut mesurer l'erreur sur les cycles complets, les poids
intermediaires, les changements de direction et le passage marche/course,
puis refaire la recette UCC et les captures avec un sol lisible. Aucune
conclusion de validation visuelle ou de besoin de retouche artistique generale
ne peut encore etre tiree de ce seul cas.

Preuve : `Lucian_Coherence_Contacts/blend-contact-feasibility/report.json`.
Compilation et execution Unity de l'audit terminees avec code 0.

## Contact de reception pendant la preparation : cause confirmee

Audit instrumente `play-cadence-contract-20261104-180731` (sans rendu, UCC reel,
1575 releves). A t=49,17878 s, le callback OnCharacterGrounded(true) observe
JumpPhase=Takeoff, impulseApplied=false et leftGround=false. Au meme instant,
OnCharacterLand(0,6000004) fait passer JumpPhase a Landing avant toute impulsion.
La pile d'appel remonte a UltimateCharacterLocomotion.UpdateGroundState puis
DetectGroundCollision : il s'agit de la reprise de contact UCC, pas d'un
Animation Event du clip. Le script de saut est l'unique ecrivain LandingTrigger.

Correction ciblee en verification : un contact pendant la preparation ne peut
pas devenir la reception de ce saut. OnGroundedChanged(true) efface l'indication
de vol devenue obsolete avant impulsion ; OnLanded exige une impulsion et un
depart effectif du sol, ou une reception deja anticipee. Cette derniere continue
a recevoir son contact reel. Aucune modification de hauteur, force, gravite,
clips, seuil de depart, fenetres d'input ou duree de reception.

Les modifications preexistantes de buffering/coyote sont conservees. Sept tests
Editor de contacts passent, dont vraies receptions, preparation, impulsion encore
au sol, reception anticipee et callbacks tardifs apres reset.

Controle UCC corrige `play-cadence-contract-20261104-181118`, Domain Reload
desactive : 1 574 releves, zero erreur, 59 releves aeriens pour chaque saut,
878 applications des objectifs root en locomotion et zero maintien IK natif
en vol. Le contact parasite observe au premier depart laisse maintenant
JumpPhase=Takeoff, puis l'evenement airborne est emis apres l'impulsion.
Hauteurs mesurees : 2,730134 m sans torche et 2,730189 m avec torche ; saut
torche avant correction 2,729646 m (ecart <0,6 mm). Le premier saut passe de
zero decollage a la trajectoire fonctionnelle. Les deux segments de reception
se terminent au sol avec reprise du mouvement maintenu (~5,298 m/s).

Preuves : `jump-contact-before/`, `jump-contact-after/` et
`jump-contact-tests.xml` sous `Lucian_Coherence_Contacts`. Ces executions sont
numeriques sans capture de rendu ; ne pas les utiliser comme recette visuelle.
Controle Domain Reload active `play-cadence-contract-20261104-181604` :
1 566 releves, zero erreur, 58 releves aeriens par saut, zero maintien IK natif
en vol et zero reception prematuree avant impulsion. Apex 2,729081/2,729978 m.
Le fichier EditorSettings retrouve exactement son empreinte avant le test.
Rapport et traces dans `jump-contact-reload/` ; le log confirme le rechargement
des assemblies a l'entree en Play. Les tests numeriques ne remplacent pas les
captures comparables du vrai rendu, qui restent a produire pour ce lot.

Le candidat reste non livre : melanges,
raccords, actions, terrain, temps, reseau et rendu du jeu restent a qualifier.

## Dernier lot en verification : 4 novembre 2026

Production non activee. Le candidat comporte maintenant 17 cycles Stride03 et
Common Run Stride02, issus de la reference native Humanoid IK. Les 18 copies
passent leur prevalidation individuelle ; la vitesse de cadence exploration
mesuree reste 4,96776 m/s. Les phases gauche/droite sont alignees dans les
arbres de base du candidat, sans retimer les sources ni les actions.

Le controle de 32 cas directionnels sur le vrai avatar et le controller complet
montre une limite du melange Humanoid : des clips ayant individuellement un
appui au sol peuvent relever les deux pieds une fois melanges. Un essai exporte
les objectifs IK natifs dans le repere du personnage et les melange dans ce
repere, uniquement pendant les cycles qualifies hors transition. Il ne deplace
jamais le personnage et ne verrouille aucun pied dans le monde.

Reconstruction de chaque clip seul, 479 intervalles et 480 callbacks par clip :
18/18 passes, erreur maximale 2,66 mm aux pieds, rotation mesuree identique.
Cela verifie la representation des objectifs, pas la qualite des melanges.

| Marche, 16 directions | Phases alignees | Objectifs root melanges |
| --- | ---: | ---: |
| Pire fraction avec les deux semelles >3 cm | 14,9 % | 7,5 % |
| Plus grande hauteur du pied le plus bas | 4,39 cm | 3,98 cm |
| Penetration minimale | -0,95 cm | -1,30 cm |

Amelioration partielle, insuffisante pour livraison. Les phases de vol de course
ne sont pas automatiquement des erreurs : elles doivent etre comparees aux
appuis prevus du geste. Le test UCC reel du nouveau lot est lance separement.
Les tests de sauts precedents ne prouvent pas encore son absence de regression.

Preuves archivees : `Lucian_Coherence_Contacts/stride03-blends/` : manifeste,
rapports avant/apres alignement, objectifs root et reconstruction des effecteurs.
La suite du document decrit les essais anterieurs, avec leurs limites.

Test UCC `play-cadence-contacts-20261104-175638` : 3 000 releves, 17 segments
de trois secondes simulees, aucun message d'erreur. La sonde refuse maintenant
un segment trop court ou une demande de mouvement qui n'a pas ete exercee.
La premiere execution `175342` avait saute presque toute la marche pendant un
chargement de rendu ; son absence d'erreur ne constitue donc pas une validation.
Le temps simule est maintenant utilise pour les segments, et la session batch
est limitee a 60 images/s. Aucun reglage projet n'est sauvegarde par ce changement.

Sous-fenetres d'appui detectees, en tenant compte du cycleOffset reel :

| Scenario | Contacts retenus | Chemin median | Maximum |
| --- | ---: | ---: | ---: |
| Exploration normale | 5 | 2,48 cm | 3,08 cm |
| Sprint | 10 | 2,33 cm | 3,26 cm |
| Torche, normal | 6 | 3,01 cm | 3,18 cm |
| Torche, sprint | 10 | 2,12 cm | 3,10 cm |

Le seuil reste depasse sur certains contacts. Les fenetres de proximite au sol
incluent aussi approche/depart du pied et ont des chemins nettement plus grands ;
elles ne doivent pas etre confondues avec les appuis sources. Le lecteur de
traces utilise maintenant les masques propres aux cycles et leur decalage reel,
et ne certifie aucun appui pour un melange sans source dominante >=98,5 %.
Les noms des essais de combat decrivent le stick : la cible proche peut etre
depassee, ils ne certifient pas les huit directions locales du personnage.

Preuves : `Lucian_Coherence_Contacts/stride03-ucc/` (traces, rapport, analyse,
captures ponctuelles). Les captures montrent le vrai modele mais n'ont pas de
repere de sol lisible et ne constituent pas une validation visuelle finale.
Empreintes protegees recontrolees : 231/234 fichiers identiques ; seules les
trois adaptations de code deja identifiees different, aucun fichier manquant.
Le vieux binding SupportIK est desormais refuse pour eviter un retour au clamp
qui degradait le balancement du pied. Changer de clip invalide les effecteurs
exportes et les offsets du cycle ; leur regeneration reste explicite.

Controle de non-regression `play-cadence-contract-20261104-175943` : compilation
Unity reussie, 1 500 releves et 865 applications des effecteurs root observees.
Le saut torche fournit 56 releves aeriens, sans conservation IK native en vol.
Le premier saut reste au sol : **test refuse**, malgre son animation de depart.
Ce lot n'est donc pas qualifie pour remplacer la configuration de production.

Reference avec la meme sonde a 60 images/s :
`play-baseline-contract-20261104-180237`, 1 495 releves, zero erreur,
25/49 releves aeriens pour les deux sauts. La trace permet de localiser le conflit :
Jump_Start est quitte vers Jump_End avant la fin du depart dans les deux cas.
Dans la reference, la transition commence vers phase 0,207 et l'impulsion
vient juste d'etre appliquee ; dans le candidat, elle commence vers 0,168 et
aucun deplacement vertical n'est produit. Clips/vitesse du Jump_Start identiques,
pas de multiplicateur de cadence sur cet etat. Ne pas masquer ce resultat en
changeant les reglages de saut ou en declarant la sonde non representative.

Piste a instrumenter : PlayerScriptedJumpController.OnLanded accepte une
reception tant que jumpActive, meme avant takeoffImpulseApplied. Les transitions
AnyState vers Jump_End/Landing_Hard sont pilotees par LandingTrigger. La trace
demontre la sortie prematuree ; elle ne prouve pas encore quel emetteur a pose
ce trigger. Prochain controle cible : evenements UCC de contact/reception et
parametres de presentation pendant la preparation, puis correction du conflit
d'autorite demontre, sans retoucher hauteur, impulsion ou timing auteur.

Preuves refusees conservees dans `stride03-contract-incomplete/`, reference
dans `stride03-contract-reference/` sous `Lucian_Coherence_Contacts`.

Objectif actif du 2 novembre 2026 : rendre naturels et coherents les appuis,
le bassin, les jambes et le buste de Lucian, sans modifier son gameplay physique.
Ce document est le suivi de verification, pas une declaration de livraison.

## Etat de reference observe

- Prefab gameplay : `Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab`.
- Avatar effectif : `Lucian_CC5_Embed/Lucian_CC5_Character_Model.Fbx` ; valide
  Humanoid dans Unity. Cette validite ne prouve pas encore la qualite du retarget.
- Controller : `Lucian.overrideController`, base `Lucian_ImpactLab.controller`.
- Corps et pantalon effectivement lies aux os Humanoid de jambes evalues.
- Trois couches : Base Layer, Upper Body Flame, Moving Legs.
- `Lucian.asset` ne reference aucun LocomotionPresentationProfile : le chemin
  calibre preexistant n'est donc pas actif pour ce personnage.
- Le bridge conserve sa conversion vitesse-vers-blend et son maximum avec
  l'intention d'input. Sous lock, un relachement peut aussi forcer visuellement
  Idle avant la fin de la deceleration physique.
- Moving Legs est une copie independante des arbres de locomotion, masquee aux
  jambes sans bassin. Elle recoit une vitesse en m/s tandis que l'arbre copie
  attend les valeurs de blend du controller. Ses phases ne sont pas synchronisees
  avec la couche principale ; ses etats n'utilisent pas le parametre de cadence.

## Mesures Unity du premier lot

Commande explicite et sans modification des assets gameplay :
`Lit/Animation Presentation/Audit Lucian Controller Coherence (Read Only)`.
Rapport : `Library/LucianCoherence/controller-baseline.json` ; images numerotees
au meme endroit. Le rapport contient les sources, remplacements effectifs,
GUID/fileID, consommateurs, seuils, Foot IK et parametres de cadence.

Les sondes de parametres sont intentionnellement controlees. **Elles ne mesurent
pas une vitesse UCC de 6,5 m/s en jeu.** Les deux couches sont placees a des phases
identiques pour isoler le masque et les parametres ; cela ne mesure pas leur
desynchronisation temporelle reelle. Comparaison avec la pose complete :

| Sonde | Ecart maximal des pieds | Difference d'angle de genou | Bassin |
| --- | ---: | ---: | --- |
| Marche, blend 1,35 ; vitesse de sonde 1,036 | 18,2 cm | 34,0 deg | inchange |
| Marche, blend 1,35 ; vitesse de sonde 6,5 | 62,8 cm | 119,8 deg | inchange |
| Course, blend 3,25 ; vitesse de sonde 6,5 | 0 | 0 | inchange |
| Combat directionnel, memes phases et melanges | 0 | 0 | inchange |
| CombatIdle avec jambes en strafe droit | 54,4 cm | 46,5 deg | inchange |

Dans la deuxieme sonde, la couche principale joue effectivement Common Walk,
alors que Moving Legs joue Common Run a poids 1. Ceci prouve un conflit de
presentation possible avec les parametres actuels, pas encore sa frequence en
conditions de jeu. Les poses sont stables avant/apres rendu ; la capture ne
reinitialise pas les transformations mesurees. La cape peut masquer un appui.
Aucune de ces mesures ne certifie les semelles ou le glissement au sol.

## Ordre des corrections et preuves requises

1. Reproduire avec le vrai Lucian/UCC dans une scene de test, enregistrer vitesse
   obtenue, intention, LitSpeed, MovingLegs*, clips/poids et phases des couches,
   bassin/pieds et autorite active. Marche/course et huit directions, depart,
   arret, changement d'allure ; torche active/rangee. Identifier la frequence
   reelle du conflit ci-dessus et les autres causes avant de changer des clips.
2. Restituer une seule pose coherente bassin-jambes en locomotion normale.
   Verifier une suppression du doublon de presentation dans ces etats plutot
   que normaliser des jambes sur un bassin provenant d'un autre mouvement.
   Conserver le besoin distinct des actions mobiles du haut du corps ; traiter
   leurs raccords explicitement. Ne pas generaliser le changement aux compagnons.
3. Calibrer la presentation sur la distance UCC obtenue sans changer sa vitesse
   ni ses accelerations. Mesurer sur l'avatar CC5 les cycles effectifs, leurs
   appuis et leurs phases ; verifier les seuils avant d'activer un profil. Pas
   de validation automatique sur une simple estimation ou sur les anciens chiffres.
4. Auditer/retoucher seulement les clips encore defectueux, leur provenance,
   extractions Humanoid, offset de rotation, boucles et raccords. Comparer la
   source non aplatie au resultat ; conserver mouvements corporels et evenements.
5. Verifier masque/courbes torche, IK et correction de buste LateUpdate, seuls
   puis combines. Eviter une compensation de rotation appliquee deux fois.
6. Tester locomotion puis saut/reception, esquive, skills/combo, Hurt/mort,
   interruptions et cinematiques ; collisions, pentes/marches, mur, pause et
   ralenti ; solo/host-client. Verifier retour d'autorite et inputs maintenus.
7. Comparer avant/apres avec vrais meshes/rendu, puis dans le jeu. Conserver les
   captures et donnees, les ecarts residuels et toute retouche artistique requise.

## Protections et livraison

Avant toute correction : empreintes des assets et reglages proteges (saut,
esquives, torche, UCC, skills et Timelines), plus sauvegarde des seuls objets
reellement modifies. Les modifications Git deja presentes sont conservees.
Aucun Root gameplay, teleportation corrective, changement d'impact ou de
fenetre de combat n'est autorise pour masquer une erreur de presentation.

La livraison reste ouverte jusqu'aux preuves de pose et d'appui, raccords,
cadence reelle et non-regression dans le vrai jeu. Compilation ou absence
d'erreur Console ne constituent pas cette preuve. Le premier lot ne modifie
encore aucun clip, controller, avatar, profil physique ou script runtime.

## Reference figee du premier audit

`Lucian_Coherence_Baseline/controller-baseline.json` contient les 136 feuilles
et les mesures. `protected-resources.json` conserve les empreintes de l'etat
courant, y compris les modifications auteur presentes avant toute correction.
Les captures sont prises a la phase de plus grand ecart des pieds ; le cote
gauche conserve le corps complet et le cote droit applique Moving Legs.

- [Marche avec melange Idle/jambes](Lucian_Coherence_Baseline/0.png)
- [Buste marche / jambes course](Lucian_Coherence_Baseline/1.png)
- [Idle combat / jambes strafe](Lucian_Coherence_Baseline/7.png)

Le PNG est une reference de pose sur fond neutre. Il ne remplace ni un parcours
sur sol, ni le controle des semelles, ni la qualification artistique du rendu.

## Reproduction UCC et candidat d'autorite (2 novembre, 21 h)

Le parcours reel utilise le prefab gameplay dans la scene de test Fracture,
sur une grande plateforme temporaire. Placement initial UCC seulement ; puis
entrees manette simulees par la chaine normale, sans correction de position.
Scenarios : arret/depart, marche, sprint, huit orientations sous lock,
torche rangee/equipee et retour a Idle. Les etiquettes des huit orientations
representent les commandes de stick, pas une orientation monde garantie.

Premier passage exploratoire 194901 : la torche ne pouvait pas s'equiper dans
l'inventaire vide de cette fixture ; ne pas utiliser ce passage pour la torche.
Le pre-requis de l'inventaire a ensuite ete corrige avec ResetFlameToMax et une
assertion de l'etat effectivement equipe. Aucun inventaire campagne n'est ecrit.

Reference complete : `play-baseline-20261102-195456` (1 182 releves, aucune erreur).
Le conflit est maintenant reproduit avec de vrais deplacements, plus seulement
avec des sondes : en marche stabilisee (5,2948 m/s), 32/32 echantillons stables
ont Common Walk sur le corps et Common Run sur les jambes ; idem torche active.
Course stabilisee : 8,7372 m/s, deux couches Run mais phases independantes.
Animator.applyRootMotion reste false pendant tout le parcours.

Candidat : `play-candidate-20261102-195854` (1 214 releves, aucune erreur).
PlayerLocomotionSettings.preserveWholeBodyLocomotion permet au corps complet
de piloter les jambes pendant Locomotion/CombatLocomotion/CombatIdle, depart,
arret et rotations. Les autres etats gardent leur contrat de jambes mobiles.
Le reglage est opt-in : false par defaut pour les autres acteurs et encore
absent de Lucian.asset. Seule une copie runtime de CharacterData l'active pour
cet essai. L'activation reelle est explicite, via commande Editor separee ;
elle n'a PAS encore ete lancee. Pas de correction des clips dans ce lot.

La couche Moving Legs reste a zero pendant les etats de locomotion du candidat
(assertion du vrai runtime). Vitesses stabilisees libres et torche : marche
5,2948 m/s dans les deux passages ; course 8,7372 et 8,7402 m/s respectivement,
identiques avant/apres a la precision affichee. Les portions de lock ne sont
pas des itineraires strictement identiques (integration autour d'une cible et
verrou d'equipement de torche au changement d'etape) : aucune equivalence de
distance/orientation de toutes ces portions n'est revendiquee.

Les captures avant/apres sont des instantanes par etape, pas des poses a une
phase strictement identique. La cadence reste non calibree. Les traces de
semelles montrent encore des penetrations et quelques frames avec les deux
pieds trop hauts ; ce candidat ne certifie donc pas les appuis ou l'absence de
glissement. Il reste en test pour ne pas activer une correction isolee qui
ameliorerait les torsions en aggravant le patinage.

Preuves figees : `Lucian_Coherence_UCC_Baseline/` et
`Lucian_Coherence_UCC_Candidate/` : report.json, samples.csv, soles.csv et trois
captures ; comparaison des vitesses dans authority-comparison.json. Le CSV
semelles utilise son propre temps relatif ; ne pas aligner les deux CSV sans
synchronisation explicite. Les statistiques par etape derivees approximativement
des temps de fin ne servent pas de critere d'acceptation.

Le controle d'empreintes conserve les 234 ressources de reference, sauf les
TROIS scripts intentionnellement modifies : PlayerSettings.cs (champ opt-in),
LitOpsiveLocomotionBridge.Presentation.cs (lecture du champ),
PlayerMovingLegsOverlay.cs (autorite complete). Prefab, Lucian.asset, avatar,
clips, parametres physiques et scripts de saut existants conservent leurs
empreintes. Compilation Unity runtime/Editor reussie ; defaut temporaire du
harness lie aux anciennes API InstanceID corrige pour Unity 6.6.

Prochaine verification : mesures des cycles et de leurs contacts sur le CC5,
avec une horloge commune pour la trace UCC/poses. La presentation doit couvrir
5,30/8,74 m/s sans changer la physique ni certifier les anciens profils. Tester
ensuite une correction complete d'autorite + cadence + poses, puis les actions
et les autres conditions du plan avant activation dans le jeu.

## Cadence et diagonales : candidat mesure (2 novembre, deuxieme lot)

18 cycles effectifs mesures sur le CC5, avec/sans l'IK Humanoid natif ; 18
sources Root uniques resolues, de meme duree. Les clips actuels Common Walk/Run
durent 1,0/0,8 s ; leurs trajectoires sur Lucian valent 2,0714/5,3004 m/s.
Les anciens chiffres calcules sur 2,0/1,6 s ne decrivent plus ces ressources.
Ne pas les reutiliser ni attribuer cet ecart a un changement de rig seul.

Candidat distinct dans AnimationPresentation/LucianCoherencePreview/Cadence01 :
controller, override, donnees, profil, prefab et scene copies. Etat auteur de
la production intact ; GUID source, physique, saut et esquives preserves.
Le profil choisit et melange marche/course selon la vitesse obtenue, puis
applique la cadence aux seuls etats de cycle. Le stick reste l'intention
physique ; il ne fournit pas une vitesse animee supposee. Flag optionnel false
sur les profils existants. supportValidated reste false ; aucune certification
des appuis ni activation production n'est impliquee par validated=true
(validation des donnees de mesure seulement).

Essai cadence 201533 : 1 222 releves sans erreur, UCC aux memes vitesses,
cycle Run quasi complet a 5,2948 m/s et cadence 1 ; sprint 8,7372 m/s,
cadence 1,6484. Torche equipee verifiee. Le monitor specialise empeche les
anciennes ecritures d'input concurrentes sur la direction calibree.

Second conflit confirme : les enfants diagonaux des arbres SimpleDirectional2D
sont sur les coins (+/-1,+/-1), mais la direction fournie est sur le cercle
unitaire. Cela ajoute de l'Idle dans les deplacements intermediaires. Dans la
seule copie du controller, les positions directionnelles ont ete normalisees.
Essai 202557 : 1 217 releves sans erreur ; le maximum d'Idle parasite tombe a
zero dans combat_R/combat_BR/combat_FL (contre jusqu'a 18,8/27,9/8,5 % avant).
Seconde normalisation : empreinte strictement identique, donc idempotente.

Les traces de pose et de pilotage utilisent maintenant le meme unscaled_time
et le meme numero de frame : 1 217/1 217 correspondances exactes. Les mesures
restent non conformes : jusqu'a 17 cm de penetration de semelle sur les cycles
communs et 23 cm dans certains melanges directionnels. Pas de preuve d'absence
de glissement, ni de qualification artistique de ces appuis. La correlation
de phase montre des decalages importants, notamment Run_BR/Run_R ; Walk_BR
reste sous le seuil du precedent audit (0,738). Aucun decalage de phase n'a ete
applique aveuglement : contacts et pose doivent etre traites ensemble.

## IK et contrat des copies a retoucher

CharacterIK UCC est deja actif, avec IK Pass sur Base Layer. Ses raycasts et
ses objectifs IK doivent donc etre inclus dans la validation du rendu final.
L'essai distinct 203952 prepondere les pieds a poids 1 dans cet IK existant
(1 207 releves sans erreur, aucun saut dans ce passage). Il ameliore certains
passages, mais laisse des penetrations de 13-17 cm. Ce reglage n'est PAS une
solution retenue et n'est pas applique en production. Aucun nouveau foot-lock
runtime n'a ete ajoute.

Comparaison des sources Root/Inplace communes : bassin Y identique a moins
d'un micrometre, root physique Y constant dans l'echantillonnage. Les memes
bas de semelles negatifs existent lors du retarget de la reference Root sur
ce rig. Cette evidence ne justifie donc pas une reinstallation globale ni
un aplatissement de RootT/RootQ. Les differences horizontales de pose restent
a examiner ; cette comparaison ne certifie pas la conversion complete.

Le contrat Humanoid est verifie dans Unity 6.6 : HumanPoseHandler expose les
positions/rotations IK en repere du corps. Les courbes LeftFootT/RightFootT
stockent les objectifs de base du pied ; la position osseuse IK inclut aussi
leftFeetBottomHeight (ici 0,05975 m) et sa rotation. Sur Common Walk frame 0,
la prediction corps + FootT correspond au resultat IK apres cet offset.
La pose FK et l'objectif IK d'origine different de plusieurs centimetres.
Les anciennes retouches de muscles seuls ne revalident pas ces objectifs.

Prochaine correction : retoucher une copie pilote sur le CC5, conserver les
poses du bassin/buste, enregistrer muscles ET objectifs IK coherents via
HumanPoseHandler, puis verifier FK, IK natif et le vrai UCC. Calculer le sol
cible depuis le support physique de la capsule, pas depuis un minimum de pied
arbitraire. Verifier le sampling manuel contre BakeMesh avant de generaliser.
Suivre un point de mesh fixe durant un appui pour distinguer patinage et
roulement naturel talon/pointe ; le seul point le plus bas change de sommet.
Puis harmoniser phases et raccords, tester les actions/temps/terrain/reseau,
et activer seulement le resultat combine qualifie.

Preuves figees : Lucian_Coherence_Cycles (mesures et contrat),
Lucian_Coherence_Cadence (controller normalise, CSV/semelles et images),
Lucian_Coherence_IKExperiment (essai IK non retenu). Les CSV semelles de ce lot
contiennent les frames communes. Le header du recorder a ete etendu en fin de
ligne ; les colonnes historiques gardent leur ordre.

## Pilote d'appuis : validation entre cles et conflit de torche

Le calcul de semelles est compare a BakeMesh : ecart maximal de 0,48 micrometre.
Le sol physique mesure reste root + 0,10000038 m. Aucun reglage de capsule change.

Le premier pilote de muscles est rejete. Ses seules cles semblaient correctes,
mais le controle intermediaire revelait jusqu'a 14,4 cm de penetration :
decomposition de torsion cuisse/mollet discontinue pres de la jambe tendue.
Conserver seulement la torsion source apres inversion n'est pas une solution :
les autres angles et le centre du corps sont couples. Ce pilote ne peut plus
etre branche par sa commande Editor. Preuve :
`Lucian_Coherence_SupportPilot/rejected-muscle-report.json`.

Nouvelle copie `CoherencePilot/Lucian_Common_Run_SupportIK.anim` : muscles,
corps, evenements et duree source conserves. Seuls les objectifs FootT/Q sont
recalcules ; requiert le Foot IK Humanoid natif de l'etat. Aucune immobilisation
de pied dans le monde et aucun moteur physique ajoute. Avant installation,
le solveur est mesure sur le vrai avatar, puis son faible residu est corrige
dans les objectifs, sans reextraire les muscles. Resultat hors gameplay :
ecart cheville cible <1 micrometre, buste et rotations des pieds inchanges,
translation Root nulle. Controle persiste sur quatre cycles : penetration max
0,86 mm. Preuves : `Lucian_Coherence_SupportPilot/native-report.json`,
`native-subframes.csv` et `Library/LucianCoherence/support-native/persisted-cycles.csv`.

Ce resultat ne suffit PAS en gameplay. Essai UCC 212641 : 1 210 releves sans
erreur ; semelle la plus basse jusqu'a -7,9 cm en course, -17,6 cm avec torche.
Essai 212731, demande de desactivation IK UCC : 1 222 releves sans erreur,
jusqu'a -7,0/-17,0 cm respectivement. **Controle invalide pour conclure sur
l'absence d'IK** : l'assertion ajoutee dans 213627 constate une reactivation avant
le premier releve. Les retries d'attachement de LookSource peuvent reactiver
CharacterIK meme apres enabled=false. Le prochain controle retire uniquement
son instance runtime initialisee et verifie son absence pendant chaque releve.
Ce controle est maintenant termine : essai 213737, 1 219 releves et autant de
poses alignees exactement par frame, zero frame IK UCC actif, zero erreur.
Course : minimum -0,8 mm rangee / -0,1 mm active. Ce resultat isole le conflit
des objectifs/poids avec CharacterIK, pas un defaut restant de cette copie seule.
Idle sans correcteur UCC s'enfonce de 9,3 cm : supprimer globalement l'IK serait
une regression. Les cycles directionnels non retouches restent defectueux.

Essai 213846 : IK UCC present et OverrideFootIKWeight=1 dans la fixture seulement,
1 221 releves tous avec composant actif, zero erreur. Course a -1,06 cm stable
rangee/active, Idle a -1,06 cm, physique 5,2948/8,7372 m/s conservee. Entrees et
melanges encore jusqu'a -2,55 cm ; directions non retouchees jusqu'a -13 cm.
CharacterIK.PositionLowerBody ecrit SetIKPositionWeight/SetIKRotationWeight,
et son poids par defaut peut revenir a zero alors que les objectifs natifs
sont necessaires au retarget de cette copie. Le poids global 1 n'est pas livre :
il faut borner la cooperation aux cycles valides et verifier restitution des
actions, sauts, esquives et cinematiques. Aucun nouveau foot-lock.

Preuves UCC sauvegardees sous Lucian_Coherence_SupportPilot/native_only et
ucc_weight1 ; resume runtime-summary.json. Le minimum du point le plus bas
ne mesure toujours pas le patinage horizontal d'un meme point de contact.

Autre conflit constate : Upper Body Flame n'a aucun AvatarMask dans le vrai
Lucian_ImpactLab.controller, ni dans Player_Model. Le test historique de torche
suppose un AnimatorController direct et ne couvre pas le controller override
actuellement utilise. Un masque explicite bras/doigts, Root/Body/jambes/IK pieds
exclus, avec chemins Transform du rig effectif, est prepare dans Cadence01
seulement. Essai 213128 : 1 219 releves sans erreur, meme UCC. La degradation
supplementaire avec torche disparait : minimum en course -8,4 cm rangee contre
-7,7 cm active (phases de captures distinctes), medianes -1 cm dans les deux cas.
La locomotion reste non conforme, et aucun changement production n'est autorise
par ces validations partielles. Prochaine isolation : controller entier sans
scripts, puis ecrivains runtime/IK et phases des cycles directionnels.

Empreintes de preservation reverifiees : 231/234 ressources protegees identiques,
dont tous les clips/imports/prefabs controles et reglages proteges. Les trois
differences attendues sont PlayerSettings, le bridge de presentation et
MovingLegsOverlay (ajouts opt-in documentes). Aucun original reimporte/reecrit
par la preparation de ce pilote.

## Dix-huit cycles et cooperation IK limitee (23h03)

Les 18 cycles mesures passent la preparation SupportIK. Maximum d'ecart de
cheville cible : 0,153 mm ; haut du corps : 0,089 mm ; minimum de semelle sur
deux cycles persistes : -1,573 mm ; mouvement Root extrait : zero. Muscles et
evenements source conserves. Les copies et leurs GUID restent separes des FBX.

La marche Walk_BL etait bloquee par un residu Root XZ de 3,25 cm (Bake XZ inactif).
Le pilote retire seulement cette trajectoire mesuree de RootT.x/z, puis bake XZ,
sans modifier RootT.y ou RootQ. Controle de fidelite au rendu Inplace precedent :
ecart haut du corps 0,089 mm, rotation des pieds identique. Les 17 autres copies
ne demandent pas ce retrait. Aucune retouche des imports/originaux.

LitPresentationCharacterIK utilise le meme solveur UCC et ses correcteurs de
terrain ; apres son appel, il conserve les poids finaux des pieds a 1 seulement
pour les cycles explicitement qualifies en Locomotion/CombatLocomotion. Les
parametres et lissages internes du solveur ne sont pas modifies. Profil valide,
avatar, sol, bridge actif, absence d'action et etat/destination de locomotion
sont requis. Aucun maintien pendant les sauts/cinematiques/actions. Les autres
acteurs et le prefab Lucian de production n'utilisent pas cette adaptation.

Essai 215326 : 1 222 releves sans erreur, override UCC=-1 partout, cooperation
observee sur les cycles et absente a Idle. UCC libre 5,2948/8,7372 m/s inchange.
En combat, minima selon direction -1,38 a -3,44 cm, medianes autour de -1 cm,
contre jusqu'a -17/-23 cm avant. Course torche active/rangee a -1,06 cm.
Departs et melanges restent autour de -2/-3 cm : validation verticale finale
incomplete ; phases et raccords a traiter avant livraison.

Sonde ajoutee pour comparer deux sauts par inputs South, avec mouvement maintenu
et torche rangee/active. Candidat 215832 : 1 582 releves, zero erreur et zero
maintien IK natif en vol (28/51 releves aeriens). Reference 215925 : 1 584 releves.
Hauteur au-dessus du root au sol : 1,8345/2,4056 m contre 1,8336/2,4052 m ; ecart
<1 mm pour chaque scenario. Les deux hauteurs different deja dans la reference
et n'ont pas ete recalibrees. Retour au mouvement apres reception dans les deux
cas. Ces deux sauts ne qualifient pas les plafonds, les esquives, le combat,
les cinematiques, les modes de temps, le reseau ou les changements de zone.

Deuxieme application du manifeste : controller, override, profil et prefab
strictement identiques par SHA256. Manifestes/provenance et mesures/captures :
`Lucian_Coherence_NativeCycles/manifest.json`, `idempotence.json`,
`runtime-summary.json` et sous-dossiers locomotion/candidate_jump/baseline_jump.
Compilation Unity passee. Donnees profile.supportValidated restent false.

Prochaine mesure necessaire : conserver l'identite d'un sommet de semelle
durant un appui. Le minimum de semelle courant change de sommet et ne prouve
pas une absence de glissement horizontal. Puis calibrer cadence de contact,
phases directionnelles et raccords ; tester actions/terrain/temps/reseau et
valider visuellement dans le vrai jeu avant de brancher la production.

## Contacts fixes, reference Humanoid et retour du pied (23h59)

La mesure suit maintenant un sommet identifie (renderer + index) pendant chaque
fenetre proche du sol ; elle ne deplace jamais ce sommet ni le personnage.
Capture a +/-2 cm et liberation au-dela de +/-3 cm, plus controle grounded.
Deux diagnostics separes : toutes les fenetres proches du sol et sous-fenetres
d'appui detectees dans la reference native. Ces seuils ne constituent pas une
certification de contact artistique. Controle sur 121 poses : ecart de sommet
tenu manuel/BakeMesh 0,42 micrometre ; identite d'un autre sampler refusee.

Le premier lot de 18 copies ne doit PAS etre considere comme corrige au sens
du rendu final. Il eliminait la penetration mais ecrasait une partie du retour
de pied : choisir FK puis relever seulement les points sous le sol cree une
trajectoire plate pendant le balancement. Les contacts fixes le revelent. Les
fenetres proches du sol de Common Run parcourent parfois plus d'un metre ; une
partie est un balancement tres bas, pas un appui. Les sous-fenetres d'appui de
reference revelent aussi de vrais deplacements parasites. L'ancien minimum
vertical seul n'etait donc pas un critere suffisant.

Comparaison Root/Inplace, FK/IK sur le meme avatar : la trajectoire Humanoid IK
preserve mieux le retour. Exemple a phase 0,75 du Common Run : semelle droite
FK a 0,035 m du root, native IK a 0,135 m ; sol physique a 0,100 m. Le premier
clamp FK la posait donc au sol a tort. Le source Root presente lui aussi un
ecart entre mouvement extrait et appui retargete : on ne deduit plus la cadence
finale du seul deplacement Root moyen.

Nouveau pilote Common Run Stride02 : reference native IK, memes muscles/corps,
evenements et duree, correction verticale cyclique estimee durant les appuis
puis interpolee pendant le retour. La pose native de reference est conservee
horizontalement et en orientation ; aucune retouche des sources/imports.
Gates hors gameplay : corps et rotation des pieds inchanges, ecart cheville
<2 micrometres, penetration max sur deux cycles 0,70 mm, Root extrait zero.
Au milieu du retour droit (phase 0,75), la semelle vise ~13 cm au-dessus du sol
au lieu d'etre aplatie sur celui-ci.

Cooperation IK affinee : conserver le poids de retarget ne doit pas transformer
la correction partielle UCC en rotation sol instantanee. Les objectifs natifs
et ceux corriges par UCC sont maintenant interpoles par le poids de correction
UCC, puis resolus au poids natif 1. Ni configuration ni lissage interne UCC
modifies. Hors eligibilite, le solveur reste celui d'origine.

33 contacts mesures dans walk/run/torch_walk/torch_run donnent un facteur
mediane 0,93630 par rapport au premier seed, soit nativeSpeed exploration
4,96776 m/s. UCC reste a 5,2948/8,7372 m/s. Le nouveau binding preserve cette
calibration plutot que la reinitialiser. Deuxieme binding : profil/override
strictement identiques par SHA256.

Essai 223511, 3 055 releves sans erreur ; sous-fenetres d'appui detectees :

| Scenario | Contacts retenus | Chemin median | Maximum |
| --- | ---: | ---: | ---: |
| Exploration normale | 6 | 3,56 cm | 3,80 cm |
| Sprint | 10 | 2,11 cm | 3,38 cm |
| Torche, normal | 6 | 2,60 cm | 3,14 cm |
| Torche, sprint | 10 | 2,18 cm | 3,33 cm |

Le seuil de 3 cm n'est pas atteint partout. Les fenetres proches du sol restent
plus larges que les appuis detectes, notamment a l'approche du contact : ne pas
les masquer dans une declaration de reussite. Les directions de combat utilisent
encore les premieres copies FK ; retour et phases y restent a reconstruire.
Une unique correction de cadence ne peut pas reparer ce defaut de trajectoire.

Recontrole sauts 224023 : zero erreur, zero retention IK native en vol ; hauteurs
1,8357/2,4060 m contre 1,8336/2,4052 m reference, ecart maximum 2,2 mm. Aucun
parametre de saut modifie. Cela ne qualifie pas les autres interactions.

Audit du rig : 55 os Humanoid ont memes chemins, positions, rotations et longueurs
que le modele CC5 et son Avatar. La comparaison de deformation de peau doit etre
faite dans le repere du root, car la transformation du renderer s'annule lors du
skinning monde. Dans ce repere, aucune difference de rotation par rapport au
modele source, ecart de translation maximal 3,17 cm. Cet offset de reference ne
prouve pas une deformation articulaire ; aucun squelette/avatar n'est remplace.

Preuves : `Lucian_Coherence_Contacts` (initial/swing/cadence/jump, source-reference,
contact-sampler.txt, stride-report.json, stride-contact-phases.csv, calibration,
rig-rest.json, script de lecture). Les appuis detectes sont une aide a la retouche,
pas une validation visuelle finale. Priorites : appliquer la reference native et
les retours aux autres cycles, aligner les phases, traiter les petits residus et
les raccords, puis actions/terrain/temps/reseau et jeu. Production non activee.
