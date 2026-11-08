# District_2 — Stillhaven

Statut : proposition intégralement validée le 27 octobre 2026. Construction en cours.
Ce document définit le contenu approuvé ; sa validation ne signifie pas que toutes
les scènes sont déjà construites ou qualifiées. Lore.md reste l'autorité cosmologique.

## Identité et continuité

Le deuxième district se nomme **Stillhaven**, après **Moonrise** (District_1).
Ses cours et maisons forment un ancien refuge désormais silencieux ; le cycle
de la famille Mirage en révèle les liens quotidiens et les chemins d'entraide.

L'accès depuis Moonrise est libre dès sa découverte, sans exiger Clairmont ni
les vingt notes. Le retour vers Moonrise reste disponible. La sortie vers les
Limbes se débloque à la conclusion locale de Stillhaven.

Le nom présenté au joueur est « District 2 — Stillhaven » dans les sauvegardes,
à l'arrivée et pendant le chargement. Les scènes `District_2_Courts*`, le
manifeste et les identifiants existants restent stables pour conserver les
références et les sauvegardes. Le manifeste est enregistré dans le Bootstrap
pour reprendre une partie directement dans District_2 avec ses scènes additives.

## Direction validée

- District 1 — Moonrise sert de tutoriel ; District 2 — Stillhaven développe le jeu d'exploration/enquête.
- Les Limbes deviennent la troisième zone.
- Réutiliser au maximum les assets des zones précédentes.
- Un cycle raconte une famille dont un jeune membre a découvert et aménagé des passages.
- Les thèmes retenus sont la liberté et l'altruisme.
- Vingt passages sont prévus dans l'ensemble du jeu ; leur liste diégétique n'est pas exhaustive.
- Les notes sont réparties entre les zones ; certaines renvoient vers une zone antérieure.
- Réserver des passages aux zones futures.
- Lecture : connaissance garantie. Observateur : d6 caché, succès sur 2+, une tentative par personnage et passage.
- La connaissance est partagée ; Sauf-Conduit reste individuel pour traverser un passage étroit.
- Reconnaissance à proximité : outline/interactivité et réplique de première découverte.
- Classer les connaissances par cycles. La recherche des vingt notes est un cycle transversal.
- L'arrivée de certaines notes dans les Limbes reste volontairement inexpliquée.

## Proposition narrative — Les portes qu'on laisse ouvertes

Noms validés : famille Mirage ; Alix, adolescente ; Agathe, sa mère ;
Abel, son père. Leur courant religieux et les circonstances précises de
leur disparition ne sont pas définis par cette proposition.

Alix explore d'abord les recoins du château pour disposer d'un espace à elle.
Elle découvre d'anciens accès et aménage quelques petites ouvertures praticables.
Ses proches s'inquiètent des risques, sans être présentés comme des geôliers.

Au fil de ses explorations, Alix utilise aussi ce savoir pour aider : porter des
provisions, faciliter des visites et éviter des détours aux proches. Sa liberté
ne consiste donc pas à abandonner sa famille. Les traces montrent des gestes
quotidiens répétés, pas un dernier sacrifice qui expliquerait toute sa vie.

Une limite devient importante : les passages étroits qu'elle utilise ne sont pas
accessibles à tout le monde. Elle cherche aussi comment rétablir un accès commun,
plus large, notamment utile à son père. Cet accès public n'est pas un
vingt-et-unième passage secret.

Le joueur reconstitue ces épisodes dans les lieux de vie et remet au présent cet
accès commun en service. Cela ouvre le parcours de la zone : il ne réécrit pas
le passé et ne prétend pas sauver rétroactivement les habitants.

La conclusion porte sur une liberté qui permet de choisir ses liens et d'aider,
sans effacer ses propres besoins. Elle n'exige ni de révéler une mort héroïque,
ni de résoudre une nouvelle accusation d'abandon comme celle d'Éloïse.

Les vingt notes prolongent cette histoire au-delà de la zone. Leur découverte
intégrale n'est jamais nécessaire pour terminer le cycle familial.

## Proposition spatiale — Les cours intérieures

Un quartier relié par des cours, des escaliers et des galeries à plusieurs niveaux.
La distinction avec District 1 vient de la disposition et des usages, plutôt que
d'un nouveau pack artistique. Les branches reviennent sur les lieux déjà vus
et permettent au joueur de comprendre progressivement leur organisation.

