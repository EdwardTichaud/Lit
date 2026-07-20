# Diagnostic performance global — candidate 2026-07-18

## Contrat applique

Reference provisoire : Windows 10, i7-8700, RTX 2070 8 Go, 16 Go RAM,
DirectX 12, 1920x1080, profil `Balanced`, VSync desactivee. La cible est
60 FPS sans hitch perceptible : frame p95 <= 16,67 ms, p99 <= 25 ms et aucune
frame > 50 ms hors chargement explicite. Le contrat complet est dans
`performance_contract.md`.

## Passe Player candidate

`Runs/idle-clean-01.json` est une passe Development Player autonome de 60 s,
apres 10 s de prechauffage, dans `Maison`. Elle n'est pas encore une baseline
officielle : il manque deux repetitions et un trajet camera scripté.

| Mesure | Resultat | Budget | Etat |
| --- | ---: | ---: | --- |
| Frame p95 | 25,32 ms | 16,67 ms | echec |
| Frame p99 | 34,71 ms | 25 ms | echec |
| GPU p95 | 6,97 ms | 14 ms | marge |
| CPU/Main Thread p95 | 25,31 / 25,29 ms | 13,5 ms | echec |
| Allocations GC moyennes | 265 522 o/frame | 0 o | echec |
| Frames > 25 ms / > 50 ms | 204 / 15 | 0 | echec |
| SetPass p50 / triangles p50 | 172 / 2,24 M | diagnostic | a attribuer |

La limite est donc CPU, pas GPU. Aucune refonte de rendu, de streaming, de
lumiere ou de visibilite n'est autorisee sur la base de cette seule passe.

## Causes classees par certitude et impact

1. **Chemin CPU/Main Thread** — confirme, priorite maximale. Son p95 est
   25,29 ms alors que le GPU p95 est 6,97 ms. L'attribution par sous-systeme
   manque encore.
2. **Allocations GC recurrentes** — confirme, priorite maximale. La mediane
   est 230 428 o/frame et la moyenne 265 522 o/frame. Une lecture de 1 000
   frames de capture brute totalise 262 129 852 octets, mais sans pile C# :
   cette capture n'avait pas le deep profiling.
3. **Hitches CPU** — confirme, priorite elevee. 15 frames depassent 50 ms et
   le pire echantillon est 60,39 ms. La source precise reste inconnue.
4. **Pression de soumission de rendu** — observee, non prouvee comme cause.
   Les 172 SetPass et 2,24 M triangles sont stables ; le GPU garde de la marge.
   Ne rien modifier avant une capture qui relie ces compteurs aux frames lentes.
5. **Empreinte memoire graphique rapportee** — a verifier. Le compteur Unity
   indique ~11,7 Go alors que la carte declare 8 Go ; il ne doit pas etre traite
   comme une VRAM physique avant validation avec un outil pilote. C'est un
   risque de stabilite, pas une cause etablie des hitches.

Les logs `FacialExpressionController` observes pendant le chargement sont un
indice a investiguer, pas une cause retenue : la fenetre de mesure les exclut.

## Prochaine micro-etape autorisee

Construire une variante Development avec deep profiling, jouer **un seul**
scenario court et scripté, puis utiliser `PerformanceGcAllocationReport` pour
obtenir les piles de `GC.Alloc`. La premiere correction devra viser une source
classement 1 ou 2 et sera conservee seulement si la nouvelle passe Player
ameliore les percentiles sans regression visuelle ou reseau.

## Perimetre non modifie

Pas de reference de production, de scene, de prefab, de systeme reseau, de
sauvegarde, de lumiere, de portail, de streaming ou de visibilite n'a ete
modifie.
