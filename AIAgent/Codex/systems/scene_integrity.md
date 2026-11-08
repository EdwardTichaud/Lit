# Integrite des scenes de jeu

## Qualification fonctionnelle et suite (2026-10-29)

Les corrections portent sur les objets et interactions du parcours actuel, ainsi
que sur AnimationLab, qui reste un outil utilise bien que hors build. Une question
de perimetre est ouverte pour Arena, CommonAreas, Outdoor et WhiteCastle : conserver
leurs defauts documentes comme prototypes, ou remettre aussi ces scenes en etat.
Leur absence des Build Settings ne vaut pas autorisation de les supprimer.

Les VFX Dark/Fire/Ice Fire/Mist/Rage gardent leurs graphes et reglages. Seule leur
reference a un ancien mesh de demonstration disparu est retiree. Le chemin
MeshSetup.SetupRenderer fournit le vrai MeshFilter ou SkinnedMeshRenderer ; les
cinq graphes sont testes dans les deux modes. Aucun mesh d'acteur n'est remplace.

LightBall, Grapple et Trail utilisent une texture de lueur radiale douce deja
presente (SrRubfish FX_TX_GlowADD_01). La propriete Sprite Texture / _MainTex est
connectee a Alpha via son canal rouge. Controle GPU : bords noirs, centre lumineux,
transition progressive ; bruit, teinte, UV et intensite sont conserves.

Sept controles de particules sans rendu propre passent explicitement en mode
None, leurs emissions et enfants restant intacts. Le controle de Lightning est
Pos/Thunder, pas la racine du prefab. Sa racine conserve son ancien materiau et
son mode ; la correction finale cible uniquement le controle imbrique.

SceneIntegritySkinUsage suit les references d'objets resolues depuis les 53 scenes
activees, les ScriptableObjects et les racines Resources ; il exclut les pointeurs
d'heritage prefab, qui ramenaient les materiaux importes remplaces par des overrides.
Resultat : 854 assets, 121 prefabs et 733 ScriptableObjects, zero echec ; 40
affectations de peau, toutes sur les quatre materiaux HQ Standard, sans SSS.
La recette Play confirme ce mode sur les renderers reellement presents. Les cartes
SSS/epaisseur signalees sont donc inactives dans cette configuration ; conserver
leurs references source pour une eventuelle reactivation, qui necessitera les cartes.

Les sockets hand_l_items/sword des trois presentations sont explicitement vides,
sans reference de mesh perdue ni collider ; leurs corps visibles restent presents.
Le mesh ProBuilder du balcon est genere a l'instanciation avec collider identique.
Ces etats d'auteur ne doivent pas etre corriges en ajoutant des armes ou des cubes.

Validation : 36 tests passes (complete-integrity-tests-verified.xml), dont quatre
captures GPU de geometrie (render-checks/). Le raster de controle montre les vrais
buffers de vertices/indices, pas un remplacement de la surface par un placeholder.
Il ne constitue pas une comparaison artistique des materiaux finaux en situation.
Recette Play 41103eaab9914592b708f741c41c4a7e passee/restauree : 227 controles,
zero erreur runtime, surface physique du balcon, peau live, dix notes/passages,
transitions, sauvegarde et reprise. Le dernier inventaire / triage est consolide
dans native-current-qualified.json et triage-current-qualified.json.

## Registre de resolution des 97 signalements

Le registre resolution-ledger.json relie chaque signalement initial aux corrections,
aux constatations natives et aux tests. Il conserve les empreintes des fichiers
utilises comme preuves. Il ne transforme pas un signalement sans preuve en succes.

- 31 corriges et absents du nouvel inventaire natif.
- 20 verifies non bloquants actuellement : 16 cartes SSS/epaisseur non utilisees,
  trois sockets d'armes explicitement vides, un mesh ProBuilder genere a l'instanciation.