| Secteur proposé | Fonction de jeu et récit |
| --- | --- |
| Cour d'arrivée | Repères visuels, accès visibles et premières pistes ; pas une nouvelle série d'instructions de tutoriel. |
| Maison familiale | Relations, habitudes, annotations et début de l'enquête. |
| Atelier et réserve | Provisions, réparations et effets concrets de l'entraide. |
| Galeries et hauteurs | Exploration verticale, itinéraires alternatifs et distinction entre accès anciens et ouvertures aménagées. |
| Accès commun et cour finale | Mise en service du parcours principal, rencontre de combat et conclusion. |

Réemploi prioritaire : sols, murs, arches et garde-corps de ModularDungeonCollection
déjà utilisés dans les Limbes ; escaliers, mobilier, portes, Flames, matériaux et
socles d'ennemis existants lorsqu'ils sont valides. Les modèles sources et leurs
GUID restent conservés. Chaque prefab réemployé doit être contrôlé avant placement.
La scène WhiteCastle existante n'est pas reconstruite ou remplacée automatiquement.

## Proposition de contenu local

1. **Les portes qu'on laisse ouvertes** : cycle principal familial. Trois pistes
   explorables en parallèle reconstituent curiosité, liens familiaux et aide
   concrète. Les connaissances et les actions sur le décor permettent de
   rétablir l'accès commun.
2. **Le trajet des visites** : enquête annexe sur les déplacements de Abel et
   les limites des raccourcis personnels ; récompense par un épisode familial
   et un retour de parcours utile.
3. **La réserve commune** : enquête annexe sur le partage des provisions, à
   travers les objets et les traces domestiques, plutôt qu'un exposé religieux.
4. **Les marges du plan** : exploration annexe comparant annotations et lieux ;
   montre que Alix a trouvé certains accès et en a aménagé d'autres.
5. **Le passage commun** : rencontre finale avec une Gargouille, puis conclusion
   du secteur. Aucune origine nouvelle n'est attribuée à cet ennemi.

Ces cinq cycles constituent le contenu approuvé à construire.
Les définitions resteront indépendantes ; les accès physiques organiseront
la progression. Le cycle global des vingt notes reste séparé.

Les énigmes du chemin principal reposent sur des observations, lectures et
interactions garanties. Aucun jet d'Observateur, Sauf-Conduit ou passage secret
ne devient un prérequis obligatoire. Les voies ordinaires maintiennent le jeu
possible en solo, quel que soit le choix de capacité.

Proposition de présentation : arrivée, trois souvenirs familiaux, introduction
de la rencontre et conclusion, avec les outils de dialogues/caméra existants.
Les modèles de présentation sont réemployés ; aucun doublage n'est présumé fourni.

## Proposition de répartition des vingt passages

Deux passages du District 1 sont comptés dans les vingt. L'audit a identifié un
seul passage logique existant ; l'ajout d'un second a ensuite été validé.

| Destination du passage | Nombre proposé | Notes correspondantes |
| --- | --- | --- |
| District 1 | 1 existant + 1 nouveau | Une note en zone 2, une dans les Limbes. |
| Zone 2 | 6 | Quatre notes locales, une en District 1, une dans les Limbes. |
| Limbes | 2 | Une note en zone 2 et une note locale. |
| Zones futures | 10 réservés | Affectation et contenu reportés à la conception de ces zones. |
| Total | 20 | Une note et une connaissance propres à chaque passage. |

La zone 2 ne nécessiterait donc pas la création anticipée des zones futures.
Les dix passages réservés ne seraient pas déclarés construits ou testés.

Proposition pour le cycle transversal : compter les **notes consultées**, et non
les connaissances de passages. Une réussite d'Observateur ne fait pas avancer
ce compteur. Les états note retrouvée, note lue et passage reconnu restent
distincts ; le journal permet de relire l'indice et d'identifier la zone concernée.

Aucune note ne doit être enfermée derrière son propre passage. Les dépendances
entre notes et passages doivent rester résolubles sans Observateur, avec les
capacités de traversée normalement disponibles.

Récompenses proposées : raccourcis utiles, pièces facultatives, épisodes
familiaux et ressources existantes. Aucune compétence nouvelle n'est définie ici.

## Ce que la zone révèle et ce qu'elle réserve

Elle montre que des habitants créaient de l'autonomie et des solidarités dans les
usages hérités. Le joueur apprend à utiliser ses connaissances pour explorer et
à revisiter un ancien lieu après une découverte dans une autre zone.

