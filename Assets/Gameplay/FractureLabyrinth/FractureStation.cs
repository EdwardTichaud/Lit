using UnityEngine;

namespace Lit.Fractures
{
    /// <summary>Uses the existing local interaction router/selection; never polls the physical Interact button.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FractureStation : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
    {
        public enum Kind { Entry, ArchiveDial, ArchiveValidate, Part, Repair, Threshold, RoutingDial, NetworkShortcut, Protection, Reinforcements, Exit, Bookshelf, GalleryDial, GalleryValidate }
        public FractureGraybox labyrinth;
        public Kind kind;
        public string label;
        public int dialIndex;
        public int dialValue;
        public FractureStation linkedDial;
        [Tooltip("-1 = aucun indice ; 0/1/2 = indice numéroté 1/2/3.")]
        public int clueIndex = -1;
        [System.NonSerialized] public bool searched;
        public float reach = 2.2f;
        private GameObject detected;
        public int LinkedDialValue => linkedDial != null ? linkedDial.dialValue : -1;
        public string SelectionSuffix => kind == Kind.GalleryDial && labyrinth != null ? " [" + Symbol(labyrinth.ArchivePuzzle.GetInput(dialIndex)) + "]" : kind == Kind.RoutingDial ? " [" + Symbol(dialValue) + "]" : string.Empty;
        /// <summary>Explicit solutions for iteration in the isolated prototype, not campaign hints.</summary>
        public string GetPrototypeHelp()
        {
            if (labyrinth == null) return string.Empty;
            if (labyrinth.districtIntegration && kind == Kind.Entry)
                return Help("Utilisez Interagir pour ouvrir la porte de départ et explorer les Archives.", "Les bibliothèques cachent les trois indices nécessaires à la porte des Galeries.", "Cette zone fait partie du quartier. Votre progression est conservée ; le chemin d'entrée permet aussi de revenir au quartier.");
            if (labyrinth.districtIntegration && kind == Kind.Exit)
                return Help("Utilisez Interagir pour valider la résolution, puis revenez par le passage d'entrée.", "Les deux énigmes sont terminées. Vous gardez les indices et l'ouverture de la porte.", "Aucun changement de zone ni téléportation. Aucun boss ou butin n'est encore ajouté à cette séquence.");
            if (kind == Kind.Entry)
                return Help("Utilisez Interagir sur ce pupitre : cela démarre le parcours et ouvre la porte de départ devant vous.", "Derrière cette porte : fouiller les bibliothèques des Archives, puis ouvrir la porte des Galeries avec les indices trouvés.", "La porte de départ reste ouverte pour revenir ici et recommencer. Les indices déjà trouvés restent notés pendant cette session de test.");
            if (kind == Kind.Bookshelf)
                return Help(searched ? "Cette bibliothèque a déjà été fouillée." : "Approchez cette bibliothèque et utilisez Interagir pour la fouiller.", "Trois bibliothèques contiennent chacune un symbole avec un numéro. Le numéro indique sa place dans la combinaison de la porte des Galeries.",
                    (searched ? clueIndex >= 0 ? "Trouvé ici : " + (clueIndex + 1) + " = " + Symbol(FractureArchivePuzzle.SymbolForNumber(clueIndex)) + ".\n" : "Aucun indice ici.\n" : "Toutes les bibliothèques ne contiennent pas d'indice.\n") + "Indices trouvés : " + labyrinth.ArchivePuzzle.FoundCount + "/3. Ils restent affichés dans le carnet en haut de l'écran.");
            if (kind == Kind.GalleryDial || kind == Kind.GalleryValidate)
                return Help(labyrinth.ArchivePuzzle.DoorOpen ? "La porte est ouverte. Traversez-la et rejoignez le poste de fin." :
                    kind == Kind.GalleryDial ? "Interagir change le symbole de la case " + (dialIndex + 1) + " : O → Y → — → O. Placez ici le symbole portant le numéro " + (dialIndex + 1) + " trouvé aux Archives." :
                    "Réglez les cases 1, 2 et 3 avec les symboles trouvés aux Archives, dans l'ordre de leurs numéros. Puis utilisez Interagir ici pour essayer la combinaison.",
                    "La bonne combinaison ouvre cette porte des Galeries. L'ordre de découverte des indices ne compte pas : seul leur numéro indique leur place.",
                    "Indices : " + labyrinth.ClueNotebook + "\nCases actuelles : " + labyrinth.GalleryCode + ".\n" + (labyrinth.ArchivePuzzle.FoundCount < 3 ? "Il reste des indices à chercher aux Archives." : "Les trois indices sont connus. Une erreur n'efface rien : ajustez les cases puis réessayez."));
            if (kind == Kind.Exit)
                return Help("Après avoir ouvert la porte, utilisez Interagir ici pour terminer le test.", "Ce poste confirme que vous avez terminé les deux énigmes.", "Pas de boss ni de récompense pour l'instant. Revenez à l'entrée pour réessayer.");
            return string.Empty; // Old station kinds are inactive in this prototype.
        }
        private static string Symbol(int value) => value >= 0 && value < 3 ? new[] { "O", "Y", "—" }[value] : "non relié";
        private static string Help(string action, string purpose, string next) => "À FAIRE MAINTENANT\n" + action + "\n\nÀ QUOI ÇA SERT\n" + purpose + "\n\nÀ SAVOIR / ENSUITE\n" + next;
        public bool IsInReach(SquadCharacterController actor) => actor != null &&
            Mathf.Abs(actor.transform.position.y - transform.position.y) < 2f &&
            Vector3.Distance(GetInteractionDetectionCollider().ClosestPoint(actor.GetInteractionOriginWorldPosition()), actor.GetInteractionOriginWorldPosition()) <= reach;
        public bool CanBeDetectedBy(SquadCharacterController actor) => isActiveAndEnabled && labyrinth != null &&
            labyrinth.isActiveAndEnabled && (labyrinth.districtIntegration || actor == labyrinth.Player) && actor.CurrentHp > 0 && IsInReach(actor);
        public Collider GetInteractionDetectionCollider() => GetComponent<Collider>();
        public Transform GetInteractionAnchor() => transform;
        public float GetInteractionMaxDistance(SquadCharacterController actor) => reach;
        public int GetInteractionPriority(SquadCharacterController actor) => 80;
        public void SetDetectedCharacter(GameObject character) => detected = character;
        public bool TryHandleLocalInteract()
        {
            if (detected == null || !RuntimeOutlineSelectionManager.IsActiveInteractable(this)) return false;
            var actor = detected.GetComponentInParent<SquadCharacterController>();
            return CanBeDetectedBy(actor) && labyrinth.TryInteract(this, actor);
        }
        private void OnDisable()
        {
            detected = null;
            if (RuntimeOutlineSelectionManager.IsActiveInteractable(this)) RuntimeOutlineSelectionManager.Clear();
        }
    }
}
