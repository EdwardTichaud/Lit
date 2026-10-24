# District_1 — reprise du 24 octobre 2026

L'objectif global de consolidation reste **en cours**, pas entièrement accepté.

## Livré

### Luc

Racine réactivée. Fantôme à `(-3.5, -93.865, 51.5)`, tombe à
`(-9, -93.865, 54.8)`. Grand décalage local du modèle de tombe neutralisé sur
cette instance uniquement. Collider explicite relié à l'interaction, portée
2 mètres, outline sur le renderer visible.

Vérification native du sol physique, des dégagements et de deux chemins
NavMesh complets ; couverture lumineuse potentielle issue d'une scène permanente,
indépendante de Belmont. La Flame proche est une Ancient Flame, type accepté
par le contrat runtime d'influence ; aucune source n'a été créée ou modifiée.
Le collier, les autres poses, les IDs existants et les autres scènes sont conservés.

Preuves locales : `Library/DistrictCycleValidation/LucRepair-20261024.txt`,
inspection préalable `LucInspection-20261024.json`, sauvegarde de sécurité
`LucBeforeRepair-20261024-080856.unity`.

### Nina

Décision humaine : créer le souvenir facultatif. Timeline **« Encore là »**,
18 secondes, quatre plans Cinemachine et quatre citations de la lettre existante.
Les apparitions sont des copies de maillages sans logique de gameplay ; les
pistes Activation les masquent hors lecture. Aucun son de production ajouté.
La passerelle caméra commune et le profil sont assignés au Director existant.

Assets éditables sous `Assets/Scenes/Cycles/Cycle_Nina/Memory`. Placements et
identifiants existants conservés ; définition et migration de sauvegarde inchangées.
Le souvenir reste facultatif et ne devient pas un prérequis supplémentaire pour
Nina ou Cicatrice. Aucune information nouvelle sur le rituel ou le destin d'Édouard.

Preuve de création native et de conservation des scènes :
`Library/DistrictCycleValidation/NinaMemoryAuthoring-20261024.txt`.
Snapshot précédent : `NinaBeforeMemory-20261024.unity` dans le même dossier.
Les deux scripts temporaires d'édition Luc/Nina ont été retirés d'Assets et
archivés dans Library ; aucun générateur permanent n'a été ajouté.

### Gustave

Décision humaine : proposer un cycle complet. Proposition écrite dans
`Cycle_Gustave_Proposal.md` : enquête facultative « Ce que la lumière garde »,
deux preuves et deux observations, sans boss imposé. Identité, traces et
récompenses proposées restent **à approuver**. La note et la scène actuelles ne
sont ni détruites, ni renommées, ni transformées en nouveau canon.

## Vérifications de cette reprise

- Première suite native dans le projet principal : **192/192 réussis**, zéro
  ignoré, `FullCycleTests-20261024.xml`.
- Après création de la Timeline et ajout du contrôle de scène Nina :
  **193/193 réussis**, zéro ignoré, `FullCycleTestsWithNina-20261024.xml`.
- Dernière passe, incluant le parcours natif des quatre citations et leur
  nettoyage après arrêt : **194/194 réussis**, zéro ignoré,
  `FullCycleTestsCleanup-20261024.xml`.
- Le passage intermédiaire `FullCycleTestsFinal-20261024.xml` contient un
  échec de fixture : l'assertion vérifiait la disparition immédiatement après
  Stop, avant la destruction différée du graphe. La fixture attend maintenant
  la frame suivante, conformément à `ActivationMixerPlayable.OnPlayableDestroy`.
  Le contrôle de visibilité et les mêmes assertions sont conservés ; aucun code
  de production n'a été modifié pour contourner cet échec.
- Ces suites concernent les classes de progression, dialogues, Timeline,
  rassemblement, triggers et les scènes Luc/Nina/Belmont/Étienne/Ardent/Clairmont
  sélectionnées dans le journal. Ce n'est pas l'ensemble de tous les tests du jeu.
- Les trois suites réussies et la création native sortent à code 0 ; le passage
  intermédiaire de fixture sortait à code 2. Aucun processus de cette reprise
  ne reste en attente.
- Aucun essai de rendu HDRP ou parcours complet avec personnages réseau n'est
  déduit de ces succès Edit Mode.

## À reprendre ensuite

1. Accepter le rendu du souvenir Nina dans le vrai District : cadrages, éventuels
   obstacles devant les caméras, lisibilité des citations, pose et matériaux de
   la trace, reprise de la caméra et des contrôles. Tester fin, arrêt, échec et
   déchargement en solo et hôte/client.
2. Jouer Luc dans les deux ordres de collecte, puis résolution/dissolution ;
   vérifier les sources réellement allumées et le parcours après Belmont terminé.
3. Valider ou modifier la proposition Gustave avant d'en créer le contenu.
4. Réaliser les parcours complets, persistances et arrivées tardives de tous les
   cycles, notamment votes d'Ardent, frein d'Étienne, torches de Belmont,
   BattleWall, dégâts et accès après déchargement.
5. Actualiser une matrice d'acceptation solo/réseau/rendu/sauvegarde. Ne pas
   déclarer les cycles prêts comme références tant que ces cases manquent.

Préserver les scènes manuellement modifiées, les prefabs sources et les travaux
en parallèle sur animations/persistance. Les anciens chemins partiels de NavMesh
doivent être inspectés, pas corrigés par un rebake automatique de tout le district.
