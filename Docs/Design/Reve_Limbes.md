# Reve — Les Limbes

Statut : base créée et intégrée le 25 octobre 2026. Portails, récupération,
objets, deux cycles et 19 liaisons de saut validés en solo.

## Demande et direction

Créer une nouvelle famille de scènes `Reve_`, en commençant par une zone de
plateforme nommée `Reve_Limbes`, avec sauts, exploration, objets récupérables et
cycles. Utiliser principalement les instances du pack
`Assets/0 - UnityPackages/Fab/ModularDungeonCollection`.
Accès demandé par le joueur : **portail depuis la Maison**.

Le concept fourni montre des îles de ruines, des arches, des piliers détachés et
des structures inclinées autour d'un tourbillon lumineux. Les fragments de décor
se contredisent ; le parcours praticable doit rester compréhensible. Le lieu ne
révèle pas automatiquement l'origine du château ou le rituel : il reprend
l'instabilité et les incohérences du Monde des Rêves décrites dans `Lore.md`.

## Architecture retenue

- `Reve_Limbes` : scène principale, points d'arrivée et récupération après chute.
- `Reve_Limbes_Environment` : îles, fragments, collisions et parcours permanents.
- `Reve_Limbes_Lighting` : ambiance HDRP, lumière centrale et Flames communes.
- `Reve_Limbes_Interactives` : retours permanents vers la Maison.
- Scènes de cycles `Reve_Limbes_Cycle_*` : quêtes, preuves, interactions et seuls
  éléments de décor effectivement pilotés par elles.
- Un manifeste propre réunit les scènes ; l'accès Maison est ajouté dans une
  petite scène dédiée, sans déplacer les éléments existants du hub.

Les scènes et les prefabs sources de District_1 ne sont pas reconstruits.
Les modules du pack restent des instances éditables ; une éventuelle adaptation
HDRP utilise des copies locales de matériaux, pas une conversion globale du pack.

## Contraintes de jeu

Les distances et hauteurs des sauts doivent être réglées à partir du contrôleur
de saut actuel. Prévoir des paliers de repos, quelques détours facultatifs et des
retours possibles. Une chute doit ramener le joueur à un point sûr sans perdre
les objets ou jalons déjà acquis. Le chemin critique n'exige pas une capacité
optionnelle. Les interactions restent couvertes par les Flames communes selon
les règles existantes.

## Première base construite

Les six scènes sont enregistrées sous `Assets/Scenes/Reve`. Le parcours est
ancré à `(600, 120, 0)` pour éviter les chevauchements avec les zones existantes.
Sept grandes îles et treize petites dalles forment les branches ; des arches,
piliers et débris flottants entourent un cœur lumineux. Les modules restent des
instances de `ModularDungeonCollection`, sans modification de leurs sources.
Sept Flames communes éclairent les postes d'interaction.

L'accès est créé dans `Assets/Scenes/Maison/Maison_AccesReve.unity`, près de
l'arrivée Maison, à environ `(3.03, -0.55, 5.99)`. La recherche native a vérifié
un sol physique et un volume libre ; aucune navigation Maison préconstruite
n'étant assignée au manifeste, elle n'a pas prouvé le chemin d'approche complet.
Les deux portails de retour sont dans `Reve_Limbes_Interactives`.
L'accès est ajouté au manifeste Maison et le manifeste des Limbes est enregistré
dans Bootstrap. Les sept nouvelles scènes figurent dans la liste native des
scènes activées et dans `ProjectSettings/EditorBuildSettings.asset`. La lecture
native a été effectuée dans l'éditeur principal, et pas seulement dans la copie.

- **Le seuil éparpillé**, `reve.limbes.seuil` : récupérer trois fragments sur
  les branches du parcours, dans n'importe quel ordre, puis interagir avec la
  stèle du carrefour. L'activation persistante `reve.limbes.seuil_recompose`
  révèle le passage vers la dernière rive.
- **La chambre à l'envers**, `reve.limbes.chambre` : explorer une branche
  facultative renversée, récupérer son fragment puis examiner son miroir. La
  récompense utilise la boîte de récompenses existante de la Maison : trois
  chiffons sales, utilisables comme combustible selon leur configuration actuelle.

