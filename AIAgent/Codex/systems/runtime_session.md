# Session runtime

## Rôle

Gérer les sessions et slots de sauvegarde, le passage menu/jeu et l’isolation
de l’état entre deux parties.

## Classes principales

Emplacement canonique des scripts de session de sauvegarde :
`Assets/Persistence/Save/Session/`.

- `SaveSessionManager` : sessions, slots, métadonnées, chemin du slot actif.
- `MainMenuController` : création/sélection d’une partie et chargement de scène.
- `GameplayRuntimeReset` : nettoyage centralisé des singletons et caches runtime.
- `LoadingScreenService` : transition de chargement de scène.

## Flux principaux

1. Le menu crée ou sélectionne un slot dans `SaveSessionManager`.
2. Le lancement appelle `GameplayRuntimeReset.PrepareForGameplayStart`.
3. La scène de jeu est chargée et les stores utilisent le chemin du slot actif.
4. Le retour au menu déclenche `ResetForMenuScene` et arrête le suivi du temps.

## Pièges observés

- Plusieurs managers sont `DontDestroyOnLoad`; le reset explicite est nécessaire.
- Un singleton peut partager son GameObject avec d’autres composants : ne pas
  détruire aveuglément l’objet complet.
- Les statiques doivent être réinitialisés même si Domain Reload est désactivé.
- Une scène lancée directement peut ne pas avoir de slot actif; les fallbacks
  globaux sont généralement désactivés pour éviter les fuites entre parties.
