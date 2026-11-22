# Audit de la progression narrative et des parcours

Date : 22 novembre 2026. État des fichiers présents sur disque ; audit sans modification du gameplay.

**Conclusion : les boucles locales sont largement raccordées, mais elles ne forment pas encore une campagne complète ni la boucle de rejouabilité souhaitée.** Les principaux défauts concernent le temps global, les récompenses utilisables, les compétences des compagnons, la collection de notes et les raccords retirés des Limbes.

## Périmètre et degré de certitude

Lecture des cinq manifestes, des 53 scènes activées dans Build Settings, des scènes de réserve sous Assets/Scenes, des prefabs placés et de leurs overrides pertinents, de la racine de session et des quatre prefabs joueurs. Inventaire : 18 CycleDefinition, 112 étapes, 62 KnowledgeSO, 4 capacités d’exploration, 16 emplacements de kits, 10 définitions de passages pour un catalogue de 20 notes, 11 GhostData et 175 Item. Les échantillons de packs, sauvegardes _Recovery et outils de performance ne sont pas des destinations de campagne.

La scène NefDesToursInterrompus est sérialisée en binaire Unity. Elle a été décodée en lecture seule : sa sortie persistante Clairmont, son passage et ses six Flames sont présents. Une absence dans une recherche de texte sur cette scène ne constitue donc pas un résultat d’audit.

Les contrôles statiques ci-dessous sont exhaustifs sur les catalogues nommés et leurs références inspectées. **Ils ne certifient pas l’accessibilité physique de chaque objet, la visibilité, l’équilibrage ou tous les états de sauvegarde.** Aucune recette Play Mode ni session réseau n’a été exécutée pour cet audit. Les conséquences déduites et les décisions de conception sont séparées des défauts confirmés.

Annexe détaillée : [inventaire par cycle, étape, capacité, passage, savoir, objet et scène](C:/Users/pierr/git/Lit/Docs/Design/Audit_Progression_2026-11-22_Annexe.md:1).

## Graphe de campagne effectivement raccordé

Bootstrap → MainMenu → nouvelle partie à la Maison, avec rencontre de Munin. La Maison permet de partir vers Moonrise et possède aussi un portail direct vers les Limbes.

Moonrise : Belmont → accès crypte ; Étienne → conduits noyés ; Ardent → procession basse ; Clairmont → accès statue/belvédère. Les quatre cycles sont indépendants dans leurs données et leur chronologie repose sur les accès physiques. Luc et Nina sont annexes. Gustave reste une vignette. Les deux leviers déplacent un mur ; les Archives de fracture sont un détour de deux énigmes.

Moonrise → Stillhaven : portail libre dans SecretNotes, sans obligation de terminer ces quatre cycles. Stillhaven : enquête Mirage → accès commun ; Visites → traversante ; finale Gargouille → portail Limbes. Réserve, Marges et épisode de visite optionnel enrichissent l’exploration, sans nouvelle récompense de puissance dans leurs définitions.

Limbes : Seuil + Chambre + Création + Captivité + Invocation → accès arène et introduction → Gargouille → départ/conclusion → retour Maison. Les retours de retraite vers la Maison existent aussi avant la victoire. Cette boucle locale ne constitue pas encore la fin générale du jeu.

## État de chaque boucle locale

| Contenu | Sortie ou bénéfice actuellement raccordé | Conclusion de l’audit |
|---|---|---|
| Belmont | Crypte, première Ancient Flame, Sauf-Conduit | Étapes raccordées ; contribution temporelle retirée avec la scène, B06–B09. Combat historique à qualifier. |
| Étienne | Conduits noyés, apaisement, souvenir | Chaîne et sortie persistante présentes ; pas de prérequis Step circulaire. Combat et retour après déchargement à qualifier. |
| Ardent | Procession basse, apaisement de Nora | Chaîne et sortie persistante présentes. Le sort d’Iris reste volontairement ouvert ; donnée de mort ambiguë, N12. |
| Clairmont | Sortie statue/belvédère, registre clos | Raccord de sortie confirmé, y compris dans la scène binaire ; parcours physique actuel non rejoué. |
| Luc | Le destin de Jon, disparition, déchargement avec marge | Boucle locale raccordée ; le collier du cycle fonctionne par ramassage. Un autre ancien collier est mal configuré, N10. |
| Nina | Chimères → Dead → sang/Scar → Cicatrice | Chaîne raccordée ; Cicatrice ajoutée à Souvenirs (B03), validation en jeu restante. |
| Gustave | Note | Pas de cycle au nouveau contrat ni de conclusion, N02. |
| Mirage / family | Accès commun de Stillhaven | Trois preuves, trois souvenirs, interaction de restauration présents. Pas de technique ni objet en récompense. |
| Stillhaven / visits | Traversante | Deux preuves et interaction présentes. Classement Side ; son utilité est un accès, pas un gain de puissance. |
| Stillhaven / reserve | Conclusion du partage | Deux preuves et dialogue présents ; vignette narrative sans prolongement mécanique dans la définition. |
| Stillhaven / margins | Comparaison des ouvertures | Deux preuves et dialogue présents ; savoir de contexte, sans prolongement mécanique dans la définition. |
| Stillhaven / finale | Portail vers les Limbes | Introduction, défaite Gargouille et conclusion raccordées ; pas une conclusion générale du jeu. |
| Limbes / seuil | Seuil recomposé ; Grimpeur dès entered | Cycle raccordé ; capacité obtenue avant sa résolution, C04. |
| Limbes / chambre | Chambre apaisée, chiffon au coffre | Cycle raccordé et objet routé par la mailbox. Classé Side mais requis par le boss, C03. |
| Limbes / création | Souvenir ; participe au déverrouillage d’arène | Trois preuves et séquence raccordées ; pas de gain de puissance propre. |
| Limbes / captivité | Souvenir ; participe au déverrouillage d’arène | Trois preuves et séquence raccordées ; pas de gain de puissance propre. |
| Limbes / invocation | Souvenir ; participe au déverrouillage d’arène | Trois preuves et séquence raccordées ; pas de gain de puissance propre. |
| Limbes / ange | Départ et retour Maison | Boucle locale raccordée ; la fin générale reste absente, N01. |
| Vingt notes | Lectures persistantes et compteur | Impossible à terminer avec les dix réservations ; deux passages sans adaptateur et aucune conclusion de collection, B01–B02, N06. |
| Archives de fracture | Deux énigmes et porte ouverte | Branche intégrée, pas encore une boucle de boss renouvelable, N04. |
| Enola | Défi de rythme et savoir | Boucle solo ; pas d’activité coopérative ni de gain de puissance actuellement, C06. |
| Deux leviers | Rotation physique d’un mur | Liaison mécanique levier A + B → Pivot présente. Pas de CycleDefinition associé ; son intérêt dépend du trajet derrière le mur, à parcourir. |

