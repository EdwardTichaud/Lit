# Phase 2A - Audit et garde-fous du systeme de glace

Date : 2026-07-18  
Unity : 6000.4.9f1  
Perimetre : maillages barycentriques, influences locales et transitions runtime.

## Etat mesure avant migration de contenu

- 1 092 maillages generes analyses.
- 2 271 026 108 octets au total (2,115 Gio).
- 1 089 maillages conformes.
- 3 maillages hors budget.

| Maillage | Sommets generes | Taille serialisee | Sortie barycentrique predite |
|---|---:|---:|---:|
| `Model/Mesh_IceEdges_Model.asset` | 5 999 997 | 912 003 035 octets | 5 999 997 |
| `SM_MERGED_BP_PathRocky_01_2_LOD0/Mesh_IceEdges_SM_MERGED_BP_PathRocky_01_2_LOD0.asset` | 481 068 | 88 521 098 octets | 481 068 |
| `sm_PathRocky_01_01_Splines_1_LOD0/Mesh_IceEdges_sm_PathRocky_01_01_Splines_1_LOD0.asset` | 481 068 | 88 521 097 octets | 481 068 |

Budgets appliques : 150 000 sommets et 25 Mio par maillage genere.

## Protections ajoutees

- estimation du nombre de sommets avant toute allocation barycentrique ;
- serialisation temporaire et verification de la taille avant remplacement d'un asset existant ;
- refus d'utiliser un maillage genere comme source d'une nouvelle generation ;
- correction du repli qui pouvait rebaker recursivement un maillage deja genere ;
- audit JSON disponible depuis le menu Unity et en ligne de commande ;
- validation CI bloquante si un asset depasse un budget ;
- lecture des en-tetes serialises sans charger tous les maillages en memoire ;
- maximum runtime de quatre influences locales, choisies par distance au renderer ;
- mise a jour par frame limitee aux renderers ayant une transition active.

## Validation

- compilation Unity Editor : reussie ;
- tests EditMode `Lit.Performance.Tests` : 19/19 reussis ;
- audit de diagnostic : 1 092 assets, 3 violations ;
- validation CI : echec attendu et confirme sur les 3 violations ;
- aucun prefab, scene, maillage existant ou comportement reseau modifie.

## Decision requise avant la migration de contenu

Les trois assets hors budget restent references. Leur remplacement exige une
validation visuelle. Deux prototypes sont possibles :

1. utiliser le maillage source sans donnees barycentriques et conserver les
   contours issus des textures et normales ; gain disque maximal, risque visuel
   localise sur le givre des aretes ;
2. decimer puis diviser les sources en morceaux conformes ; fidelite potentielle
   superieure, mais davantage de renderers, de draw calls et de travail de
   validation.

La suppression des assets actuels ne doit intervenir qu'apres remplacement des
references, captures comparatives et verification du rollback.
