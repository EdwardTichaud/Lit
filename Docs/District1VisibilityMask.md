# Masque circulaire — toutes les scènes District_1

Le masque tactique de Maison s'étend automatiquement aux scènes de `Assets/Scenes/District_1/`, aux scènes `District_1_*` des cycles et à la scène racine `District_1_Indoor_Lights`. Aucun composant à placer manuellement et aucune obligation de charger `District_1_Core` : l'installateur s'abonne aux chargements de scène et crée un unique `District_1_TacticalVisibility` local, persistant entre les chargements additifs. Il réutilise aussi une instance existante après un rechargement du domaine en Play.

La préparation est différée d'une frame pour inclure les décors construits dans `Start`. Elle conserve tous les LOD, y compris les enfants initialement inactifs. Les scènes d'environnement incluent les meshes sans nom architectural (par exemple ceux d'OldBarn). Les murs, piliers, plafonds, conduits et garde-corps sont reconnus en français et en anglais dans toute la hiérarchie ; les sols, marches, terrain, personnages animés, flammes, armes, traces narratives et VFX restent exclus. Les scènes de cycles restent couvertes sans transformer leurs personnages ou souvenirs en décor découpable.

## Matériaux

- HDRP/Lit : variante native déjà utilisée par Maison.
- Master et Ice : fonction de masque déjà intégrée.
- Autodesk Interactive : variante du graph HDRP d'origine, avec les mêmes propriétés, textures et calculs de surface, plus la fonction de masque partagée. Le graph et son matériau de référence sont inclus dans le projet ; aucune modification du package HDRP.

Les adaptations utilisent des copies locales. Les assets partagés, les colliders et les propriétés des autres effets ne sont pas modifiés. Les références de matériaux et blocs de propriétés sont restaurées au déchargement ou à la désactivation ; les copies restantes sont libérées quand le district n'a plus de renderers préparés.

## Comportement

Identique à Maison : suivi tactique en `IgnoreObstacles`, ouverture pour le joueur et la cible verrouillée, rayon et fondu du profil existant, sans rapprochement de caméra. Le clipping adaptatif reste désactivé. L3, Timeline et third-person suspendent le masque. L'inspecteur de la MainCamera affiche `Zone visibility mask` ; l'objet `District_1_TacticalVisibility` affiche les compteurs de préparation/incompatibilité. Maison conserve son périmètre et son comportement antérieurs ; District_10 et les autres zones ne sont pas inscrits automatiquement.

## Validation

`LitDistrictVisibilityTests` couvre les 25 chemins de scènes inventoriés, les exclusions de zones, la classification des meshes et de leurs parents, la conservation des textures Autodesk et la compilation de ses passes, les ressources de shader réellement livrées et une scène temporaire de préparation/restauration (matériaux, sols, physique). `LitMaisonVisibilityTests` reste une régression du masque existant.

Validation dans un projet Unity 6000.6.4/HDRP 17.6 isolé : 59 tests EditMode et un contrôle supplémentaire en Play du véritable installateur sur une scène District_1 autonome (sans Core), avec désactivation/réactivation, réutilisation d'instance et déchargement. Les 25 scènes du projet ont été comparées à la matrice de tests : aucun chemin manquant. Compilation runtime/éditeur du projet principal vérifiée séparément.

Les essais de ressenti et de rendu du niveau réel restent à effectuer en Play : couloirs, arène, crypte, OldBarn, conduits, puits et nef, puis changements de cycle, chargements/déchargements additifs et Timeline. Les tests isolés ne sont pas une validation visuelle de ces niveaux sous HDRP/TAA.