Un cycle annexe peut légitimement se terminer par une réponse narrative. L’absence de récompense de combat n’est pas en elle-même une panne ; elle devient un manque lorsqu’elle est censée porter la montée de puissance ou un retour utile dans une région.

## Défauts et risques directement rattachés au contenu

### Défauts de raccord et de données

#### B01 — Le cycle des vingt notes ne peut pas être terminé

**P1 · confirmé.** Les vingt étapes note_01 à note_20 sont toutes terminales. Le catalogue réserve explicitement les notes 11 à 20 : aucune scène de note, aucune définition de passage. Les dix lectures manquantes ne peuvent pas être réalisées normalement. Cette impasse concerne global.secret_notes ; elle ne bloque pas à elle seule les portails de campagne.

Suite proposée : Choisir un objectif de collection correspondant au contenu livré, ou produire les dix notes manquantes. Ne pas simplement valider leurs étapes à la place du joueur.

Sources : [SecretNotesCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/SecretNotesCycle.asset:163), [SecretPassageCatalog.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/SecretPassageCatalog.asset:77).

#### B02 — Deux passages catalogués des Limbes ont perdu leur raccord — raccordés le 22 novembre 2026

**Correction appliquée :** les interactions `limbes.1` et `limbes.2` sont présentes dans `Reve_Limbes_Secrets`, avec deux extrémités et triggers chacune, leurs définitions 09/10 et leurs séquences de reconnaissance historiques. Elles relient le Seuil aux terrasses permanentes gauche et droite du montage actuel. Les notes, leurs identités et les découvertes persistantes restent inchangées. Contrôle des références et de la hiérarchie effectué ; traversée Play Mode et hôte/client non exécutée. Le constat ci-dessous décrit l’état avant correction.

**P1 · confirmé.** Passage_09 (limbes.1) et Passage_10 (limbes.2) sont référencés par le catalogue, mais aucun KnowledgePassageDiscovery du montage de campagne ne référence ces définitions. Reve_Limbes_Secrets contient encore les notes et des terrasses, mais aucun TrouEtroit ni dispositif de reconnaissance associé à ces deux fiches. Les savoirs des notes 09 et 10 ne débloquent donc pas les traversées promises par ce catalogue. Cela ne prouve pas que les terrasses soient physiquement inaccessibles par un autre chemin.

Suite proposée : Raccorder les deux traversées à leurs définitions, ou retirer explicitement leur ancien contrat si ces passages ont été remplacés par une autre forme de déplacement.

Sources : [Reve_Limbes_Secrets.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Secrets.unity:1), [Passage_09.asset](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Passage_09.asset:15), [Passage_10.asset](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/Content/Data/Passage_10.asset:15).

#### B03 — Cicatrice est accordée par Nina mais absente des kits utilisables à la manette

**Correction implémentée le 22/11 :** palette « Souvenirs » ajoutée à la roue
avec LB/RB, sans remplacer les seize techniques. Cicatrice devient accessible
après scar_reward ; ciblage et catalogue réseau raccordés, autorisation partagée
vérifiée côté serveur. Compilation C# et contrôles statiques réussis ; tests
Edit Mode ajoutés mais non exécutés, parcours manette/host-client restant à valider.
Le constat ci-dessous décrit l'état avant correction.


**P1 · confirmé pour la palette gamepad.** scar_reward accorde Skill_3_Cicatrice. CycleSharedSkills peut l’ajouter aux compétences connues, mais aucun des seize emplacements du catalogue CombatSkillKitCatalog ne contient cet asset. L’ouverture de palette depuis un gamepad passe prioritairement par CombatSkillKitInput, qui ne propose que ces kits. La récompense existe donc en sauvegarde sans emplacement dans cette palette. Une utilisation par une interface générique différente n’est pas exclue.

Suite proposée : Définir la place de Cicatrice dans le système de sélection actuel et raccorder la récompense à cet accès, en conservant les sauvegardes de Nina.

Sources : [NinaCycle.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Nina/NinaCycle.asset:96), [Default.asset](C:/Users/pierr/git/Lit/Assets/Resources/CombatSkillKits/Default.asset:19), [RealTimeCombatInput.cs](C:/Users/pierr/git/Lit/Assets/CombatRealTime/Input/RealTimeCombatInput.cs:524), [CombatSkillKitInput.cs](C:/Users/pierr/git/Lit/Assets/CombatRealTime/SkillKits/CombatSkillKitInput.cs:63).

#### B04 — Les trois compagnons ne disposent pas de recrutement de campagne raccordé

**P1 · confirmé dans le contenu raccordé.** Une nouvelle progression solo ne recrute que Lucian. Les autres membres demandent RecruitMember. Le seul adaptateur narratif de cette API est SoloTeamStoryEvent ; aucune instance n’est présente dans les scènes et prefabs examinés, et aucun appel RecruitMember/UnlockSkill n’est enregistré dans les assets du projet. La réunion de la Maison crée des acteurs de présentation et joue leur discussion, sans recruter les membres. Les anciennes sauvegardes peuvent masquer ce manque par migration du roster.

Suite proposée : Placer un événement de recrutement réel pour Vesta, Soren et Aurell, avec un moment narratif et un retour visible au joueur.

Sources : [SoloTeamProgression.cs](C:/Users/pierr/git/Lit/Assets/CombatRealTime/SkillKits/SoloTeamProgression.cs:49), [SoloTeamStoryEvent.cs](C:/Users/pierr/git/Lit/Assets/CombatRealTime/SkillKits/SoloTeamStoryEvent.cs:8), [MaisonCompanionReunion.cs](C:/Users/pierr/git/Lit/Assets/Narrative/StorySequence/Scripts/MaisonCompanionReunion.cs:99).

#### B05 — La progression des techniques des kits n’a pas de déclencheur auteur

**P1 · confirmé dans le contenu raccordé.** Lucian commence avec le masque 7 : ses trois premiers emplacements sont ouverts, le quatrième reste fermé. Les compagnons recevraient seulement leur premier emplacement à leur recrutement. Aucun événement de campagne ne raccorde UnlockSkill pour ouvrir ensuite les autres emplacements. Les récompenses CycleReward.Skill ne modifient pas ce masque de kit. Le contenu ne fournit donc pas de montée progressive des seize techniques via les cycles actuels.

Suite proposée : Définir pour chaque emplacement fermé une source précise de déblocage, puis unifier le droit d’utilisation des récompenses et celui des kits.

Sources : [SoloTeamProgression.cs](C:/Users/pierr/git/Lit/Assets/CombatRealTime/SkillKits/SoloTeamProgression.cs:54), [SoloTeamProgression.cs](C:/Users/pierr/git/Lit/Assets/CombatRealTime/SkillKits/SoloTeamProgression.cs:76), [CycleProgressionService.cs](C:/Users/pierr/git/Lit/Assets/Narrative/Cycles/CycleProgressionService.cs:295).

