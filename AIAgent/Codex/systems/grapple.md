# Competences d'exploration et grappin — 2026-11-07

## Equipement

AbilityService reste l'autorite des deblocages, de la sauvegarde et des requetes
reseau. Aucun etat mutable n'est ajoute a CharacterData. AbilitySaveData conserve
ses entrees historiques playerId/abilityId ; plusieurs entrees pour une meme
identite portent maintenant plusieurs capacites. equipmentVersion=2 distingue
les anciennes sauvegardes : en solo, leurs capacites deja debloquees deviennent
equipables ensemble. Les sauvegardes v2 conservent les retraits volontaires.
JSON personnage et JSON inclus dans WorldSnapshot conservent toutes les entrees.

Solo : deblocage equipe automatiquement la nouvelle capacite ; le menu de pause
permet de cumuler/retirer des capacites a la Maison. Les verifications existantes
HasEquipped reconnaissent l'ensemble, pas seulement la premiere entree legacy.

Multi : l'hote repartit les capacites debloquees sans doublons, en ordre de
deblocage, entre les identites des clients presents ordonnes par clientId.
Quatre capacites donnent 4 en solo, 2/2 a deux, 2/1/1 a trois, 1/1/1/1 a quatre.
Avec moins de capacites debloquees, certains joueurs n'en ont pas encore.
Changement d'effectif ou nouveau deblocage : redistribution. A la Maison, choisir
une capacite d'un autre joueur echange une des siennes avec celle-ci. Les donnees
solo restent separees des entrees multijoueur. Ce partage n'est pas une refonte
des kits de combat : ceux-ci restent propres au personnage attribue en multi.

## Grappin

Assets/Gameplay/Grapple contient le scanner de session, les ancrages auteurs,
le mouvement et le partial de WorldInteractionService. Le scanner utilise
l'acteur local, la portee et la ligne de vue physique ; la camera choisit les
ancrages visibles devant elle. Les interactions ordinaires ne sont pas modifiees.
Le prompt reutilise WorldInteractionUiSettings, sans menu ni focus bloqueur.

- Observateur equipe revele les ancrages d'une liaison pour toute l'equipe.
- Bondisseur equipe permet d'initier la traversee et de preparer la liaison.
- En multi, la liaison preparee permet ensuite le passage de tous les joueurs.
  En solo, Bondisseur reste necessaire meme sur une liaison preparee.
- A / input Interact confirme l'ancrage cible uniquement si le depart est proche,
  l'acteur vivant/au sol/sans verrou concurrent, et la trajectoire libre.
- L'arc guide passe par les API de traversee UCC ; aucun collider n'est desactive.
  Un balayage de capsule verifie le parcours avant depart et a chaque pas.
  Obstruction, mort ou dechargement annulent la traversee et liberent son seul
  verrou. L'animation Falling existante sert de presentation provisoire avec un
  lien visuel ; ce n'est pas une reproduction certifiee des animations du jeu
  de reference. Aucun asset tiers n'est copie.
- Revelation et preparation sont des faits WorldRulesStateManager, respectivement
  world.grapple.<anchorId> et world.grapple.route.<routeId>. Les RPC valident
  identite serveur, capacite, portee, geometrie et cooldown ; l'owner execute le
  mouvement UCC, comme les traversees existantes. Snapshot cible pour client tardif.

## Limbes

Reve_Limbes_Progression conserve son arenaAccess et ses cinq cycles obligatoires.
Le pont final existant est conserve pour ne pas bloquer la campagne avant la
configuration du deblocage narratif de Bondisseur. Un acces rapide parallele
ajoute un palier et deux ancrages aller/retour qui
relient (604,129.43,89) et (610,129.43,114). La reception laterale contourne
le pilier frontal de l'arene sans retirer sa collision. Le NavMesh de la route
a pied reste valable ; le trajet en l'air n'est pas un chemin d'agent NavMesh.
Le boss, son introduction et ses preconditions restent inchanges.

Bondisseur est une nouvelle definition Resources/Abilities, verrouillee par
defaut. Son moment d'acquisition dans la campagne n'est pas decide : brancher
AbilityUnlock/AbilityService.Unlock depuis un evenement narratif explicite.
Le test natif grapple l'accorde uniquement dans sa sauvegarde isolee. Ne pas
presenter ce profil de test comme un deblocage deja ecrit dans le lore.

## Validation

GrappleTests : cumul/retrait/idempotence, JSON et snapshot binaire, legacy,
repartition 1/2/3/4, deconnexion, arc, obstruction et liaisons de scene.
AbilitySystemTests couvre aussi la compatibilite des API legacy.
Compilation runtime/editeur reussie ; 15/15 tests natifs passent, y compris
rayons de visibilite et balayages physiques des deux arcs auteurs.
Rapport : Library/GrappleValidation/Tests.xml, 2026-11-07 12:10:18Z.
Demandes natives : Library/GrappleValidation/Install.request et Tests.request.
Le parcours BootstrapDistrictPlayProbe, mode grapple, est passe : 114 controles,
zero erreur runtime, etat initial de l'editeur restaure. Rapport :
Library/StartupDistrictValidation/PlayRuns/795613cf0b754adca78db9d8f7ac258e/result.json.
Il couvre les treize preuves, les cinq cycles, le scan reel avec Observateur,
la confirmation par le routeur d'interaction, l'arc UCC aller, l'introduction
du boss, ses attaques et sa defaite, puis l'arc retour, les verrous relaches,
capture/restauration du cumul de capacites, ReturnToHub et retour dans les Limbes.
Le test precedent avait montre qu'une assertion globale exigeait aussi les
reconnaissances de passages secrets facultatifs : l'assertion est maintenant
limitee aux sequences principales limbes.*, sans changer leur progression.
Le helper historique d'approche des portails a aussi refuse une approche de
sortie ; cette recette ciblee utilise ReturnToHub, pas une modification du portail.

Placements d'approche, orientation camera au scan, avance des dialogues,
deblocage de Bondisseur et degats finaux du boss sont des fixtures declarees.
Le trajet de branches a pied a ete verifie lors des essais precedents ; la
recette finale le saute pour se concentrer sur le grappin. Les six cycles et
la sortie sont controles apres retour de zone ; une reprise froide du processus
et la retention des faits du grappin apres celle-ci ne sont pas certifiees.
Recette reseau 2/3/4, Domain Reload desactive, ressenti manuel et parite visuelle
avec Expedition 33 restent a qualifier. Ne pas les deduire des tests de modele.
Menu : Lit/Traversal/Test Limbes grapple (isolated save).
