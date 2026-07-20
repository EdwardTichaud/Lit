# Garde-fou minimal — meshes de glace

Ce garde-fou conserve les acquis des prototypes 2A/2B sans en faire un chantier
actif de performance globale. Il ne remplace jamais automatiquement un asset.

- Seuils versionnes : 150 000 sommets et 25 Mio serialises par mesh genere ;
  quatre influences locales maximum pour les flammes.
- Controle : `Lit/Performance/Audit Ice Assets (Report Only)` produit un rapport
  JSON ; `Lit/Performance/Validate Ice Assets (Blocking)` echoue si un seuil
  est depasse. En CI, utiliser `LitIcePerformanceAudit.ValidateForCi` et
  `-litIceAuditOutput <chemin>`.
- Declenchement : relancer l'audit apres toute modification d'un mesh genere,
  d'un generateur barycentrique ou d'un prefab de glace.
- Exception : aucune derogation silencieuse. Une exception doit etre documentee
  dans la pull request avec le chemin d'asset, le seuil depasse, la justification,
  le proprietaire, la date de revue et une mesure Player. Elle exige une mise a
  jour explicite de la politique versionnee avant de pouvoir rendre la CI verte.

Les assets de production restent inchanges par ce garde-fou.