#### B06 — La progression 666 vers 000 ne possède pas ses six Ancient Flames

**P1 · confirmé dans la campagne inventoriée.** Le profil de session part de 666 et retire 111 par Ancient Flame allumée. Le montage de campagne examiné ne fournit que deux Flames effectivement marquées ancientFlame : celle de Rooms_Flammes et celle de Belmont. Les modèles Brazier utilisés ailleurs ont ancientFlame à 0, sans override de ce champ sur les instances inspectées. Même en gardant les deux flammes présentes et allumées, le calcul s’arrête à 444, avant de considérer les problèmes de déchargement ci-dessous. Les flammes de District_5 hors campagne ne complètent pas ce parcours.

Suite proposée : Établir les six jalons temporels réellement jouables, leurs conditions, leurs régions et leur persistance. Décider séparément quelles lumières sont seulement des braseros.

Sources : [GameplaySessionRoot.prefab](C:/Users/pierr/git/Lit/Assets/Core/System/GameplaySessionRoot.prefab:4171), [AgeManager.cs](C:/Users/pierr/git/Lit/Assets/Gameplay/Temporal/AgeManager.cs:160), [District_1_Rooms_Flammes.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Rooms_Flammes.unity:24977), [District_1_Cycle_Belmont.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/District_1_Cycle_Belmont.unity:31530).

#### B07 — Terminer Belmont retire la flamme qui vient de faire avancer l’époque

**P1 · conséquence déduite du code et des liaisons.** La Flame district1.belmont.first_ancient est dans District_1_Cycle_Belmont. Son allumage est l’étape terminale, puis le cycle demande le déchargement de sa scène. AgeManager compte des composants Flame vivants, pas les jalons archivés. La variable district1.first_ancient_flame.lit est bien écrite, mais aucun CycleActivationId de campagne n’en restaure une flamme permanente. Le succès narratif reste sauvegardé tandis que sa contribution à l’époque disparaît au déchargement ou au prochain recalcul. Le moment exact du changement reste à constater en jeu.

Suite proposée : Conserver le jalon temporel dans l’environnement permanent ou calculer l’époque depuis les jalons persistants, indépendamment des acteurs de la scène de quête.

Sources : [BelmontCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/BelmontCycle.asset:230), [CycleController.cs](C:/Users/pierr/git/Lit/Assets/Narrative/Cycles/CycleController.cs:534), [AgeManager.cs](C:/Users/pierr/git/Lit/Assets/Gameplay/Temporal/AgeManager.cs:306).

#### B08 — L’époque globale dépend du quartier actuellement chargé

**P1 · conséquence déduite du calcul.** AgeManager collecte les Ancient Flames des scènes chargées et recalcule le nombre de sources effectives. WorldRulesStateManager réécrit ensuite world.time.current_year depuis ce calcul. Il n’agrège pas les flammes allumées conservées dans les snapshots des quartiers déchargés. Un voyage vers la Maison, Stillhaven ou les Limbes ne conserve donc pas par construction une époque globale monotone fondée sur les découvertes précédentes. Éteindre une ancienne flamme fait également remonter l’année : ce n’est pas un système de jalons irréversibles.

Suite proposée : Trancher entre époque locale réversible et progression temporelle globale. Le lore et le projet de rejouabilité décrivent actuellement la seconde.

Sources : [AgeManager.cs](C:/Users/pierr/git/Lit/Assets/Gameplay/Temporal/AgeManager.cs:133), [AgeManager.cs](C:/Users/pierr/git/Lit/Assets/Gameplay/Temporal/AgeManager.cs:315), [WorldRulesStateManager.cs](C:/Users/pierr/git/Lit/Assets/Persistence/Save/World/WorldRulesStateManager.cs:177).

#### B09 — Le modèle Brazier ne porte pas le statut Ancient Flame

**P2 · configuration confirmée.** Model_Flame_Brazier contient Flame avec ancientFlame: 0. Ses instances du poste d’Étienne et de Reve_Limbes_Progression ne redéfinissent pas ce champ : la Flame du modèle hérite du comportement commun, notamment du coût par défaut de 10 plutôt que 20 si le coût reste à zéro, et ne compte pas dans AgeManager. Le modèle placé sous la première Ancient Flame de Belmont est aussi commun, mais son parent possède séparément une vraie Flame ancienne : voir B16. Un nom, une aura cyan ou un profil d’éclairage Ancient ne change pas le statut de gameplay.

Suite proposée : Distinguer explicitement brasero commun et Ancient Flame de progression ; harmoniser type, coût, influence et présentation sur les instances qui doivent être anciennes.

Sources : [Model_Flame_Brazier.prefab](C:/Users/pierr/git/Lit/Assets/Prefabs/Model_Flame_Brazier.prefab:10182), [Flame.cs](C:/Users/pierr/git/Lit/Assets/Scripts/Flame.cs:54), [Reve_Limbes_Progression.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Progression.unity:2903).

#### B10 — Deux Flames chargées ensemble partagent le même identifiant de sauvegarde

**P1 · confirmé.** Les instances 1944769635 de Reve_Limbes_Progression et 559643851 de Reve_Limbes_Secrets portent toutes deux flameId = reve.limbes.expansion.Creation.peak. Ces deux scènes appartiennent au même manifeste. CharacterStateStore déduplique sa liste de flammes par FlameId et restaure les états par cette même clé : ce chemin ne distingue pas ces deux états. Le snapshot monde possède de son côté une identité de scène distincte ; une perte effective dépend donc de l’ordre des restaurations et reste à reproduire. Le doublon auteur est certain, pas une perte systématique de toutes les sauvegardes.

Suite proposée : Attribuer une identité distincte à la nouvelle instance et traiter la compatibilité de l’état déjà enregistré.

Sources : [Reve_Limbes_Progression.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Progression.unity:5446), [Reve_Limbes_Secrets.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Secrets.unity:566), [CharacterStateStore.cs](C:/Users/pierr/git/Lit/Assets/Persistence/Save/Character/CharacterStateStore.cs:1374), [PersistentWorldSceneInstaller.cs](C:/Users/pierr/git/Lit/Assets/Persistence/Save/World/PersistentWorldSceneInstaller.cs:809).

#### B11 — Le retour Maison ne garantit plus la recharge de torche annoncée

**P1 · risque d’épuisement confirmé, blocage total à reproduire.** TryResetFlameOnMaisonEntry dépend de GetFlameResetSeconds ; celui-ci renvoie 0 si la construction legacy est désactivée, ce qui est le cas de l’asset de réglages actuel. La recharge automatique au retour est donc inopérante par ce chemin. Les chiffons et autres consommables existent, mais cette réserve est finie ; la seule récompense Item des cycles examinés est un chiffon. À zéro point, allumer une Flame coûte toujours 10/20. La présence de sources déjà allumées ou de ressources restantes peut éviter un blocage : aucune impasse totale de sauvegarde n’est déclarée reproduite ici.

