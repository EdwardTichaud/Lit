# Calibration du regard — premier lot, laboratoire uniquement

Lucian et Juggernaut restent Humanoid. Werewolf et StoneGolem restent Generic
avec leurs animations natives. Werewolf ne possede pas de course native : aucun
Run TwinBlades ne lui est attribue.

## Contenu et limites

Onze copies dediees : Idle / marche avant / course avant sur les deux Humanoid
et StoneGolem ; Idle / marche sur Werewolf. Aucune reference des controllers,
prefabs de production, SkillSO, vitesse physique ou trajectoire n'est modifiee.

Les appuis TwinBlades vont deja vers l'avant. Un offset de rotation globale
de 40 a 60 degres tournerait aussi les jambes : il n'est pas applique. Les
copies Humanoid corrigent seulement les deux muscles de rotation horizontale
du cou et de la tete. Le torse conserve sa garde de trois quarts, les bras et
les jambes leur animation. Les copies Generic conservent toutes leurs courbes.

Les offsets musculaires sont propres a chaque avatar. Ils ne sont ni des
degres ni des offsets Root Transform Rotation. Cette premiere correction ne
doit pas etre generalisee aux frappes ou aux autres directions sans mesure.

## Utilisation

Menu `Lit > Animation Presentation > Facing Calibration` :

1. **Audit Native Rigs (Read Only)** : mesures a 60 Hz, rapports dans
   `Library/ActorFacingCalibration`, sans modification des assets.
2. **Prepare Comparison Lab** : prevalidation des onze sources, generation des
   copies via les API Editor, controle apres reimport, creation du laboratoire.
3. Ouvrir `FacingCalibrationLab.unity` puis Play : selection Idle / marche /
   course, pause et repetition des clips. Premiere rangee = sources ; seconde
   = copies. L'aperçu ne contient aucun script de gameplay, VFX, IK ou
   `LucianCombatTorsoFacing`. Aucun evenement gameplay n'est execute.
4. **Capture Actual Models** : captures HDRP source/copie a 35 % du cycle,
   vraies geometries et materiaux, dans le dossier Library du rapport.
5. **Audit First Lot Transitions** : 80 raccords a quatre phases, sur les vrais
   avatars ; conservation des pieds, bassin et root. Les phases marche/course
   sont mesurees individuellement ; le lecteur conserve le clip sortant pendant
   son fondu, sans le remettre artificiellement a sa premiere frame.
6. **Audit Active Clip Consumers** : controllers effectifs (overrides resolus)
   et catalogue conservateur des skills/overrides de Lucian. Une entree de ce
   catalogue ne prouve pas a elle seule qu'un clip est actif en jeu.
7. **Prepare Directional Candidates** : les huit directions marche/course des
   deux Humanoid. 32 references : quatre deja alignees conservent leur source,
   deux reutilisent le premier lot, 26 copies supplementaires sont generees.
   La petite oscillation de regard auteur est conservee : moyenne corrigee
   a moins de 0,1 degre, amplitude bornee a 5 degres pour ce lot directionnel.
   La limite plus stricte de 3 degres du premier lot reste inchangee.
8. **Capture Directional Candidates** / **Audit Directional Crossfades** :
   captures des 32 references et 256 raccords voisins, avec les phases et
   vitesses locales deja inscrites dans les Blend Trees. Les masques, seuils,
   parametres, transitions et controllers de production restent intacts.

`Rebuild Comparison Scene` reconstruit explicitement la scene fermee ; la
preparation normale conserve une scene deja creee. Sauvegarder toute scene
sans nom avant la preparation. Les copies existantes gardent leurs GUID.

## Verification et suite

Premier passage Unity principal 6000.6.4f1 : onze clips prevalides et reimportes,
aucune source/metadonnee modifiee, zero fichier de clip ou meta change par une
seconde preparation. Ecart maximal des pieds : 0,00017 mm ; regard Humanoid
maximal : 2,54 degres. Le manifeste et la verification sont conserves sous
`Editor/FirstLotManifest.json` et `Editor/FirstLotVerification.json`.
Onze captures HDRP ont ete produites a phase identique avec controle des pieds
et projection orthographique. Elles montrent des poses statiques, pas une
validation des raccords ou du contact au sol en mouvement.

