# Stabilisation Unity — suivi du 8 septembre 2026

Source : erreurs transmises dans pasted-text.txt. Version : Unity 6000.4.9f1.
Les logs initiaux proviennent de la session utilisateur ; ils n'ont pas ete
reproduits en parcours manuel par cette intervention.

| Lot | Constat et traitement | Validation / suite |
| --- | --- | --- |
| 1 Navigation et sol | Attente du service absent reprise ; suspension des agents sur invalidation ; agent scientifique desactive dans le prefab. SphereCast demarre au-dessus des pieds (rayon inclus), contacts initiaux et colliders de personnages exclus. Capsule scientifique alignee sur les pieds. Suppression de la position imposee par le configurateur. | Tests Unity passes : marker a -98,16 m, NavMesh a -98,17 m, ecart mesure 0,007850647 m. Test du sol negatif passe. Aucun deplacement du marker ni teleport de secours. Parcours en jeu restant. |
| 2 Prefabs Opsive | Positioner retire de Luc, Scar et du scientifique (aucun UCC parent). Prefabs joueurs UCC conserves. | Import Unity reussi ; tests de prefab ajoutes. |
| 3 Netcode | Etat solo distinct de NetworkVariable ; transitions serveur apres spawn ; attente annulee, abonnements retires puis retablis au bon cycle. Correctifs NGO de destruction existants conserves. RPC migres avec memes permissions et validation metier. | Compilation C# et import/ILPP Unity reussis. Tests de destruction NGO et de transitions solo passes ; host/client, late join et Play/Stop encore requis. |
| 4 Animation | Courbe Speed retiree du clip crouch Lucian. Le panneau rejoue son etat existant au lieu d'appeler un trigger absent. Transition Nina isDead deja corrigee. | Import Unity reussi ; test de courbe ajoute. Verification visuelle du panneau et de la locomotion requise. |
| 5 References | References explicites des deux prefabs world-info dans InventoryUISettings (Bootstrap/Arena), chemin editeur actualise, reprise de resolution tardive. Maison initialise ses coffres lorsqu'elle apparait ; l'initialisation de squad n'exige plus un stockage absent d'une scene de district. Postprocesseur Archer ignore sa bibliotheque absente et convertit les GUID en chemins. | Compilation reussie. Timeline/binding Nina restent des ressources auteur a assigner ; ne pas inventer de cinematique. Verification en build des panneaux requise. |
| 6 Sauvegarde | Deux flammes distinctes du corridor partageaient scene-flame:Maison:0DE640398. Seule l'identite de la copie a ete changee ; identite historique conservee. | Test d'unicite ajoute. Verifier allumage independant, sauvegarde/rechargement et sortie. Les anciennes sauvegardes ne peuvent pas distinguer deux objets qui avaient la meme cle. |
| 7 Rendu | Le shader Ghost conserve alpha/revelation, mais desactive le brouillard transparent qui active l'absorption d'eau sans buffer disponible. Träd conserve ses materiaux et utilise un LOD de maillage plutot que les billboards Nature legacy. | Import/reparation Unity reussis. Le brouillard n'affecte plus cet hologramme ; verifier rendu et cout a distance de l'arbre avec GPU. |
| 8 API et nettoyage | Migration recherches d'objets, EntityId (cles completes, pas de conversion int), RPC, TMP et ShaderUtil. Migration ItemSceneMarker via donnees serialisees. Branches mortes de la flamme active retirees. | Compilation reussie. Champs encore references par les outils de migration ou explicitement de compatibilite conserves, sans suppression de warnings. Il reste 7 CS0414 de compatibilite (5 champs UCC utilises par le migrateur, reactionChoiceWidth et useAgeManager). Les 16 autres champs inutilises ont ete retires. Aucun CS0618 ni CS0162 dans la derniere compilation runtime. |

## Resultats automatises

- Import et compilation Unity avec ILPP reussis.
- 79/79 tests EditMode passes : stabilisation, Nina, aggro, menu/session et les trois cas de destruction NGO.
- Les premiers passages ont aussi revele deux attentes de tests obsoletes : chemin du parchemin Edouard et precision float de la borne 0,2 s. Corrigees sans modification du comportement de combat.
- 17 tests UI/clarté supplementaires valides (hors doublons), soit 96 tests distincts passes sur les derniers rapports par test. La fixture UiPanel initialise explicitement Awake en EditMode.
- Rapport consolide : `Library/Stabilization/verified-summary.json`. Les rapports des premieres tentatives restent disponibles et ne constituent pas le resultat final.

## Preuves

- Logs de compilation et import : `Library/Stabilization/` (artefacts locaux non versions).
- Regressions ajoutees : `Assets/Editor/Tests/RuntimeStabilizationTests.cs`.
- Reparation explicite de l'arbre : menu `Lit/Validation/Repair Legacy Terrain Tree LOD`, execute avec succes.
- D'autres changements Nina/combat/audio et des commits ont eu lieu pendant
  l'intervention. Ils ont ete preserves ; le diff Git final n'est pas exclusivement
  celui de cette stabilisation.

## Acceptation restante

Rejouer nouvelle partie et ancienne sauvegarde, scientifique/dialogue/combat,
lecture puis ramassage, Nina, sauvegarde et retour menu ; ensuite host/client
distant et late join. Faire Play/Stop avec Domain Reload actif puis inactif.
Verifier les panneaux, Ghost et arbre avec rendu GPU. Aucun parcours manuel
ni resultat multijoueur ne doit etre deduit des seuls tests EditMode.