Suite proposée : Fournir un secours renouvelable indépendant de la construction supprimée, puis tester une sauvegarde à zéro point et sans consommables.

Sources : [Zone.cs](C:/Users/pierr/git/Lit/Assets/Environment/4_Zone/Zone.cs:323), [LegacyBuildingSystemSettings.asset](C:/Users/pierr/git/Lit/Assets/Core/Resources/LegacyBuildingSystemSettings.asset:15), [SquadCharacterController.cs](C:/Users/pierr/git/Lit/Assets/Characters/0_Script/SquadCharacterController.cs:757).

#### B12 — L’autel de Veillée placé à la Maison recharge encore Munin

**P2 · confirmé.** Maison_Interactives instancie VigilAltar. Ce composant hérite de MuninChargeReward et échoue sans MuninController sur le personnage. Les prefabs joueurs actuels n’en portent pas ; il ne recharge pas les points de torche. C’est un reliquat actif du gameplay retiré de Munin, pas une solution à B11.

Suite proposée : Retirer cet usage ou lui attribuer un rôle cohérent avec les ressources actuelles, sans réintroduire Munin en gameplay sans décision de conception.

Sources : [Maison_Interactives.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Interactives.unity:1), [VigilAltar.cs](C:/Users/pierr/git/Lit/Assets/Interactive/VigilAltar/VigilAltar.cs:5), [MuninChargeReward.cs](C:/Users/pierr/git/Lit/Assets/Characters/1_Squad/Munin/MuninChargeReward.cs:162).

#### B13 — La généalogie Belmont contient une boucle impossible

**P2 · confirmé dans les données narratives.** FamilyRecord_Eloise_Belmont déclare Étienne comme enfant, et FamilyRecord_Etienne_Belmont déclare Éloïse comme enfant. Les parentIds sont vides dans les deux fiches. Une exploitation du registre produit une filiation circulaire, sans que le lore autorise cette relation ; le texte d’Étienne précise qu’aucune nouvelle filiation n’est introduite.

Suite proposée : Fixer la relation canonique avant de publier une généalogie au joueur ; retirer les liens inventés si cette relation reste indéfinie.

Sources : [FamilyRecord_Eloise_Belmont.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/Data/FamilyRecord_Eloise_Belmont.asset:19), [FamilyRecord_Etienne_Belmont.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/Data/FamilyRecord_Etienne_Belmont.asset:19), [Lore.md](C:/Users/pierr/git/Lit/Docs/Design/Lore.md:222).

#### B14 — Quatre LightSkills affichent le même nom

**P2 · confirmé.** Devastation, Furie, Enchainement et Envol ont toutes displayName = Devastation de lumiere. Leurs rangs de clarté sont bien distincts : il ne faut pas confondre ce rang de combat avec un niveau de progression persistant. Leur présentation ne permet pas de comprendre clairement ce qui change entre les techniques.

Suite proposée : Attribuer les intitulés et descriptions propres aux quatre techniques, puis vérifier leur sélection dans l’interface utilisée en production.

Sources : [LightSkill_1_Devastation.asset](C:/Users/pierr/git/Lit/Assets/CombatRealTime/LightSkills/1_Devastation/LightSkill_1_Devastation.asset:15), [LightSkill_2_Furie.asset](C:/Users/pierr/git/Lit/Assets/CombatRealTime/LightSkills/2_Furie/LightSkill_2_Furie.asset:15), [LightSkill_3_Enchainement.asset](C:/Users/pierr/git/Lit/Assets/CombatRealTime/LightSkills/3_Enchainement/LightSkill_3_Enchainement.asset:15), [LightSkill_4_Envol.asset](C:/Users/pierr/git/Lit/Assets/CombatRealTime/LightSkills/4_Envol/LightSkill_4_Envol.asset:15).

#### B15 — Trois groupes d’objets partagent un identifiant utilisé par la sauvegarde — corrigé le 22 novembre 2026

**Correction appliquée :** `Item_Lannières en cuir` utilise désormais `lannieres_en_cuir`, `Item_Livre_Puzzle_Test_1` utilise `item_livre_puzzle_test_1` et `Item_Note_Limbes_Climb` utilise `item_note_limbes_climb`. Les trois autres assets gardent leurs identifiants historiques. Les références GUID sont conservées. Les anciennes entrées de sauvegarde déjà ambiguës ne permettent pas une migration automatique fiable entre les deux objets d’un ancien groupe. Le constat ci-dessous décrit l’état avant correction.

**P1 · doublons confirmés, mélange à reproduire.** Les lanières en cuir et les morceaux de papier portent morceaux_de_papier ; Les mémoires de Mélodie et Livre_Puzzle_Test_1 portent item_livre ; Note_Limbes_Climb et Note_Ulric portent item_note_ulric. La note des Limbes est présente dans le contenu actuel. ItemRegistry ne conserve qu’une définition par clé et CharacterStateStore reconstruit son inventaire par identifiant : si les deux membres d’un groupe sont enregistrés ou chargés, l’identité peut dépendre du catalogue et de l’ordre de résolution. Le mélange effectif d’une sauvegarde n’a pas été exécuté dans cet audit.

Suite proposée : Attribuer des identités distinctes aux objets distincts et prévoir la compatibilité des entrées déjà enregistrées ; ne pas renommer en masse sans migration.

Sources : [Item_Lannières en cuir.asset](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Lannières%20en%20cuir.asset:15), [Item_Morceaux de papier.asset](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Morceaux%20de%20papier.asset:15), [Item_Livre_Les mémoires de Mélodie.asset](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Livre_Les%20mémoires%20de%20Mélodie.asset:15), [Item_Livre_Puzzle_Test_1.asset](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Livre_Puzzle_Test_1.asset:15), [Item_Note_Limbes_Climb.asset](C:/Users/pierr/git/Lit/Assets/Interactive/Ghost/Ulric/Item_Note_Limbes_Climb.asset:15), [Item_Note_Ulric.asset](C:/Users/pierr/git/Lit/Assets/Interactive/Ghost/Ulric/Item_Note_Ulric.asset:15), [ItemRegistry.cs](C:/Users/pierr/git/Lit/Assets/Persistence/Netcode/ItemRegistry.cs:70), [CharacterStateStore.cs](C:/Users/pierr/git/Lit/Assets/Persistence/Save/Character/CharacterStateStore.cs:1027).

#### B16 — La première Ancient Flame de Belmont contient deux propriétaires de gameplay imbriqués

