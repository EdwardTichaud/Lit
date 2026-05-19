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