Les deux définitions sont sous `Assets/Resources/Narrative`, sans prérequis
entre cycles. Quatre preuves possèdent leurs propres identifiants d'item,
de lecture et de connaissance ; elles sont récupérables et interactives à 2 m.

Le composant `LimbesFallRecovery` observe les joueurs côté hôte. Après une
chute, il demande au propriétaire du personnage de revenir au dernier palier
visité, par le contrôleur de déplacement existant. Un joueur resté dans une
autre zone n'est pas considéré comme participant. Aucun nouvel état de
sauvegarde ni retrait d'inventaire n'est introduit.

## Vérifications effectuées et limites

- Création native Unity dans la copie de vérification marquée, puis transfert
  des nouveaux assets avec contrôle des empreintes : aucun asset existant écrasé.
- Contrôle natif des six scènes : aucun script manquant, coordonnées finies,
  deux définitions valides et références item/connaissance présentes.
- NavMesh construit ; chemins natifs complets de l'entrée aux six îles
  initialement accessibles, avec dix-neuf liaisons de saut bidirectionnelles.
  Cela ne prouve pas encore la traversée par le véritable personnage.
- Cinq tests EditMode de récupération passent : nécessité d'un sol réel,
  quatre positions d'arrivée coopératives distinctes, refus des destinations
  invalides, vérification des volumes orientés et exclusion des valeurs invalides.
- La copie d'authoring n'inclut pas les pixels de toutes les textures. Elle
  n'apporte donc aucune preuve de qualité du rendu. Les matériaux et prefabs
  conservent les références originales, à résoudre dans le projet principal.
- Prévisualisation native du projet principal : 194 instances du pack,
  aucun script manquant. Le premier rendu a révélé des matériaux roses,
  dont deux références de matériaux absentes des sources du pack. Onze
  matériaux HDRP locaux ont été créés et les instances ont reçu des
  remplacements, y compris pour les slots manquants. Le dernier rendu ne
  présente plus ces surfaces roses. Les sources du pack restent intactes.
  Le fond noir de la première prévisualisation est corrigé par l'exposition
  locale du ciel ; les captures de la caméra en jeu ont également été examinées.
- L'essai réel `b7359d8f2ec545d9bd45d2f44631313f` est **réussi** : Bootstrap,
  MainMenu, création solo isolée, portail Maison → Limbes, six scènes chargées,
  saut réel UCC vers la première dalle, chute et retour au palier, portail
  Limbes → Maison, puis retour au menu. Pic du saut à `122.6291`, atterrissage
  à `(600, 120.40, 9.28)`. Aucune erreur runtime observée et configuration
  initiale de l'éditeur restaurée. Les approches du portail et du point de
  départ utilisent un positionnement de test UCC ; le saut et la récupération
  sont exécutés par les véritables composants de jeu.
- L'essai `2181b16ef29540d985516e71db0f26dc` a été interrompu par l'arrêt du
  Play Mode avant l'arrivée en jeu. Il n'est pas considéré comme réussi.
- L'essai ciblé `d754b3b3b1494bbfae18a2b7f6ff5516` a prouvé la récupération
  des quatre preuves dans l'inventaire et l'acquisition de leurs connaissances,
  dans l'ordre 3 → branche facultative → 1 → 2. Le dialogue volontaire de la
  stèle a été accepté et le cycle principal est passé à l'état terminé. Le
  contrôle du passage a échoué immédiatement après, sans attendre le prochain
  rafraîchissement de `CycleActivationId` (intervalle de 0,25 s). Le test est
  corrigé pour attendre l'effet visible ; cette attente et l'ouverture effective
  ont ensuite été validées par l'essai complet réussi indiqué ci-dessous.
- Le ciel, sous-exposé lors de la première prévisualisation, dispose désormais
  d'une correction d'exposition locale au profil des Limbes. Le cœur lumineux
  possède son propre matériau et le tourbillon décoratif est placé sous les
  îles, dans le champ de la caméra de jeu. Ces changements ne déplacent aucun
  élément praticable, preuve ou interaction.