**P1 · montage confirmé, conflit à reproduire.** La Flame ancienne 1741236592 est sur la racine 1741236591 de District_1_Cycle_Belmont. Le Model_Flame_Brazier instancié sous son Transform 1741236594 conserve aussi son composant Flame actif, de type commun ; aucun retrait de ce composant n’est enregistré sur cette instance. Le cycle écoute la Flame du parent. L’enfant possède son état et son interaction propres, alors que les deux contrôleurs peuvent agir sur une même hiérarchie lumineuse. Interagir avec l’enfant ne garantit donc pas de valider le jalon écouté sur le parent. Le destinataire réellement sélectionné doit être vérifié en jeu.

Suite proposée : Conserver un seul propriétaire Flame pour cette interaction et rattacher explicitement lumière, particules et état persistant à ce propriétaire.

Sources : [District_1_Cycle_Belmont.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/District_1_Cycle_Belmont.unity:31530), [Model_Flame_Brazier.prefab](C:/Users/pierr/git/Lit/Assets/Prefabs/Model_Flame_Brazier.prefab:10182), [BelmontCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/BelmontCycle.asset:230).

### Contenus et boucles encore incomplets

#### N01 — La conclusion générale du jeu n’est pas raccordée

**P1 · contenu manquant.** Le parcours livré possède des conclusions locales, mais aucun cycle de conclusion globale ne relie les étapes principales, l’an 000 et la fin cosmologique de Lucian/Munin. La conclusion des Limbes ouvre un retour Maison ; elle ne termine pas la campagne. Les grands éléments du lore restent réservés à la suite, ce que la documentation des Limbes reconnaît explicitement.

Suite proposée : Définir le dernier objectif jouable et son critère de réussite avant de développer d’autres branches, même si sa présentation reste provisoire.

Sources : [GameDesign.md](C:/Users/pierr/git/Lit/Docs/Design/GameDesign.md:19), [Reve_Limbes.md](C:/Users/pierr/git/Lit/Docs/Design/Reve_Limbes.md:10), [LimbesAngelCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/LimbesAngelCycle.asset:55), [LimbesZoneProgression.cs](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Scripts/LimbesZoneProgression.cs:12).

#### N02 — Gustave est une note, pas un cycle terminé ou terminable

**P2 · contenu manquant.** District_1_Cycle_Gustave est chargée, mais ne contient ni CycleController ni définition de cycle. La note actuelle ne mène pas à une suite d’étapes et à une conclusion au contrat des autres cycles. Cycle_Gustave_Proposal reste une proposition non approuvée ; ne pas la compter comme du contenu jouable.

Suite proposée : Choisir entre conserver une simple vignette d’ambiance et produire le cycle proposé, avec une conclusion et un bénéfice explicites.

Sources : [District_1_Cycle_Gustave.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_Cycle_Gustave.unity:1), [Cycle_Gustave_Proposal.md](C:/Users/pierr/git/Lit/Docs/Design/Cycle_Gustave_Proposal.md:1).

#### N03 — Le portail caché District 1 vers les Limbes n’existe pas encore

**P2 · décision récente non réalisée.** Les portails de campagne repérés relient District 1 à Stillhaven et la Maison aux Limbes ; aucun ne matérialise la nouvelle branche cachée District 1 → secteur caché des Limbes. Le document du 22 novembre laisse explicitement emplacement, condition d’accès et contenu à définir.

Suite proposée : Choisir les deux extrémités, la compétence requise, l’indice d’Observateur et l’intérêt du détour, puis les raccorder au même état partagé de session.

Sources : [GameDesign.md](C:/Users/pierr/git/Lit/Docs/Design/GameDesign.md:266), [narrative.md](C:/Users/pierr/git/Lit/AIAgent/Codex/systems/narrative.md:13).

#### N04 — La rejouabilité souhaitée n’est pas encore une boucle de campagne

**P1 · boucle de jeu manquante.** Les cycles restent résolus et leurs scènes sont ignorées après completion. Le labyrinthe intégré à District 1 contient actuellement deux énigmes, sans boss, butin ni score ; sa résolution de campagne n’est pas réinitialisée. Les primitives de tentative du prototype ne constituent pas un système de défis de campagne avec difficulté, meilleurs temps, reformation et récompenses renouvelables. Une nouvelle partie redonne les découvertes, mais ce n’est pas revenir plus fort dans la même partie sur un défi renouvelé.

Suite proposée : Séparer les résolutions narratives définitives des rencontres répétables. Donner à la Maison un départ de défi et un retour portant un résultat, sans transférer la puissance entre parties.

Sources : [CycleProgressionService.cs](C:/Users/pierr/git/Lit/Assets/Narrative/Cycles/CycleProgressionService.cs:114), [FractureStation.cs](C:/Users/pierr/git/Lit/Assets/Gameplay/FractureLabyrinth/FractureStation.cs:31), [fracture_labyrinth.md](C:/Users/pierr/git/Lit/AIAgent/Codex/systems/fracture_labyrinth.md:30).

#### N05 — Les résolutions enrichissent surtout le journal et ouvrent des accès

**P2 · progression de puissance peu raccordée.** Les 112 étapes portent 19 entrées de récompense : 10 Knowledge, 7 Activation, 1 Item et 1 Skill. La seule Skill est Cicatrice, affectée par B03. Aucun des 62 KnowledgeSO examinés n’a combatBonusEnabled activé. Les cinq cycles de Stillhaven et les trois nouvelles enquêtes des Limbes n’accordent ni Skill ni objet dans leurs définitions. Une récompense narrative peut suffire à un cycle secondaire ; en revanche la montée de puissance souhaitée n’est pas portée par cette chaîne de récompenses.

Suite proposée : Associer quelques déblocages utiles aux étapes majeures existantes plutôt qu’ajouter une économie complexe ; garder des vignettes purement narratives quand c’est volontaire.

Sources : [CycleSteps.cs](C:/Users/pierr/git/Lit/Assets/Narrative/Cycles/CycleSteps.cs:9), [Courts_finaleCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/Courts_finaleCycle.asset:34), [KnowledgeSO.cs](C:/Users/pierr/git/Lit/Assets/Gameplay/Knowledges/KnowledgeSO.cs:67).

#### N06 — La collection des vingt notes n’a pas de conclusion propre

**P2 · finalité de collection absente.** Indépendamment des dix notes manquantes, SecretNotesCycle ne possède que vingt lectures terminales sans récompense, dialogue final, activation ou scène de résolution. Chaque note peut avoir une utilité locale, mais finir la collection ne produit pas de débouché supplémentaire dans cette définition.

Suite proposée : Décider si 20/20 est simplement un objectif de complétion ou la clé d’un dernier détour, puis l’annoncer honnêtement au joueur.

Sources : [SecretNotesCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/SecretNotesCycle.asset:289).

#### N07 — Le journal regroupe les découvertes mais n’explique pas la suite des objectifs

**P2 · orientation incomplète.** KnowledgeCycleGrouping classe les savoirs déjà débloqués par cycle. Il ne présente pas les étapes actives, les sources manquantes, la différence entre fin locale et progression générale, ni les prochaines destinations. Le service connaît ces étapes, mais aucun raccord d’objectifs dans les interfaces inspectées ne forme un fil principal. Cela augmente le risque de prendre un détour sans suite pour une quête bloquée.

