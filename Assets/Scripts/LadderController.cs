using UnityEngine;

public class LadderController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }



    public void UseLadder()
    {
        // Déplacer le personnage vers le ladder_base le plus proche avec un Lerp, respecter l'orientation du point avec un Lerp aussi.
        // Une fois arrivé ladder_base, déclencher Ladder_Start.
        // A la fin de Ladder_Start, déclencher Ladder_Loop.
        // Quand le personnage arrive au ladder_top, déclencher Ladder_End.
        // Pendant Ladder_End, déplacer avec un Lerp le personnage vers ladder_Exit, respecter l'orientation du point avec un Lerp aussi.
    }
}
