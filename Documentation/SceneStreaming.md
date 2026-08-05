# Streaming de scenes

Les zones utilisent quatre categories de scenes :

- `Critical` : spawn, collisions immediates, portails et elements indispensables.
- `Loading` : contenu necessaire avant de rendre le controle au joueur.
- `PostLoading` : contenu commun charge progressivement apres le fondu.
- `Proximity` : decor local charge par chaque client a l'approche de son personnage.

Une cellule `Proximity` ne contient que des meshes decoratifs, lumieres decoratives, VFX et ambiance. Elle ne doit contenir ni `NetworkObject`, ni PNJ/ennemi reseau, ni portail, ni interaction synchronisee, ni collision necessaire au gameplay.

## Evolution reseau prevue

Les objets importants synchronises seront isoles dans une ou plusieurs scenes reseau dediees par zone : ennemis, PNJ, interactions persistantes, portails, etats de progression et autres `NetworkObject`. Le serveur chargera ces scenes communes pour toute la session. Les clients pourront alors garder des cellules decoratives de proximite autonomes, tout en partageant les memes objets et interfaces reseau coherents.

## Convention District_1

District_1 suit la convention `District_1_<Phase>_<Secteur>_<Role>`. Un objet ne doit appartenir qu'a un seul secteur et a un seul role :

- `Environment` : structure, surfaces, collisions et NavMesh du secteur ;
- `Lighting_Core` : lumiere minimale qui rend le secteur jouable ;
- `Decor` et `Lighting_Decorative` : contenu visuel local et non critique ;
- `Network` : objets synchronises communs a la session.

Les scenes de l'arrivee (corridor puis premiere salle) sont les seules scenes `Loading` obligatoires avant le retour du controle. Crypte, arene et balcon arrivent ensuite une scene a la fois. Les scenes `Proximity` restent des cellules locales decoratives et ne font jamais partie du `ZoneManifest`.

L'outil **Lit > Scenes > District 1 > Architecture et migration** cree la structure, offre une migration sure de Corridor/Arena/Balcony et bloque les migrations incompatibles vers une cellule de proximite. L'audit **Lit > Performance > Auditer l'architecture District_1** applique les plafonds de decoupage : Critical (100 renderers / 20 lumieres), Loading (250 / 50 / 200 comportements) et PostLoading (300 / 60). Un pic d'activation mesure au-dessus de 50 ms doit entrainer un decoupage supplementaire, meme si ces plafonds sont respectes.