Suite proposée : Afficher une prochaine piste narrative par cycle découvert et un objectif général, sans révéler d’avance les secrets.

Sources : [KnowledgeCycleGrouping.cs](C:/Users/pierr/git/Lit/Assets/Gameplay/Knowledges/Journal/KnowledgeCycleGrouping.cs:19), [CycleProgressionService.cs](C:/Users/pierr/git/Lit/Assets/Narrative/Cycles/CycleProgressionService.cs:138).

#### N08 — Plusieurs régions présentes sur disque n’appartiennent à aucun parcours de campagne

**P2 · contenu hors parcours.** District_3, District_4, District_5, District_3_Cathedrale, District_2_Environment_WhiteCastle, CommonAreas, Outdoor et AngelOfJustice ne figurent pas dans les cinq manifestes ni dans les 53 scènes de Build Settings actuelles. Leur présence dans le projet ne constitue pas une suite de Stillhaven ou des Limbes. L’annexe distingue ces scènes des laboratoires et des échantillons.

Suite proposée : Attribuer un statut à chacune : future région, réserve de décor, prototype ou reliquat. Ne pas les supprimer dans le cadre de cet audit.

Sources : [EditorBuildSettings.asset](C:/Users/pierr/git/Lit/ProjectSettings/EditorBuildSettings.asset:7), [ZoneManifest_District_1.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/ZoneManifest_District_1.asset:49), [ZoneManifest_District_2_Courts.asset](C:/Users/pierr/git/Lit/Assets/Scenes/District_2/Courts/ZoneManifest_District_2_Courts.asset:25).

#### N09 — Ulric possède une résolution sans parcours de preuve raccordé

**P2 · reliquat non raccordé.** GhostData_Ulric demande Knowledge_UlricRemainsLocation, mais aucun item ni récompense de cycle examiné ne produit ce savoir. Le prefab interactif d’Ulric et ses restes n’appartiennent pas au graphe actuel de prefabs de campagne ; le décor Props_Note_Limbes_Smoke utilisé dans les Limbes ne rétablit pas cette quête. Il s’agit d’un contenu en attente, pas d’un verrou actuel des Limbes.

Suite proposée : Soit construire la preuve et la rencontre, soit marquer clairement ce contenu comme retiré de la version jouable.

Sources : [GhostData_Ulric.asset](C:/Users/pierr/git/Lit/Assets/Interactive/Ghost/Ulric/GhostData_Ulric.asset:30), [Knowledge_UlricRemainsLocation.asset](C:/Users/pierr/git/Lit/Assets/Gameplay/Knowledges/Knowledge_UlricRemainsLocation.asset:1).

#### N10 — Le vieux collier de Jon porte un savoir de lecture sur un objet non lisible

**P2 · reliquat de données.** Item_Necklace_Jon a readableKind: 0, une connaissance dans knowledgeUnlockedOnRead et aucune dans knowledgeUnlockedOnPickup. Sa connaissance ne peut pas être obtenue par ces chemins normaux. Cet asset n’a pas de placement direct repéré dans la campagne actuelle : le vrai cycle Luc utilise Item_JonCollar, qui débloque bien son indice au ramassage. Ne pas conclure que Luc est bloqué pour cette raison.

Suite proposée : Déclarer l’ancien collier obsolète ou corriger son contrat avant de le replacer dans une scène.

Sources : [Item_Necklace_Jon.asset](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_Necklace_Jon.asset:19), [Item_JonCollar.asset](C:/Users/pierr/git/Lit/Assets/Gameplay/Items/Item_Pickable/Item_JonCollar.asset:39).

#### N11 — Un savoir Belmont est sans source ni usage repéré

**P2 · reliquat de données.** Knowledge_Belmont_lower_flame_network existe, mais aucune référence depuis les composants, objets, cycles et séquences examinés ne l’utilise. La restauration du réseau ne l’accorde pas dans BelmontCycle. Ce n’est pas une connaissance manquante bloquant un prérequis actuel ; c’est une promesse documentaire potentielle restée orpheline.

Suite proposée : Décider s’il doit être accordé lors du réseau restauré ou rester une réserve de contenu explicitement marquée.

Sources : [Knowledge_Belmont_lower_flame_network.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/Data/Knowledge_Belmont_lower_flame_network.asset:16), [BelmontCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/BelmontCycle.asset:204).

#### N12 — Iris a une date de mort renseignée alors que son destin reste ouvert

**P2 · ambiguïté narrative.** FamilyRecord_Iris_Ardent indique deathAge: 666, tout en déclarant dans ses notes qu’aucune mort n’est établie. Le lore réserve son destin à la suite. Le champ n’a pas de marque explicite « date inconnue » ; un affichage administratif naïf transformera cette valeur en certitude.

Suite proposée : Représenter une mort inconnue sans inventer de fin à Iris ; garder la distinction entre défaut technique et fait narratif.

Sources : [FamilyRecord_Iris_Ardent.asset](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Ardent/Data/FamilyRecord_Iris_Ardent.asset:23), [Lore.md](C:/Users/pierr/git/Lit/Docs/Design/Lore.md:235).

### Choix de conception à clarifier

#### C01 — La Maison permet d’aller directement aux Limbes

**Choix déjà documenté · pas un bug.** Portail_des_Limbes dans Maison_Interactives n’exige pas d’avoir résolu Moonrise ou Stillhaven. Il contourne donc leur ordre supposé. Cet accès a été demandé et documenté auparavant ; il ne faut pas lui ajouter arbitrairement un verrou. En revanche, appeler les Limbes « troisième zone » ne suffit pas à garantir cette chronologie narrative.

Suite proposée : Choisir entre régions ouvertes avec révélations compatibles dans tous les ordres, et chronologie guidée. Dans les deux cas, tester les révélations des Limbes avant les cycles de Moonrise.

Sources : [Maison_Interactives.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Maison/Maison_Interactives.unity:547), [PortalController.cs](C:/Users/pierr/git/Lit/Assets/Interactive/Portal/PortalController.cs:115), [Reve_Limbes.md](C:/Users/pierr/git/Lit/Docs/Design/Reve_Limbes.md:68).

#### C02 — Les cycles principaux de Moonrise ne conditionnent pas l’entrée de Stillhaven

**Choix déjà documenté · pas un bug.** Acces_libre_aux_Cours_interieures est une transition sans prérequis de cycle. Belmont, Étienne, Ardent et Clairmont sont classés Main, mais les terminer n’est pas requis pour changer de district. L’entrée libre est un choix déjà acté, pas un oubli à réparer silencieusement. Il manque néanmoins une définition commune de ce que signifie « principal » pour la fin du jeu.

