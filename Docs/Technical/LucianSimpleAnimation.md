# Animation simplifiée de Lucian

## Contrat de production

Lucian utilise une couche principale en corps entier et une seule couche de bras pour porter la torche. UCC garde l'autorité physique. Les vitesses, accélérations, distances des actions et clips de saut ne sont pas recalibrés par cette migration.

L'écoute et l'attente réutilisent le même clip Idle que l'exploration, sans copie ni écart de posture supplémentaire. La présentation du dialogue demande ses états au contrôleur d'actions existant. Le bridge transmet la vitesse physique et la direction aux Blend Trees. Les vitesses des etats et leurs seuils se reglent manuellement dans l'Animator ; aucun calcul de cadence ne les adapte au moteur.

## Sources et données

- Exploration : Mobility Starter IPC, Idle, marche et jogging en corps entier.
- Lock : Dynamic Archer Humanoid Inplace, huit directions marche/course sans variante de visée.
- Dialogue et interactions : sources non Twinblades ; assise conservée lorsque le geste complet correspondant n'est pas disponible.
- Combat : les clips, événements et fenêtres des attaques, compétences, esquives, défenses et réactions existantes restent en place.

Les 24 copies de locomotion et le profil mesuré sont sous `Assets/Animations/Autres/Locomotion/Lucian/SimpleWholeBody`. Les originaux fournisseur sont conservés. Le manifeste identifie GUID, sous-asset, source, copie, vitesse native, phase et trajectoire mesurée. La conversion retire seulement la trajectoire physique par échantillonnage sur l'avatar de Lucian, puis conserve la pose complète.

Vitesses natives mesurées : exploration 1,177 m/s (marche), 3,094 m/s (jogging) ; cycles de lock avant 0,848 m/s et 3,543 m/s. Les autres directions ont leurs propres mesures. Ces mesures restent documentaires. Aucune cadence n'est calculee automatiquement ; regler manuellement les vitesses des etats Animator, les seuils des Blend Trees et UCC.

Le profil réutilise les données existantes. Les champs historiques de contacts, pieds et récupération ne pilotent pas ce chemin. Unity peut désérialiser ces classes en objets vides : seule une donnée complète et explicitement configurée est significative ; le chemin simplifié n'en consomme aucune.

## Retrait des compensations

Les anciens solveurs de buste, de jambes séparées et de contacts compensatoires sont supprimés du projet, ainsi que leurs expériences et installateurs. Leurs composants et couches ont été retirés par les API Unity avant la suppression des scripts. Le composant IK personnalisé a été remplacé par le composant UCC standard en conservant ses références sérialisées. Les autres protagonistes ne sont pas recalibrés : ils seront reconstruits ultérieurement sur la base de Lucian.

Le bridge conserve un seul chemin de locomotion présenté depuis la vitesse physique. Le mode qui tournait seulement le bassin des actions a été supprimé : l’orientation passe par le moteur UCC. Les profils ne contiennent plus de courbes compensatoires de pieds ou de variantes de dialogue UpperBody. Les anciens menus de dialogue redirigent vers l’installation et le validateur uniques. Les captures et mesures sans modification de pose restent disponibles.

Les états Turn_L180, Turn_R180 et Run_Stop restent des identifiants réservés sans clip : aucune animation 180° n'est inventée à partir d'un virage 90°, et aucun arrêt de course incompatible n'est substitué. Le bridge simplifié ne les demande pas ; il utilise les raccords des cycles complets. Les débuts/arrêts auteur restent accessibles mais ne sont pas imposés aux déplacements normaux.

## Outils

`Lit > Animation > Simple Lucian` propose :

1. Audit Without Changes : résolution et mesure des sources, rapport dans Library, sans modification des assets.
2. Apply Migration : prévalidation complète, puis migration ciblée par les API Unity ; sauvegarde initiale des assets concernés dans Library.
3. Validate Contract : contrôle des deux couches, du masque de bras et de l'exclusion de Twinblades hors locomotion/dialogue.
4. Play Mode Smoke Test : essai borné dans Iluvilirae_Test, avec restauration de la scène de démarrage Play de l'Editor. Ne sauvegarde pas les scènes de travail.

Les copies conservent leurs GUID lors d'une seconde installation. Le validateur s'exécute également avant build. Les exports d'audit et demandes de validation temporaires sont dans `Library/SimpleLucianAnimation`.

## Validation et limites

Les réglages physiques de Lucian et de ses deux variantes de laboratoire sont comparés aux données initiales. La sérialisation Unity a ajouté dans la variante ImpactLab le champ booléen existant holdLandingThroughContactLoss à sa valeur par défaut false ; elle n'en a pas changé le comportement.

Les tests vérifient le contrat installé, les choix de dialogue, les anciennes sessions, les courbes originales de jambes/bassin, l'isolation effective du bras de torche, l'absence de contacts compensatoires et l'absence du multiplicateur automatique de cadence sur Lucian.

Les captures de poses ne remplacent pas la validation de transitions en mouvement. Le contrôle visuel final doit inclure exploration, lock, diagonales, départs/arrêts, combos, esquives, réception, assise, torche, dialogues et cinématiques ; puis pente, mur, ralenti et host/client. Les effets de torche et la simulation de cape ne sont pas validés par des captures de pose fixes.

### Résultats de ce lot

Compilation Unity runtime/Editor réussie. 55 tests Editor passent dans le projet principal. Le smoke test Play Mode passe pour le mouvement UCC, la parole à l'arrêt, l'attente après réplique, la libération du verrou, une réplique mobile sans pose imposée et la préemption par une action. Il utilise des demandes déterministes envoyées au bridge public : il ne valide pas les bindings de manette.

Les 15 premiers assets de dialogue retirés étaient prouvés sans consommateur : trois masques, neuf variantes UpperBody et trois copies corporelles indisponibles. Les copies de repos redondantes sont également retirées après redirection vers l'Idle commun, seulement si elles ne sont plus référencées. Les ressources fournisseur et les GUID des clips conservés ne sont pas réinstallés.

Les preuves sont conservées sous `Docs/Validation/2026-11-19/SimpleLucian`. Les parcours visuels complets, les collisions dans toutes les situations, le ralenti et le host/client ne sont pas déclarés validés par ces tests ciblés.

La réinstallation finale a conservé à l'identique les 88 assets contrôlés. Le test réseau vérifie aussi la sérialisation du drapeau de suppression corporelle : les observateurs suivent la décision du propriétaire, sans déduire le mouvement depuis un moteur distant désactivé. Le smoke test couvre le retour depuis la présentation cinématique avec des inputs annoncés maintenus.

## Reglages manuels des vitesses - 20 novembre 2026

Le calcul de cadence par vitesse native et le parametre `LocomotionPlaybackRate` sont supprimes du chemin de Lucian. Le menu `Lit > Animation > Simple Lucian > Use Manual Locomotion Speeds` retire uniquement les anciens liens vers ce parametre dans ses deux controllers. Il conserve les valeurs `Speed` des etats, les vitesses des enfants, les seuils, les transitions et les modifications existantes. Une reinstallation ne reactive plus ce multiplicateur.

Regler la presentation avec `Speed` sur les etats de locomotion et les seuils `LitSpeed` des Blend Trees. `LitSpeed` transmet directement la vitesse planaire en m/s. La vitesse de deplacement se regle dans UCC. Le temps global de combat et de cinematique reste independant de ce retrait.