- Les quatorze fichiers des scripts temporaires de construction et leurs
  métadonnées sont archivés sous `Tools/Validation/ReveLimbes/Authoring`, hors
  de `Assets`. Aucun générateur de décor n'est nécessaire au fonctionnement
  des scènes. Les contrôles Play Mode restent dans le dossier de validation.
- La reprise `5c87355f777442c4812e25c70034e41e` a été annulée et restaurée,
  sans être considérée comme une réussite. Unity conservait des chemins de
  scripts retirés dans sa liste de compilation. Le retrait final des fichiers
  de transition a été fait par `AssetDatabase.DeleteAsset` et la recompilation
  a réussi ; aucun script de construction ne reste dans `Assets`.
- L'essai `6674ba85f7b24d05945d6b797310646c` est **réussi** : les quatre
  fragments sont résolus dans le catalogue dès la Maison, avant chargement
  de leurs scènes ; récupération réelle dans l'inventaire et connaissances ;
  résolution volontaire des deux cycles ; activation visible du passage ;
  sortie puis retour par les portails ; deux cycles toujours terminés,
  passage, quatre objets et quatre connaissances conservés. Aucun
  Error/Exception/Assert runtime observé et état initial de l'éditeur restauré.
  Le catalogue existant de `GameplaySessionRoot` référence les quatre nouveaux
  items, pour permettre leur résolution même si les quêtes sont déchargées.
  Les approches sont positionnées par le contrôleur UCC de test ; les dialogues
  et le gestionnaire de récupération sont les systèmes de production.

La conservation ci-dessus couvre une sortie/rentrée dans la même session,
pas un redémarrage complet d'une sauvegarde. Les 19 liaisons de saut sont
validées individuellement. La coopération hôte/client et l'arrivée tardive restent
des essais complémentaires ; aucune réussite réseau n'est revendiquée.

## Suivi des contrôles

- [x] Portails Maison/Limbes aller et retour avec les services de jeu réels.
- [x] Scènes, caméra et contrôles opérationnels en solo.
- [x] Premier saut et récupération dans le vide avec UCC.
- [x] Quatre objets, deux cycles et accès conservés après sortie/rentrée.
- [x] Rendus natifs et captures de caméra en jeu examinés ; matériaux corrigés.
- [x] Dix-neuf liaisons de saut franchies avec la physique UCC et le sol cible vérifié.
- [ ] Compléments : sauvegarde froide, hôte/client et arrivée tardive.

La finition artistique et le ressenti d'un parcours manuel continu restent
à affiner après cette base. Les rapports détaillés des essais sont sous
`Library/StartupDistrictValidation/PlayRuns/<identifiant>/result.json`.
## Calibrage du parcours

Les dalles ont été espacées à partir des déplacements mesurés du contrôleur
UCC, sans changer la vitesse ni la hauteur de saut du personnage. Les premiers
intervalles sont de 0,75 m et les principales traversées latérales ont été
rapprochées. La dalle Saut_09 est raccourcie pour éviter son chevauchement avec
le sol surélevé de l'île voisine. La chambre facultative est rapprochée de
1,6 m ; ses interactions restent dans leur plateforme et leur zone de lumière.

Le NavMesh a été rebâti et les six îles initialement accessibles disposent de
chemins complets. Les 868 identifiants d'objets de la scène d'environnement
et les GUID des assets sont conservés. Les essais individuels utilisent le
contrôleur de production, avec une approche proche du bord, et vérifient le
collider de la plateforme d'arrivée plutôt qu'une distance à un point idéal.
Ils repositionnent le personnage entre les sauts et ne constituent pas une
partie manuelle continue.

Essai final du parcours : `0be3c206c163419088d6ed333a3f0ab4` — réussi, 19/19 liaisons, retour Maison/menu, aucune erreur runtime observée et restauration de l'éditeur confirmée. Les sauvegardes de test sont isolées. Les limites ci-dessus (parcours manuel continu, sauvegarde froide et multijoueur) restent applicables.

