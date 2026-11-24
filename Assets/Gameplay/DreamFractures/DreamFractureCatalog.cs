using System;
using UnityEngine;

namespace Lit.DreamFractures
{
    [CreateAssetMenu(menuName = "Lit/Fractures oniriques/Catalogue")]
    public sealed class DreamFractureCatalog : ScriptableObject
    {
        public DreamFractureDefinitionSO[] fractures = Array.Empty<DreamFractureDefinitionSO>();
        public string returnSceneName = "Maison_Core";
        public Vector3[] returnPositions = Array.Empty<Vector3>();
        public DreamFractureDefinitionSO Find(string id) =>
            Array.Find(fractures, f => f != null && f.id == id);
    }
}
