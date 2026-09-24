# HDRP Environment

Les profils HDRP sont portés directement par les composants `Volume` des scènes
Core et de leurs scènes additives. `ZoneManifest` ne modifie aucun profil
visuel au runtime.

## Configuration

1. Placer les `Volume` HDRP et leurs colliders dans la scène Core ou la scène
   additive concernée.
2. Configurer leur profil, priorité, poids et `blendDistance` directement dans
   l'Inspector Unity.
3. Utiliser le composant `Zone` pour les comportements de zone gameplay et
   audio, sans lui ajouter de gestionnaire de profils HDRP.
4. Ajouter `LocalVolumeAnchor` à la scène Core lorsqu'une caméra à la troisième
   personne doit évaluer les volumes depuis le personnage contrôlé.
5. Ajouter `LocalVolumeSunController` sur un Volume local lorsqu'une lumière
   directionnelle doit être active seulement dans ce volume.