- 46 hors parcours actuel : anciennes scenes Arena/CommonAreas/Outdoor/WhiteCastle,
  emission inutilisee d'un materiau ancien et deux microdetails IdaFaber. Le choix
  de les conserver comme prototypes ou de les reparer egalement reste ouvert.

L'audit brut compte donc 66 signaux : 20 dependances qualifiees, 43 dans les scenes
hors build et 3 dans leurs dependances exclusives. Aucun directement dans une scene
activee ; aucun nouveau signal de jeu non qualifie. Les 46 hors parcours restent
bien des defauts d'assets : ce classement ne les repare pas et ne les supprime pas.
Le perimetre integral du but ne doit pas etre declare termine avant la clarification
utilisateur sur leur traitement. AnimationLab a ete traite comme outil utilise.

Detail de l'inventaire hors parcours : prototype-impact.json regroupe les 46
signaux sur 20 objets/materiaux distincts. Cinq objets de CommonAreas ont perdu
a la fois leur modele et leur MeshCollider ; cinq autres objets ont un materiau
absent. Quatre renderers de particules sont des controles desactives, deux images
UI sont conditionnelles, un eclairage a perdu ses textures, et trois materiaux
ont une carte de detail/emission manquante. Les compteurs bruts ne representent
donc pas 46 blocages independants. Ce classement est issu des composants natifs,
sans sauvegarde ni modification de ces anciennes scenes. Il precise le travail
a faire selon le choix utilisateur ; il ne remplace pas ce choix.

Les renderers de controle sont maintenant non rendus par contrat (mode None) :
les sept corrections ne changent que ce champ, pas les enfants ou emissions.
Le test final vise explicitement Pos/Thunder pour Lightning et confirme que sa
racine n'a pas ete remodelee. Validation finale : 36/36 tests, zero echec/ignore ;
quatre images GPU inspectees et recette de 227 controles passee/restauree.

## Priorisation campagne / prototypes (2026-10-29)

SceneIntegrityTriage lit le rapport natif et les Build Settings actuels. Les scenes
hors build sont identifiees explicitement ; une dependance partagee reste prioritaire
si une scene active la reference. Un atelier hors build n'est pas un ancien niveau
abandonne. Le graphe de dependances est conservateur : il peut inclure des sources
de prefabs dont les instances de scene remplacent deja le materiau ou le mesh.
Le rapport triage.json garde usedBy et les constatations natives pour chaque signal.

Repartition des 97 signaux initiaux : 4 directement dans les scenes actives,
44 dans leurs dependances, 44 dans des scenes hors build et 5 dans des dependances
exclusives hors build. Ces derniers concernent Arena (5), CommonAreas (31),
WhiteCastle (2), Outdoor (5), AnimationLab (1), ainsi que M_FX_fire_dungeon,
les deux microdetails IdaFaber et les defaults de mesh des effets Dark/Fire.
Ils restent documentes, sans suppression des scenes sources.

Premieres corrections de la campagne : le sol Balcony_1 herite du materiau Ice
MOD_Floor_01_O_straight_med deja present, avec sa geometrie ProBuilder et son
collider conserves ; deux cadres UI gardent leurs sprites et utilisent explicitement
UI/Default ; le logo decoratif de fabrication sans sprite est desactive ; une
liaison d'une piste supprimee de GiantJuggernaut_Intro, sans cible, est retiree.
Sauvegardes et provenance : campaign-reference-repairs.json et CampaignReferenceBackups.

