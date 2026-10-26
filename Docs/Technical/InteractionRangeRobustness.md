# Portée des interactions — correction du 26 octobre 2026

La sélection des objets et leur utilisation doivent respecter la proximité
réelle du personnage. Un volume de détection étendu ne donne pas une portée
étendue à l'objet.

- Les portails utilisent `interactionAnchor` (leur Transform par défaut) comme
  point d'utilisation. La distance configurée est vérifiée à la sélection, à
  l'activation locale et dans `ServerTryUse`. La marge réseau existante de
  0,35 m est conservée côté hôte.
- Les autres triggers utilisent leur ancre pour mesurer la portée. Les colliders
  physiques primitifs utilisent leur surface. Pour un MeshCollider non convexe,
  une véritable intersection de surface remplace la proximité à sa boîte
  englobante, qui pouvait contenir un grand espace vide.
- Les colliders désactivés ou inactifs sont rejetés. La cible est rafraîchie au
  début de l'appui Interagir, avant les gestionnaires prioritaires, lorsqu'aucune
  interface ne détient le focus.
- Le contact volumétrique des influences lumineuses reste distinct de la portée
  d'utilisation : `GetInteractionPoint` est conservé pour l'influence ;
  `GetInteractionRangePoint` sert à la sélection et à la validation de portée.

Aucune scène, position, destination de portail ou distance configurée n'est
modifiée par ce correctif.

## Vérification

Les tests Unity EditMode ciblent les grands triggers et colliders physiques,
les ancres explicites, la sélection en cache après éloignement, le refus côté
hôte, les objets désactivés, les espaces vides de meshes concaves, les murs,
l'ordre des cibles proches, la saturation du tampon de détection et les
interactions avec les Flames. Les deux géométries de prefab Portal_1/Portal_2
sont instanciées avec une destination locale de test ; le voyage entre zones
et une session réseau complète ne sont pas exécutés par ces tests.

Rapports : `Library/InteractionRangeValidation`.
Le test existant `ItemCanUseLitBrazierBeforeReceivingPhysicsNotification`
échoue également isolément avec le calcul de portée original de HEAD.
Ce résultat préexistant est conservé dans `BaselineInfluence.xml` et n'est
pas compté comme une réussite du correctif. Deux tests de décoration Maëlle
exigent des scènes absentes de la copie de validation et ne sont pas inclus
dans la dernière sélection ciblée.

Dernière passe : 29/29 tests ciblés réussis (AcceptedResults.xml). Les quatre fichiers runtime et le fichier de tests correspondent exactement aux versions du projet source. Compilation Unity réussie dans la copie isolée.