Suite proposée : Conserver la liberté voulue et définir ce que ces résolutions changent dans la conclusion, ou les reclasser si elles sont réellement facultatives.

Sources : [District_1_SecretNotes.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_SecretNotes.unity:725), [Zone_2_Proposal.md](C:/Users/pierr/git/Lit/Docs/Design/Zone_2_Proposal.md:213), [ClairmontCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/ClairmontCycle.asset:18).

#### C03 — La chambre des Limbes est secondaire dans les données mais obligatoire pour le boss

**P2 · incohérence de classement confirmée.** LimbesChambreCycle est Side. LimbesZoneProgression et l’introduction du boss exigent pourtant la completion des cinq cycles d’exploration, dont la Chambre. L’ancienne documentation la décrit encore comme détour facultatif avant de préciser son changement de rôle dans l’extension. Ce n’est pas un cycle annexe au sens d’un contenu que le joueur peut ignorer pour finir les Limbes.

Suite proposée : Aligner sa catégorie et sa présentation, ou retirer cette exigence de l’accès final si elle doit redevenir facultative.

Sources : [LimbesChambreCycle.asset](C:/Users/pierr/git/Lit/Assets/Resources/Narrative/LimbesChambreCycle.asset:18), [LimbesZoneProgression.cs](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Scripts/LimbesZoneProgression.cs:7), [Reve_Limbes_Progression.unity](C:/Users/pierr/git/Lit/Assets/Scenes/Reve/Reve_Limbes_Progression.unity:5015).

#### C04 — Deux déplacements sont acquis avant toute véritable résolution narrative

**P2 · rythme de déblocage à décider.** Observateur et Saut de lumière sont disponibles au départ. Grimpeur se débloque sur entered du Seuil des Limbes, pas sur resolved. Avec l’accès Maison → Limbes, on peut donc obtenir Grimpeur sans finir une enquête de Moonrise. Sauf-Conduit est le seul de ces quatre accès qui exige une résolution complète, celle de Belmont. Ce montage réduit la réserve de compétences à gagner pour revenir plus tard dans les régions.

Suite proposée : Définir quelles capacités doivent être initiales et quelles découvertes doivent inciter à revenir. Ne pas retarder une capacité indispensable au parcours initial sans créer une route alternative.

Sources : [Ability_SautDeLumiere.asset](C:/Users/pierr/git/Lit/Assets/Resources/Abilities/Ability_SautDeLumiere.asset:19), [Ability_Grimpeur.asset](C:/Users/pierr/git/Lit/Assets/Resources/Abilities/Ability_Grimpeur.asset:21), [Ability_SaufConduit.asset](C:/Users/pierr/git/Lit/Assets/Resources/Abilities/Ability_SaufConduit.asset:20).

#### C05 — À quatre joueurs, certains n’ont aucune capacité d’exploration au départ

**P2 · conséquence du choix multijoueur.** Les capacités sont réparties sans doublon entre clients ; seules deux sont réellement disponibles au départ. Deux joueurs sur quatre n’en reçoivent donc aucune avant les déblocages suivants. Après Belmont, tant que Grimpeur n’est pas acquis, il n’y en a que trois pour quatre. De plus le départ du porteur ou une nouvelle arrivée peut redistribuer les capacités. Ce comportement est codé et cohérent avec la répartition actuelle, mais son intérêt coopératif et le retour de traversées individuelles restent à accepter.

Suite proposée : Prévoir un rôle utile pour chacun au début et vérifier les retours de parcours quand le propriétaire d’une capacité change, sans ajouter une capacité nouvelle par défaut.

Sources : [AbilitySaveData.cs](C:/Users/pierr/git/Lit/Assets/Scripts/Abilities/AbilitySaveData.cs:38), [AbilityService.cs](C:/Users/pierr/git/Lit/Assets/Scripts/Abilities/AbilityService.cs:100).

#### C06 — Le jeu du couteau d’Enola est exclusivement solo

**Choix de périmètre · pas un bug.** L’entrée de la fracture, le tabouret et le retour sont soloOnly ; EnolaKnifeGame refuse un NetworkManager en écoute. La récompense est le savoir enola.knife.rhythm, sans bonus de combat activé ni consommation par un autre cycle. Le défi a donc sa boucle locale en solo mais ne constitue pas encore une activité de préparation ou de rejouabilité coopérative.

Suite proposée : Conserver cette activité comme vignette solo assumée, ou définir ultérieurement un rôle multijoueur et une utilité de sa récompense. Aucune conversion réseau n’est prescrite par cet audit.

Sources : [EnolaKnifeGame.cs](C:/Users/pierr/git/Lit/Assets/Gameplay/EnolaKnifeGame/EnolaKnifeGame.cs:64), [WorldActionInteractable.cs](C:/Users/pierr/git/Lit/Assets/Gameplay/EnolaKnifeGame/WorldActionInteractable.cs:16), [enola.knife.rhythm.asset](C:/Users/pierr/git/Lit/Assets/Resources/Knowledges/enola.knife.rhythm.asset:32).

#### C07 — Les règles d’Observateur et d’équipement ne sont plus décrites de façon unique

**P2 · règles contradictoires dans la documentation.** Abilities.md et certains commentaires/contrôles historiques parlent encore d’un emplacement solo, de doublons possibles en multi et d’absence de dés. AbilityService applique l’équipement cumulatif solo et une répartition exclusive en multi. KnowledgePassageDiscovery appelle un d6 persistant avec échec sur 1, puis s’appuie sur les notes comme autre moyen de découverte. AbilitySystemValidation teste encore l’ancienne API Equip, sans établir la distribution réelle du service. Ce test peut réussir en laissant le nouveau contrat non vérifié.

Suite proposée : Mettre à jour la règle auteur et valider les comportements du service réellement utilisé ; conserver la lecture des notes comme secours après un échec d’Observateur.

Sources : [Abilities.md](C:/Users/pierr/git/Lit/Docs/Design/Abilities.md:1), [AbilitySystemValidation.cs](C:/Users/pierr/git/Lit/Assets/Editor/AbilitySystemValidation.cs:32), [KnowledgePassageDiscovery.cs](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/KnowledgePassageDiscovery.cs:59), [SecretPassageAttemptLedger.cs](C:/Users/pierr/git/Lit/Assets/Narrative/SecretPassages/SecretPassageAttemptLedger.cs:45).

### Acceptation en jeu encore nécessaire

#### V01 — Les raccourcis doivent être acceptés après résolution et déchargement des cycles

**Validation nécessaire · pas une panne démontrée.** Les sorties Belmont, Étienne, Ardent et Clairmont possèdent bien des adaptateurs persistants dans les scènes d’environnement. La porte Clairmont est présente dans la scène binaire et ne doit pas être déclarée absente à partir d’une recherche textuelle. Restent à jouer : passage réel, retour inverse, restauration froide, absence de lumière de cycle et chemin NavMesh après retrait des acteurs.

