# Cameras UCC et Cinemachine

## Regle fondamentale

La camera physique taggee `MainCamera` a un seul pilote a la fois :

- **gameplay et lock** : `CameraController` UCC ;
- **plan de scene, cutscene ou angle specifique** : `CinemachineBrain`.

`LitCameraDirector` est l'unique composant autorise a basculer entre ces deux
modes. Il desactive UCC et ses entrees avant d'activer le Brain, puis rebind UCC
au personnage local et le repositionne a la fin du plan.

## Ajouter un plan dans une scene

1. Creer un GameObject Cinemachine Camera dans la scene.
2. Regler ses composants Cinemachine (suivi, composition, bruit, collision) et
   ses cibles de suivi/regard.
3. Ajouter `LitCinemachineShot` sur ce meme GameObject.
4. Appeler `Activate()` depuis un trigger, un evenement de gameplay ou un
   signal Timeline.
5. Appeler `Release()` lorsque le plan est termine. Si le GameObject est
   desactive, le retour UCC est automatique par defaut.

Le `LitCameraDirector` est cree sur la `MainCamera` lors du premier appel. Il
ajoute un `CinemachineBrain` a cette camera seulement lorsqu'un plan
Cinemachine est demande. Le Brain est desactive le reste du temps : cela evite
tout conflit avec UCC.

## Binding d'un acteur Timeline

Pour authorer une Timeline dans une scene additive, un prefab personnage peut
rester assigne aux pistes Animation en edition. Ajouter
`LitTimelineLocalPlayerBinder` au GameObject qui porte le `PlayableDirector`,
puis assigner ce prefab dans **Editor Preview Actor**. Au runtime, toutes les
pistes actuellement liees a cet acteur (ou a ses enfants) sont automatiquement
rebindees vers les composants equivalents du personnage local cree par
Bootstrap. Une piste Animation liee a un `Animator` est ainsi jouee par
l'Animator du vrai joueur, sans remplacer ni desactiver son GameObject.

Si une Timeline est demarree par un signal ou un script avant que le joueur
local soit pret, appeler `Bind Now()` juste avant `PlayableDirector.Play()`.

Creer un parent de previsualisation dans la scene, placer sous lui le ou les
prefabs de reference, puis ajouter `LitTimelinePreviewActor` au parent. Il
reste disponible pour authorer la Timeline en edition puis desactive
automatiquement tous ses enfants au lancement du jeu.

## Usage par contexte

| Contexte | Systeme |
| --- | --- |
| Exploration, zoom, rotation libre | UCC Adventure |
| Lock combat temps reel | Vue UCC CombatLock |
| Combat tour par tour actuel | A migrer progressivement vers un LitCinemachineShot |
| Dialogue, scene narrative, plan fixe | Cinemachine via LitCinemachineShot |
| Changement de personnage | Binder UCC, apres le retour du directeur |

## Interdictions

- Ne pas laisser un `CinemachineBrain` actif pendant que UCC est actif.
- Ne pas modifier directement le `Transform` de la MainCamera depuis un script
  de gameplay.
- Ne pas desactiver manuellement UCC pour un plan : appeler le directeur ou le
  composant de plan.
- Ne pas mettre plusieurs Brains sur la MainCamera.

## Migration

Les anciens scripts qui desactivent eux-memes UCC (presentation de combat et
sequences narratives) doivent etre convertis plan par plan : creer une camera
Cinemachine correspondante, puis demander le plan au directeur. Pendant cette
transition, ne pas melanger un ancien pilote direct et Cinemachine dans la meme
sequence.
