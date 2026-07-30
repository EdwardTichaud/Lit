# Streaming de scenes

Les zones utilisent quatre categories de scenes :

- `Critical` : spawn, collisions immediates, portails et elements indispensables.
- `Loading` : contenu necessaire avant de rendre le controle au joueur.
- `PostLoading` : contenu commun charge progressivement apres le fondu.
- `Proximity` : decor local charge par chaque client a l'approche de son personnage.

Une cellule `Proximity` ne contient que des meshes decoratifs, lumieres decoratives, VFX et ambiance. Elle ne doit contenir ni `NetworkObject`, ni PNJ/ennemi reseau, ni portail, ni interaction synchronisee, ni collision necessaire au gameplay.

## Evolution reseau prevue

Les objets importants synchronises seront isoles dans une ou plusieurs scenes reseau dediees par zone : ennemis, PNJ, interactions persistantes, portails, etats de progression et autres `NetworkObject`. Le serveur chargera ces scenes communes pour toute la session. Les clients pourront alors garder des cellules decoratives de proximite autonomes, tout en partageant les memes objets et interfaces reseau coherents.