Validation Play Mode du lecteur, sur les quatre rigs reels dans le mini-projet
isole : 23 717 assertions et 657 frames comparees par passage, avec puis sans
Domain Reload. Pause, changements d'allure, demandes rapides et trois cycles
de desactivation/reactivation passent. Ces tests n'entrent jamais en Play Mode
dans l'editeur principal. Invocation batch : `FacingCalibrationPlayValidation.Run`
ou `FacingCalibrationPlayValidation.RunWithoutDomainReload`. Les rapports
precisent explicitement qu'ils ne qualifient pas le gameplay UCC/NavMesh.

Le controle des courbes auteur evite les reecritures dues aux seuls caches
natifs de Unity. Une seconde preparation directionnelle ne modifie aucun clip
ni meta. Les poses sont tout de meme controlees apres reimport. Les ecritures
sont limitees aux copies generees, sans sauvegarde globale des assets dirty.

Points ouverts mesures : quatre correspondances de phases directionnelles
ont une correlation inferieure a 0,75 et demandent une verification des contacts ;
aucun offset n'est applique automatiquement aux controllers. Une ancienne
reference `Twinblades_Idle_Inplace_LucianAligned` est manquante dans Player_Model :
pas de transition ou d'etat par defaut entrant trouve dans le fichier, aucune
reparation devinee. Les sorties d'attaque du Juggernaut restent un lot distinct :
sans IK, ecart final des semelles de 5 a 6 cm ; avec l'IK Humanoid existante,
Strike/Followup deviennent des candidats de recuperation, mais Sweep reste
au-dessus du seuil de 3 cm. Aucun raccord retime n'est active sans comparaison
rendue et verification du rythme/impact.

Audit des semelles complementaire : les quatre cas restent ambigus avec et
sans IK native. Les controllers actuels ont Foot IK desactive ; aucune IK n'est
activee silencieusement. Sur Walk_L du Juggernaut, le pied droit ne rejoint pas
la hauteur de l'Idle dans la tolerance mesuree, donc un simple cycle offset
ne suffit pas. Voir `Editor/UncertainContacts.json` et les CSV dans Library.

Une experience en memoire sur RootT.y arrive a remettre le pied le plus bas a
hauteur constante, sans changer les rotations des articulations, la geometrie
relative jambes/bassin ou le root physique. Elle n'est PAS retenue : l'amplitude
verticale du bassin passe par exemple de 7,6 a 15,6 cm sur Walk_L, avec des
corrections pouvant approcher 10 cm. Aucun de ces clips experimentaux n'est
enregistre. Les variantes d'extraction verticales Unity ne donnent pas non plus
de correction globale acceptable. Les comparaisons sont archivees dans
`Editor/RejectedGroundingProbe.json` et `Editor/VerticalExtractionProbe.json`.

`Prepare Actor Preview Bindings (Not Installed)` genere deux controllers
d'override dedies, sans modifier les graphes de base ni les prefabs. Tous les
slots effectifs non calibres sont conserves, y compris les remplacements de
torche. L'Idle gameplay propre au Juggernaut est calibre depuis sa copie
effective, en conservant ses evenements et ses reglages. Werewolf et StoneGolem
gardent directement leurs controllers Generic natifs. Ces bindings restent des
assets de test non installes : contacts, recuperations et UCC/NavMesh doivent
encore etre qualifies. Voir `Editor/PreviewBindings.json`.

Le manifeste conserve les GUID/sous-assets, empreintes source/metadonnees,
avatar, evenements, offsets et resultats. La preparation refuse un changement
de duree, cadence source, extraction, evenement, ou un ecart de pieds/bassin
superieur a 1 mm. Le regard des copies Humanoid doit rester a moins de 3 degres
de l'avant. Cette mesure est horizontale : l'inclinaison verticale de la tete
n'est pas comptabilisee comme un desaxage.