Elle ne révèle ni Azazel, ni les lignées captives, ni l'identité infernale de la
statue : ces révélations restent dans les Limbes. Pas de nouvelle explication du
rituel, de l'origine des Gargouilles ou de l'armée, ni du secret Lucian–Munin.
Les religions restent sincères et ignorantes de l'intention des fondateurs.

## Construction après validation

1. Validation acquise : récit, noms, parcours, comptage et suivi des notes lues.
2. Construire les scènes éditables de la zone, son manifeste, ses accès et ses
   parcours avec les sources d'assets valides. Préserver les scènes précédentes.
3. Créer les cinq cycles locaux, preuves, dialogues, souvenirs et rencontre finale.
4. Créer le catalogue global, les notes/connaissances et les passages affectés
   aux zones construites ; réserver les dix autres sans faux contenu terminé.
5. Migrer l'unique passage existant en conservant son identifiant et les
   découvertes des sauvegardes anciennes ; ajouter le second passage D1 validé.
6. Raccorder la progression District 1 → zone 2 → Limbes par les accès de
   campagne, sans conditionner la fin locale aux vingt notes globales.
7. Compiler et qualifier lectures, progression complète, passages, retours entre
   zones, sauvegardes, reconnaissance unique, tirages par personnage et coopération.
   Examiner le rendu et l'accessibilité de chaque preuve et passage placé.

Le socle technique existant est testé séparément. Il ne prouve pas la réalisation
de ce contenu. Les références de prefabs manquantes signalées dans Clairmont
doivent être traitées explicitement avant de revendiquer un parcours D1 complet.

## État de réalisation après validation

Un premier blockout fonctionnel est installé sous `Assets/Scenes/District_2/Courts` :
scènes `District_2_Courts` et `District_2_Courts_Environment`, manifeste,
cinq cycles, neuf documents familiaux et six présentations.
Les scènes restent éditables ; le prototype
WhiteCastle est préservé. Les notes de passages sont un contenu distinct de ces
neuf documents ; leur installation et leur recette sont décrites ci-dessous.

Le contrôle auteur natif a validé 57 points. Un parcours Play automatisé a validé
les neuf lectures réelles, les cinq cycles, les six présentations et la
conservation des accès après rechargement, sans erreur runtime. Les approches et
l'avance des dialogues sont des fixtures : ce n'est pas une partie manuelle.
Les raccords complets de campagne, le multijoueur et la finition visuelle
restent ouverts. Le lot passages est qualifié séparément ci-dessous.
Le profil HDRP et la lumière diffuse existants du Rêve ont été réemployés sans
les effets des Limbes. Le second essai Play passe aussi après cette correction ;
la capture a été examinée, sans considérer le blockout comme une scène finalisée.

### Lot passages — construction en cours

Six passages des Cours et deux des Limbes sont maintenant installés, avec leurs
notes et des renvois entre zones. Un passage du District 1 est migré en conservant
son identifiant. Le cycle transversal compte les ouvertures réelles des notes,
indépendamment d'Observateur. Dix emplacements restent réservés aux zones futures.
L'audit auteur passe 77 contrôles. La régression native passe 44 tests. Le test
Play du contenu auteur passe 174 contrôles, sans erreur runtime : huit lectures
de notes, huit nouveaux passages parcourus dans les deux sens, portail Cours →
Limbes, conservation des connaissances et du compteur, reconnaissance unique et
épisode familial facultatif. Les approches et dialogues sont pilotés ; ce n'est
pas une partie manuelle continue ni une qualification multijoueur.

### Décisions complémentaires validées et appliquées

- Ajouter un second passage au District 1 : il relie l'entrée du quartier au
  couloir ouest. Sa note se trouve dans les Limbes. L'ancien passage est préservé.
- Laisser l'accès du District 1 aux Cours libre dès sa découverte, sans exiger la
  fin de « Le registre ouvert » ni une compétence ou des notes.

Ces ajouts sont installés : dix passages et dix notes dans les zones actuelles,
dix emplacements toujours réservés au futur. L'audit actualisé passe 87 contrôles.
Le circuit ciblé en Play passe 210 contrôles : accès libre avec Clairmont
incomplet, dix notes lues, dix passages en aller-retour et progression conservée
après les retours de campagne. La régression native passe 45 tests.

Les 66 diagnostics d'assets manquants préexistants du District 1 restent consignés
séparément. Ces résultats ciblés ne valident pas ces assets, la finition artistique,
une sauvegarde froide ou le multijoueur. Un défaut de restauration de la Gargouille
désactivée a été corrigé pour permettre les retours répétés dans les Cours.