Les tests SceneIntegrityCampaignTests examinent les vrais assets sauvegardes,
leurs sprites/materiaux et la geometrie/collision du balcon. Le parcours campagne
controle maintenant aussi par raycast la surface de marche du balcon.
Loading_Orbe : spheres absentes remplacees par la sphere Unity deja utilisee dans
l'effet ; dernier Burst retabli vers le meme sous-mesh BasicShapes que ses voisins.
Sept tests passes (campaign-tests-portals.xml), dont les trois templates de
portails. Les surfaces 2/3 utilisent le Plane deja present sur leur collider ;
le portail 5 utilise le mesh deja assigne dans Maison. Les transformees,
colliders d'interaction et materiaux restent inchanges. L'audit distingue les
freres homonymes par leur index et controle la geometrie reelle des emetteurs
mesh actifs. L'ancien total de 97 peut donc omettre des doublons ; les nouveaux
comptages ne doivent pas etre interpretes comme une simple soustraction.
Recette Play 64da5342942e4d1e82f21c737c042f80 passee/restauree : 220 controles,
zero erreur runtime, raycast de la surface de marche du balcon, interactions,
transitions et reprise de sauvegarde. Rapport campaign-repaired-play-result.json.
L'incompatibilite du masque tactique du balcon a disparu ; les neuf elements
de Rooms/Nef restent signales. Pas de certification artistique ni multijoueur.

Audit/triage actualises apres ce lot : 80 signaux (native-campaign-phase1.json),
contre 97 avant corrections. Aucun directement dans les scenes de jeu actives ;
31 dans leurs dependances, 44 dans les scenes hors build, 5 dans leurs dependances
exclusives. Les 31 dependances restent a qualifier : cartes SSS/epaisseur de peau,
slots d'armes vides des presentations, racines de particules desactivees, mesh
ProBuilder genere et defaults de VFX. Ce resultat ne les declare pas tous resolus.

## Audit et reparation (2026-10-28, travail en cours)

Objectif : examiner les scenes de jeu, leurs prefabs et dependances, restaurer les
assets manquants et corriger les incoherences verifiees. Ne pas assimiler le seul
parcours District_1/District_2 a une validation de toutes les scenes.

SceneIntegrityAudit ouvre toutes les scenes de Assets/Scenes dans des previews,
sans les sauver. Il examine les prefabs et materiaux de leur graphe de dependances,
les references serialisees cassees, scripts absents, shaders manquants, slots de
materiaux et MeshFilters vides, ainsi que les definitions de cycles et les noms
de scenes en Build Settings. Rapport natif : Library/SceneIntegrity/native.json.
Les emplacements vides doivent etre examines : un slot de trails inutilise peut
etre volontaire ; ne pas masquer une reference cassee par une exception globale.

SceneIntegrityRepair est un outil auteur explicite, jamais un correctif runtime.
Il sauvegarde uniquement les assets modifies. Il repare un MeshCollider casse
depuis un unique maillage visuel dans le meme espace local ; ne pas affecter
directement un mesh enfant transforme. Un override casse ne revient a sa source
que si le prefab fournit une reference valide. Il retire uniquement les anciennes
proprietes de textures cassees absentes du shader actuel, sans vider les textures
actives. Les scenes non sauvegardees bloquent la reparation.

## Assets Dungeon_Environment retrouves

Le premier audit statique identifie 13 GUID de prefabs absents, 1 214 instances
et 64 GUID non resolus dans 65 scenes. Douze prefabs Dungeon_Environment et leurs
dependances ont une copie locale exacte sous Library/ActorAnimationSandbox.
Restauration dans leur emplacement importe d'origine avec leurs .meta et fileIDs,
sans remplacer les scenes ou leurs transformations. 64 assets (environ 450 Mo)
restaures, empreintes et provenance dans recovery-report.json. Cela ramene les
prefabs de scenes absents a un, le Props_Note_Ulric_Smoke de District_5.
Ce dernier et son materiau ont ensuite ete recuperes dans Git avec leur GUID,
avec d'autres assets UI/VFX disparus. Rapports git-recovery*.json.

Ces imports sont sous Assets/0 - UnityPackages, deja exclu de Git par la politique
du projet : ils sont presents localement, pas implicitement versionnes. Ne pas
creer de substituts vides pour faire disparaitre les erreurs de prefab.

Les colliders originaux du pack avaient aussi des references de sous-maillages
invalides. La reparation native corrige dix prefabs depuis leurs visuels valides,
ce qui restaure leurs instances sans nouvelle identite de gameplay. Premiere
passe : 50 corrections verifiees, detail dans deterministic-repairs-first.txt.
Le rapport natif initial apres recuperation du pack comptait encore 769 signaux
(references, materiaux, meshes) ; ce nombre n'est pas un etat final.

