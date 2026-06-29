using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Definit les differentes transitions possibles lors de l'activation d'une camera Cinemachine.
/// </summary>
public enum FadeType
{
    /// <summary>
    /// Utilise un fondu visuel via <see cref="CinemachineBlendSwitcher"/> pour passer d'une camera a l'autre.
    /// </summary>
    Fade,

    /// <summary>
    /// Laisse Cinemachine effectuer un mouvement smooth classique entre les plans.
    /// </summary>
    Smooth
}

/// <summary>
/// Composant leger place sur chaque <see cref="CinemachineCamera"/> afin d'indiquer
/// au <see cref="CinemachineBlendSwitcher"/> le type de transition a utiliser.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CinemachineFadeConfiguration : MonoBehaviour
{
    [Tooltip("Style de transition souhaite lorsque cette camera devient active.")]
    public FadeType fadeType = FadeType.Smooth;
}


