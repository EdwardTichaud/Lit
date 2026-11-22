# Dialogue placement and sequence gaze

Implemented for the locally controlled Lucian. No existing scene marker or prefab was positioned automatically.

## Authoring

On StorySequenceRunner, assign one Empty to Player Transform. Its world position is the arrival point and its Y rotation is the initial body heading. Leave it empty to retain the existing sequence opening. A complete NavMesh of the configured agent type (default 0) must exist. Approach timeout defaults to 30 actor seconds.

Approach uses exclusive input ownership and the existing UCC motor, without a NavMeshAgent, Transform translation, forced velocity or animation cadence matching. Arrival requires <=15 cm planar distance, <=30 cm height difference, speed <=0.05 m/s for three fixed ticks and heading <=2 degrees. Failed approaches release control and rearm proximity triggers after exit.

The existing UCC CharacterIK supplies sequence head gaze. A different resolved speaker changes a cached direction clamped to +/-45 degrees relative to the body. The same speaker, waiting and Lucian speaking retain the snapshot. Only the owner decides; network state transmits body heading, target yaw and session/revision. Existing IK settings are restored when ownership ends. Timelines release sequence gaze before playback.

## Validation

- Unity 6000.6.4f1 runtime/Editor compilation passed.
- 59 Edit Mode cases passed: dialogue regression, sequence authoring, cycle dialogue, plus new gaze limits/session/network and complete NavMesh routing around an obstacle.
- Actual Lucian integration in Iluvilirae_Test: PASS arrival distance/heading/stopped checks before content, body heading retained, same-speaker snapshot retained, new-speaker yaw changed, successful release, restart and abort cleanup.
- Play Mode exposed the UCC input-disable event also disabling the locomotion handler. Narrative walking now retains that handler, rejects human movement through its lease/focus and releases the same existing external-lock handles. The final actual-motor integration passed after this correction.
- Final standalone compilation covers the no-marker callback compatibility and priority-interruption guards added after the integration run; these do not change its successful approach path.

No user scene or prefab edits were saved by the probe. Visual certification of the actual animated head angle, slopes, dynamic obstructions, pause/slow motion and live host/client sessions remains separate. The automated head assertions verify the commanded angle and replicated state; they do not constitute a rendered visual acceptance.
