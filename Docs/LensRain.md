# Pluie sur l'objectif

Dans `Reve_Limbes_Core`, sélectionner **Limbes_PluieObjectif** :

- Cocher ou décocher le composant **Volume** pour activer ou désactiver l'effet.
- Garder **Weight** à 1 quand l'effet est actif. Un poids intermédiaire mélangerait aussi les réglages de pluie du pack avec leurs valeurs par défaut.
- Pour doser l'effet, changer **Droplet Amount** dans le profil. **Droplet Refraction** règle la déformation et **Trail Amount / Speed** le ruissellement. Pour commander l'effet depuis une Timeline, activer ou désactiver l'objet du Volume.

Pour une autre ambiance, ajouter un Volume global à la scène concernée et lui assigner `Volume_Limbes_Objectif`, ou dupliquer le profil pour des réglages propres à cette scène. Le Volume suit le chargement de sa scène. Pour éviter qu'une modification touche plusieurs scènes, modifier une copie du profil.

Real Rain est enregistré dans HDRP à **Before Post Process**. Le profil garde l'intensité du pack positive parce que son shader la multiplie aussi par la quantité de gouttes, mais coupe séparément les traînées de pluie dans le monde, le brouillard, le mouillage, les éclaboussures et la baisse de luminosité. Il n'ajoute pas le rig météo, l'audio ou le tonnerre du pack. La pluie du décor et le profil lumière restent indépendants.

L'effet suit les Volumes de la caméra affichée, aussi quand Cinemachine pilote la MainCamera. Cette configuration ne détecte pas les toits : désactiver ou atténuer le Volume dans les ambiances abritées selon le besoin.

Les gouttes, leurs reflets et la buée s'atténuent avec la luminosité de l'image. Une image entièrement noire ne reçoit aucune lueur des gouttes.
