# Capacités d'exploration — AbilitySO

## Règles

Les six statistiques JDR (Force, Dextérité, Constitution, Intelligence, Sagesse,
Charisme), `CharacterStats`, `StatsSO` et les jets d'exploration sont supprimés.
Les PV, SkillSO, BasicSkillsSO, compétences chargées, équipements et réglages de
combat sont conservés. Le crochetage conserve son outil consommé et sa difficulté
existante, mais sans bonus de Dextérité : seul son tirage propre reste utilisé.

Une seule capacité peut être équipée à la fois : un emplacement pour le joueur
solo, un emplacement par identité humaine stable en multijoueur. Changer de
personnage ou laisser des compagnons dans l'escouade ne donne aucun emplacement
supplémentaire. Deux joueurs peuvent choisir la même capacité.

Les déblocages sont partagés par la partie/sauvegarde de l'hôte. Ils ne sont pas
une progression globale du compte ni une récompense exportée chez un autre hôte.
L'équipement est personnel. Les identités déconnectées restent sauvegardées.

- **Observateur** : disponible dès le départ ; détecte sans jet les passages
  dissimulés à proximité. Le passage découvert reste révélé et persistant.
- **Sauf-Conduit** : débloqué à la complétion réelle de `district1.belmont` ; permet
  au joueur qui l'équipe d'emprunter un passage découvert. Ne téléporte pas les
  autres joueurs et ne leur prête pas sa capacité.

La restriction à une capacité est volontaire : un joueur solo peut découvrir un
passage avec Observateur puis changer à la Maison. Ne pas placer la progression
principale derrière une combinaison obligatoire de capacités sans alternative.

## Équipement à la Maison

Le service de session crée un panneau UGUI local, sans reconstruire les scènes.
À la Maison, utiliser **Capacités [F6]** en bas à droite ou F6. Le panneau liste
les capacités, leurs descriptions, leur verrouillage et le choix courant.
Fermer avec le bouton, Échap ou Retour manette lorsque le panneau est ouvert.
Hors Maison ou pendant une transition, le panneau se ferme et les requêtes de
changement sont refusées côté hôte. Un personnage mort, en combat ou sous verrou
de mise en scène ne peut pas changer de capacité.

## Création / déblocages

Créer un asset via `Lit/Abilities/Ability` et le stocker sous
`Assets/Resources/Abilities`. Son `abilityId` doit être unique et stable.
L'asset décrit une capacité ; il ne stocke aucun état de joueur.

- `availableAtStart` : déblocage initial.
- `unlockCycle` + `unlockStepId` optionnel : fin réelle d'un cycle ou d'une étape.
- `unlockKnowledge` : connaissance réelle, combinée au cycle si les deux existent.
- Sans condition : utiliser `AbilityUnlock.Unlock()` depuis un événement de
  gameplay exécuté par l'hôte, ou `AbilityService.Unlock(ability)`.

Une nouvelle capacité doit aussi être consommée par une interaction ou un
comportement via `AbilityService.HasEquipped(character, ability)`. Créer un asset
seul n'ajoute pas automatiquement de mécanique.

Les déblocages ne remplacent jamais automatiquement la capacité équipée. Les
états virtuels Dev de CycleController ne satisfont pas les conditions persistantes.

## Persistance et réseau

CharacterSaveData v10 contient `abilities` ; WorldSnapshot binaire v2 contient le
même état pour restauration et arrivée tardive. Le lecteur accepte toujours les
snapshots v1. Les anciennes listes `skillIds` ne sont lues que pour migrer les
identifiants d'une sauvegarde v9 ; elles ne deviennent pas plusieurs capacités
équipées et ne contournent pas les nouvelles conditions de déblocage. Une ancienne
liste contenant Sauf-Conduit ne le débloque donc pas avant Belmont. Aucun asset
CharacterData n'est modifié au runtime.

AbilityService utilise des messages fiables : l'hôte décide des déblocages et
valide l'équipement demandé selon l'identité réelle de l'émetteur. Les clients ne
disposent d'aucune commande de déblocage. Le contrôle des passages vérifie côté
hôte la capacité équipée, la proximité, l'état du personnage et la découverte.

Les GUID des deux anciens assets et des composants de présentation ont été
conservés. Les champs de TrouEtroit migrent avec FormerlySerializedAs.

## Vérifications

`Lit > Abilities > Validate Ability System` : contrôles non destructifs des
emplacements, IDs, assets, conservation du combat et sérialisation JSON/binaire.
Tests NUnit supplémentaires dans AbilitySystemTests.

Le 19 octobre 2026 : compilation du code de jeu et des outils éditeur réussie,
15 contrôles exécutés dans Unity réussis, 3 méthodes de test AbilitySystemTests
exécutées avec succès. Conversion native des deux assets finalisée avec leurs
GUID d'origine. Aucun placement de scène modifié. Le script temporaire utilisé
pour la conversion et l'exécution des contrôles a été retiré.

À vérifier en jeu : nouvelle partie, fin Belmont, sélection Maison, refus dans
District_1, changement de personnage, découverte puis traversée, sauvegarde et
rechargement, deux clients équipés différemment, arrivée tardive et reconnexion.

## Passages de maintenance intégrés

Deux raccourcis utilisent ce contrat : couloir ouest de District_1 et maçonnerie
centrale de la Nef des Tours Interrompus. Voir `Cycle_Clairmont.md`.
Un seul TrouEtroit pilote deux extrémités, avec un Rigidbody cinématique et
`additionalTriggers` pour la seconde. Découverte commune persistante, permission
de traversée propre à chaque joueur ; les arrivées sont alignées sur le NavMesh.
