using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Authoring references only. Preview is implemented exclusively by the Editor.</summary>
public sealed class CombatAnimationLab : MonoBehaviour
{
    public Animator player;
    public Animator enemy;
    public Transform playerAnchor;
    public Transform enemyAnchor;
    public PlayableDirector director;
    public CinemachineBrain cameraBrain;
    public CinemachineCamera previewCamera;
    public SignalReceiver previewSignals;
    public TimelineAsset timeline;
    public AnimationClip playerClip;
    public AnimationClip enemyClip;
}
