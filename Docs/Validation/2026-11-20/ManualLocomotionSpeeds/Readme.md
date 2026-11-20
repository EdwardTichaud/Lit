# Lucian - vitesses manuelles

- Calcul de cadence et choix automatique de l'allure par vitesses natives retires du profil.
- LitSpeed et le tier de locomotion transmettent directement la vitesse planaire physique en m/s.
- Aucun ajustement de la vitesse de deplacement UCC ni des vitesses des etats Animator.
- Parametre LocomotionPlaybackRate et ses liens retires des deux controllers de Lucian par les API Unity.
- Comparaison exacte des controllers avant/apres : seules les deux proprietes de liaison et le parametre retire ont change. Les modifications presentes avant l'intervention sont conservees.
- L'installateur ne recree plus le multiplicateur. Validation avant build et test Editor adaptes.

Validation le 20 novembre 2026 : compilation runtime/Editor sans erreur, contrat Unity PASS, 56 tests Editor passes, zero echec. Pas de validation visuelle de correspondance pas/distance : ce reglage est laisse manuellement a l'auteur.

Le temps global de combat/cinematique et les autorites physiques ne font pas partie de ce retrait. Les autres controllers ennemis ne sont pas reconstruits.