Les appuis mesures identiques a la source ne prouvent pas une absence de
glissement au sol. Le laboratoire compare les poses sur place, sans UCC ni
NavMeshAgent. Il reste a valider les raccords et la locomotion avec ces vraies
autorites dans Iluvilirae_Test, le comportement avec torche, les autres
directions et les actions. Les marqueurs d'impact et l'orientation des armes
doivent etre controles avant toute integration aux controllers du jeu.

Retouches des quatre marches (choix artistique confirme)
-----------------------------------------------------
`Prepare Four Walk Retouches (Lab Only)` cree des copies sous `RetouchedWalks` :
Lucian Walk_BR, Juggernaut Walk_B, Walk_L et Walk_FL. La retouche utilise une
resolution des jambes hors runtime puis enregistre les muscles Humanoid ; elle
n'ajoute aucun verrouillage des pieds, IK runtime ou deplacement physique.
Une enveloppe lisse corrige la partie basse des pas, en conservant les arcs
des pieds leves et l'orientation auteur des chevilles. Les fenetres d'appui
deduites de la hauteur restent des hypotheses a verifier en mouvement reel.

La reconstruction du centre de masse Humanoid est compensee pour conserver
la trajectoire du bassin. Exception explicite : Walk_L Juggernaut abaisse sa
posture de 3,5 cm constants pour donner assez de portee aux jambes ; son
oscillation est conservee. Ce choix de pose reste a qualifier sur les raccords.
Les copies de regard et leurs overrides ne sont pas remplaces par ces copies :
leur composition et leur integration restent a faire apres qualification.

Le contrat de retouche est distinct du contrat strict des copies de regard.
Il controle bassin, haut du corps, racine physique, orientations des pieds,
contact vertical aux frames et entre les frames, boucle, evenements et sources.
Un controle echoue bloque l'ecriture du clip concerne. Les captures utilisent
les vrais meshes et materiaux ; elles ne prouvent pas la cadence, les contacts
horizontaux ou la qualite des transitions UCC/NavMesh. Rapports detailles :
`Library/ActorFacingCalibration/walk-retouch-*.json`.

Execution / Executed1
---------------------
`Calibrate Named Execution Clips` configure uniquement les deux copies FBX
nommees par l'utilisateur : Execution1_Humanoid en Humanoid et Executed1_Generic
en Generic, Based Upon Original, offset rotation 0 et rotation bakee. Les
fichiers fournisseur sans suffixe restent intacts. Les GUID et fileID existants,
evenements, durees et cadences des deux FBX sont verifies apres reimport.

Pour StoneGolem, utiliser **Assets/StoneGolem/Animations/Calibrated/
Twinblades_Attack_Executed1_Inplace_Generic.anim** : le FBX Generic TwinBlades
ne correspond pas directement aux chemins de ses os. Le menu produit ce .anim
par retarget Humanoid hors runtime sur une copie du squelette StoneGolem puis
enregistrement de courbes Transform natives. Modele, prefab, controller et
avatar Generic de production ne sont pas modifies. Les helpers sont dans
`Execution/EditorSources` ; ils ne sont pas des rigs de remplacement a installer.
La capture a 480 Hz conserve la duree et la vitesse de lecture. Les courbes
sont simplifiees avec tolerance locale puis controlees sur le vrai rig Generic,
aux cles, aux milieux et a des quarts d'intervalles independants. Seuils :
5 mm de position, 0,5 degre de rotation, aucun mouvement physique ; evenements
et empreintes du modele/prefab proteges. Aucun IK ou retarget runtime ajoute.
Rapports : `Library/ActorFacingCalibration/execution-clips.json` et
`executed-stonegolem.json`. La qualite rendue et le raccord de l'execution dans
son contexte de jeu restent une verification visuelle distincte.