Suite proposée : Faire une sauvegarde avant/après chaque résolution puis traverser dans les deux sens, en solo et avec client tardif.

Sources : [CycleActivationId.cs](C:/Users/pierr/git/Lit/Assets/Narrative/Cycles/CycleActivationId.cs:24), [District_1_NefDesToursInterrompus_Environment.unity](C:/Users/pierr/git/Lit/Assets/Scenes/District_1/District_1_NefDesToursInterrompus_Environment.unity:1), [Bootstrap_District1_Robustness_Acceptance.md](C:/Users/pierr/git/Lit/Docs/Design/Bootstrap_District1_Robustness_Acceptance.md:1).

#### V02 — Les combats de cycles de Moonrise utilisent encore un autre socle que les rencontres récentes

**Validation nécessaire.** Scientifique, ShadowGuardian, DeadWeight, FalseChoir et BrokenAnchor conservent EnemyController et leurs adaptations de boss ; Juggernaut de production et les Gargouilles récentes utilisent Brains. Cette coexistence n’est pas une preuve de panne, mais les succès du laboratoire Brains ne valident pas les morts, contres et callbacks de défaite des cycles historiques.

Suite proposée : Tester chaque boss réellement placé jusqu’à la récompense et au déchargement, sans déduire sa validité du comportement de Juggernaut.

Sources : [Prefab_DeadWeight.prefab](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Etienne/Prefabs/Prefab_DeadWeight.prefab:1), [BrokenAnchor.prefab](C:/Users/pierr/git/Lit/Assets/Scenes/Cycles/Cycle_Belmont/Prefabs/BrokenAnchor.prefab:1), [Juggernaut_Combat.prefab](C:/Users/pierr/git/Lit/Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab:1).

#### V03 — Les overrides de test peuvent cacher les blocages d’exploration

**Validation nécessaire.** GameplaySessionRoot préactive les quatre capacités dans editorTestDefaults. Ce mécanisme est limité à l’éditeur solo et ne s’écrit pas comme progression, mais il rend les essais d’accès beaucoup plus permissifs qu’une nouvelle partie normale. Tester les passages avec ce profil ne valide ni Belmont comme source de Sauf-Conduit ni le déblocage réel de Grimpeur.

Suite proposée : Faire les recettes de progression avec ces quatre cases de test désactivées et une sauvegarde neuve, puis une recette séparée de restauration.

Sources : [GameplaySessionRoot.prefab](C:/Users/pierr/git/Lit/Assets/Core/System/GameplaySessionRoot.prefab:2313), [AbilityService.cs](C:/Users/pierr/git/Lit/Assets/Scripts/Abilities/AbilityService.cs:20).

#### V04 — La continuité de session n’est pas qualifiée par cet audit statique

**Validation nécessaire.** Le projet possède des faits persistants, récompenses idempotentes, mailbox Maison, réplication et arrivées tardives. Leur existence est un point positif, mais cet audit n’exécute ni nouvelle partie complète, ni coupure de processus, ni hôte/client. Les comptes rendus anciens concernent des révisions et parcours précis ; ils ne garantissent pas la version actuellement modifiée.

Suite proposée : Couvrir la reprise à chaque verrou, coffre absent/plein, déconnexion du porteur de capacité, changement d’hôte de sauvegarde et arrivée tardive. La perte d’hôte ne doit pas être assimilée à une migration automatique.

Sources : [CycleProgressionService.cs](C:/Users/pierr/git/Lit/Assets/Narrative/Cycles/CycleProgressionService.cs:331), [current_work.md](C:/Users/pierr/git/Lit/AIAgent/Codex/current_work.md:50).

## Ce qui est déjà correctement raccordé

- Les 53 scènes des cinq manifestes et de Bootstrap/MainMenu sont activées dans Build Settings ; les manifestes de voyage sont enregistrés dans GameFlowService, avec celui de la Maison séparément. Aucun prefab source manquant n’a été détecté sur les instances de ces scènes, y compris dans la scène binaire.
- Les 18 IDs de cycles sont distincts. Le parcours statique des 112 étapes ne montre ni référence d’étape locale absente ni boucle dans leurs prérequis Step. Chaque définition possède une étape terminale ; Nina/Luc sont accessibles au service par le catalogue Resources sans charger leur scène.
- Les 17 cycles avec contrôleur de scène ont une définition raccordée. Le cycle transversal des notes est volontairement alimenté par le service de lecture et n’exige pas un dix-huitième contrôleur de scène.
- Nina : le scientifique n’exige pas de connaître les chimères pour l’interaction du cycle ; sa défaite produit le savoir. La pose Dead de Nina demande les deux connaissances du cycle. nina_spoken pilote sang et Scar. Le souvenir reste non terminal. Le problème relevé ici est l’accès à Cicatrice depuis la palette actuelle, pas une boucle de prérequis dans Nina.
- Luc : le collier utilisé par le cycle accorde l’indice au ramassage, la tombe produit le second, la clôture produit Le destin de Jon. La marge de déchargement existe. Le vieux collier non lisible est un autre asset.
- Les activations de sortie Belmont, Étienne, Ardent et Clairmont sont dans des environnements permanents ; elles ne sont pas toutes enfermées dans les scènes de quête.
- Les objets de preuve lisibles inspectés ont leurs savoirs référencés ; les documents de cycles ne sont pas déclarés cassables. Aucune conclusion « détruire un parchemin bloque forcément la quête » n’est tirée.
- Les noms visibles du cycle Mirage utilisent Alix, Agathe et Abel. Les anciens noms Silas/Alma/Nell subsistent dans des noms de fichiers et de points caméra ; ce n’est pas à lui seul un échec de renommage du texte narratif.
- Les matériaux, textures, scripts de rendu et ressources de packs non reliés à la progression ne sont pas classés comme impasses narratives.

## Ordre de traitement conseillé

1. Rétablir une progression réellement gagnable : choisir la fin générale, établir les six jalons temporels et séparer leur persistance du chargement des scènes (B06–B08, N01).
2. Rendre les récompenses jouables : Cicatrice, recrutement et techniques des kits, secours de torche ; corriger aussi la collision d’identifiants (B03–B05, B10–B12).
3. Fermer les branches annoncées : notes 11–20, deux passages des Limbes, statut de Gustave et de la collection (B01–B02, N02, N06).
4. Fixer le graphe de découverte et de retour : liberté Maison/Limbes, rôle des cycles Main, compétences initiales et portail caché ; conserver les choix déjà approuvés tant qu’ils ne sont pas révisés explicitement (C01–C05, N03).
5. Ajouter une première rencontre renouvelable avec difficulté/résultat/récompense de session, puis qualifier tout le trajet en solo et hôte/client. Les énigmes de fracture existantes peuvent rester une branche d’exploration (N04–N05, V01–V04).
