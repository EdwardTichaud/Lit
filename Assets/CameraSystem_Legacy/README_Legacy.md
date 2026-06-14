# Camera System Legacy

Ce dossier archive l'ancien systeme camera custom du projet.

Il ne doit plus etre utilise ni attache a des GameObjects actifs. La camera de gameplay est desormais geree par Opsive UCC, via la camera UCC native et le ViewType Third Person/Adventure.

Les assets et scripts sont conserves pour historique et pour preserver les GUID Unity, mais UCC est l'unique source de verite pour la camera active du gameplay.

## Features legacy abandonnees

- Camera CRPG custom avec pivots yaw/pitch.
- Pan clavier/souris, edge scrolling et mode free camera.
- Zoom custom et collision camera custom.
- Triggers de cameras fixes.
- Overrides temporaires de suivi pendant le placement d'objets/buildings.
- Effets camera de respiration, course et chute.
- XRay camera/render texture/mask follower.
- Reglages custom d'offset shoulder hors configuration UCC native.

Si une feature doit revenir, elle doit etre reconstruite comme une integration explicite UCC et non comme un second framework camera parallele.
