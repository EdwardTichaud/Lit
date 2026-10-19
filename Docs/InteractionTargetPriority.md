# Sélection d'interaction : distance avant catégorie

Le détecteur du personnage local conserve ses règles d'éligibilité : portée, disponibilité, influence de Flame selon le type, orientation et ligne de vue depuis le personnage. La caméra ne détermine pas la portée.

Parmi les candidats valides, la distance au personnage est désormais prioritaire pour tous les types, y compris les portails et fantômes. La priorité authorée sert uniquement à départager des distances identiques ; l'identifiant stable de l'objet départage ensuite les égalités. Le résultat ne dépend plus de l'ordre des colliders renvoyés par Unity. Aucune mémoire de cible ne permet à une ancienne cible plus lointaine de battre une cible plus proche.

Les colliders physiques gardent leur point de proximité existant. Pour un trigger d'accès, le classement utilise l'ancrage de l'interaction, ou le centre du volume sans ancrage : être dans un grand trigger ne réduit plus artificiellement sa distance à zéro. Le volume et les conditions de portée existants restent utilisés pour l'éligibilité.

La recherche commence avec 128 colliders, mais agrandit son tampon si celui-ci est plein, puis le réutilise aux frames suivantes. Une salle dense ne tronque plus silencieusement les candidats avant leur classement.

Le détecteur reste propriétaire de la sélection et de l'action même si l'objet n'a aucun renderer d'outline disponible. Les anciens callbacks sans propriétaire ne peuvent pas remplacer cette sélection. Le changement de cible continue à désélectionner l'ancien objet avant d'activer le nouveau ; le bouton Interagir utilise la même sélection centralisée.

Vérification : compilation runtime et éditeur réussie ; 9 tests ciblés de tri, permutations, distance de trigger et recherche saturée passent dans un projet Unity isolé. Les tests d'intégration avec deux portails de priorités différentes et de conservation d'autorité sans outline sont ajoutés à `CharacterInteractionDetectionTests` et compilés, mais n'ont pas été exécutés dans la scène gameplay.

À vérifier en Play : portail/objet/Flame/fantôme côte à côte, passage de l'un à l'autre, limite de portée, mur entre personnage et cible, menus et combat. Le plus proche doit gagner parmi les cibles éligibles, et l'objet surligné doit correspondre à celui actionné.
