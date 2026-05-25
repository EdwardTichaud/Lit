# Scripts Gameplay

## StabReading

`StabReading` transforme un GameObject de scene classique en objet lisible.
Il n'utilise pas `Item` ni `InteractableItem`: le joueur le cible avec la meme
detection monde, puis l'action `Interact` ouvre `StabPanel` au lieu d'ajouter un
objet dans l'inventaire.

Setup minimum:

1. Ajouter `StabReading` sur le GameObject lisible.
2. Ajouter ou verifier un collider sur le GameObject ou un enfant.
3. Renseigner `Reading Text`.
4. Laisser `Stab Panel` vide si la scene contient un GameObject nomme
   `StabPanel`.

Par defaut, le script ecrit dans `StabPanel/Root/Frame_1/Text (TMP)`, donc dans
l'enfant de l'enfant de l'enfant de `StabPanel`. Si la hierarchie UI change,
assigner directement `Reading Text Target` dans l'inspecteur.

`Return` ferme le panel et rend le focus gameplay. `Hide Panel On Start` garde
le panel invisible au lancement tout en conservant sa hierarchie active via le
`CanvasGroup` quand il existe.

## DistrictRegistry

Les registres d'habitants par quartier reutilisent le systeme readable existant:
un registre jouable est un `Item` avec `readableKind = Book`, lu dans `BookPanel`
et feuilletable avec la navigation de livre deja presente dans
`InventoryPanelController`.

La source de donnees est separee de l'affichage:

- `DistrictRegistry` contient les `ResidentRecord`.
- `ResidentRecord` porte l'identite, l'annee de naissance, la chambre,
  les tags et les futurs liens (`lineageId`, `relatedRoomId`,
  `relatedObjectId`, `relatedReadableId`).
- `ResidentEvent` porte l'historique date d'un habitant: naissance,
  deplacement, rature, disparition, deces, correction de chambre ou note.
- L'`Item` readable contient les pages generees pour l'UI actuelle.
- `DistrictRegistryReadable` est le pont runtime entre les donnees, l'`Item`,
  `TemporalZone` et `AgeManager`/Braseros anciens.

Assets initiaux:

- Donnees: `Assets/ScriptableObjects/Narrative/DistrictRegistries/`
- Livres lisibles: `Assets/ScriptableObjects/Item/ReadableLore/DistrictRegistries/`

Workflow:

1. Modifier les habitants dans un asset `DistrictRegistry`.
2. Verifier que `Readable Item` pointe vers l'`Item_REG_*` correspondant.
3. Cliquer `Rebuild Readable Item Pages (Age666)` dans l'inspecteur, ou lancer
   `Lit/Narrative/Rebuild District Registry Readables`.
4. Sur un registre de scene, ajouter `DistrictRegistryReadable` et assigner le
   meme `DistrictRegistry` / `Item` si le registre doit reagir localement au
   temps.
5. Utiliser l'`Item` readable comme les autres livres du projet.

Regles temporelles:

- Les pages sont reconstruites pour l'annee consultee.
- Un habitant est visible seulement si `birthYear <= annee consultee`.
- Le dernier evenement affiche est le dernier `ResidentEvent` non-naissance dont
  `year <= annee consultee`.
- Les evenements futurs restent caches: pas de deces, de deplacement ou de
  rature avant leur annee.
- Les pages restent des `Item.ReadablePage`, donc le feuilletage de `BookPanel`
  ne change pas.

Priorite des sources temporelles:

1. Le registre utilise l'age dominant de `TemporalZone` quand elle est
   presente et preferee.
2. Sinon, il utilise l'age canonique d'`AgeManager`, donc les Braseros anciens.
3. En dernier recours, il utilise `fallbackAge` (`Age666` par defaut).

`InventoryPanelController` appelle
`DistrictRegistryReadable.RefreshReadableItemForCurrentTemporalContext` avant
d'ouvrir un livre et pendant la lecture. Les registres temporels peuvent donc
changer de contenu sans remplacer l'UI de livre existante.

Les ratures sont rendues textuellement avec `[Nom rayé]` pour rester compatibles
avec l'UI TMP actuelle, qui ne rend pas le Markdown de type `~~texte~~`.

TODO: permettre le filtrage des registres par lignée familiale.
TODO: relier les `ResidentRecord` aux chambres interactives visitables.
TODO: ajouter le support des objets transgénérationnels dans les registres.