## Recuperations et corrections complementaires

97 dependances VFX originales ont ete retrouvees dans les archives Asset Store
locales, avec leurs GUID ; neuf textures blanches et des assets UI ont aussi ete
recuperes dans Git. Les pointeurs Git LFS seuls ne sont pas importes comme images.
Les rapports de provenance sont conserves dans Library/SceneIntegrity.

Treize materiaux Lucian dont le shader avait disparu utilisent la conversion
HDRP deja presente dans LucianCC5HqIntegration. Elle relit maintenant les textures
et couleurs serialisees meme en l'absence du shader initial, pour conserver les
cartes et la transparence. Le shader Test de WhiteCastle a ete adapte aux signatures
LightLoop et aux variantes d'ombres de la version HDRP installee.

FlameGuidanceArcRenderer reutilise les deux LineRenderers auteurs et leur materiau
au lieu de creer des doublons runtime. La reparation ajoute un materiau partage
aux lignes qui n'en avaient pas. Quatre tests d'editeur passent : conservation
des cartes/couleurs/alpha, collisions dans le bon espace local et reutilisation
des lignes. Rapport repair-tests-final.xml. Cela ne certifie pas le rendu visuel.

La recette BootstrapDistrictPlayProbe ne tolere plus les anciennes erreurs de
prefabs Dungeon_Environment ni les erreurs d'ouverture des cinq scenes D1
precedemment exemptees. Les anciens rapports gardent leur valeur historique ;
une nouvelle recette stricte est necessaire apres les restaurations.

Cette recette stricte a passe 213 controles, sans Error/Exception/Assert runtime,
avec restauration des scenes sources (run 7aac05f5c4f1446497fb9b303f78c467,
campaign-strict-result.json). Elle couvre les portails, dix notes et passages,
les cycles de Stillhaven, la sauvegarde production et sa reprise depuis le menu.
Les approches UCC/dialogues restent des fixtures ; aucune certification de toutes
les collisions, du rendu, des autres districts ou du multijoueur n'en decoule.

## Validation restante

Dernier rapport natif : native-fourth.json, 65 scenes / 222 prefabs / 2 111
materiaux, 97 points encore ouverts. Aucun prefab, script ou shader manquant
detecte par cet audit. Les 84 renderers/slots facultatifs identifies sont
consignes separement ; leurs references cassees restent controlees.
La racine VFX_RainSplash (emission desactivee, 31 enfants) utilise explicitement
Render Mode None. Ses enfants gardent leurs materiaux et leur emission.
Trois anciens slots mesh de particules non rendues en mode Mesh sont nettoyes.

Le premier parcours strict avait encore deux avertissements natifs NavMesh :
la scene Nina imposait m_Enabled=1 sur l'agent du scientifique, alors que son
prefab est desactive par defaut. Cet override est retire ; EnemyController
conserve son chemin d'attente/inscription via NavMeshWorldService. Le prochain
parcours controle aussi l'absence de cet avertissement. Recette finale
45ff570e28814bcc9eff0d16b02ac875 passee/restauree : 228 controles, zero erreur
runtime et zero avertissement de creation d'agent sans NavMesh.
Rapport campaign-final-result.json. Les six avertissements restants concernent
les materiaux de dix elements architecturaux D1 incompatibles avec le masque
tactique, rencontres deux fois : Rooms (7), Balcony (1), Nef (2). A traiter
avec le reste de l'audit, sans annoncer une qualification visuelle globale.

Relancer le rapport natif apres chaque lot, examiner les references restantes
et leurs usages, corriger les vrais defauts plutot que reduire le perimetre.
Verifications physiques/visuelles puis recette Play de la campagne exigibles
apres la restauration des collisions. Ne pas conserver l'ancienne exemption
des erreurs de prefabs abc000 dans la recette comme preuve de succes final.
