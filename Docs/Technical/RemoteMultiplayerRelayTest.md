# Test multijoueur distant Relay

## Préparation unique

1. Dans le tableau de bord Unity du projet `fcfcdf68-1558-4ee6-8db4-35ac395e466c`, activer Multiplayer Services / Relay pour l'environnement utilisé par le build.
2. Ouvrir le projet dans Unity afin que le Package Manager installe `com.unity.services.multiplayer@1.1.8` et mette à jour `Packages/packages-lock.json`.
3. Produire un même build pour l'hôte et les amis. Aucune redirection de port ni règle de routeur n'est nécessaire.

## Déroulement

1. L'hôte démarre par `Bootstrap`, ouvre **Multijoueur**, crée ou charge une partie multijoueur.
2. Le menu crée une allocation Relay, copie le code dans le presse-papiers et charge Maison. Le code reste affiché en haut de l'écran de l'hôte.
3. Chaque ami ouvre **Multijoueur > Rejoindre**, saisit le code Relay, puis attend l'écran de synchronisation. Il ne crée pas de sauvegarde locale.

## Contrôles de recette

- Les joueurs voient les autres personnages, mais chaque client ne contrôle que le sien.
- Porte, levier, flamme, coffre, loot et construction changent de façon identique chez tous les joueurs.
- Un ami qui rejoint après une modification de Maison reçoit le snapshot avant que ses entrées soient libérées.
- Lorsque l'hôte ferme la session, le code Relay cesse d'être valable.

## Limite voulue pour ce test

La sauvegarde de l'hôte est la seule persistance entre deux sessions. Les inventaires des amis sont répliqués pendant la session, mais ne sont pas sauvegardés individuellement.
