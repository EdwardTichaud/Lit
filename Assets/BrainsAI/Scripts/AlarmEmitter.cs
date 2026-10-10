using UnityEngine;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Emits an "alarm noise" that can be detected by AI Brains within a certain distance.
    /// Useful for simulating events like explosions, screams, or alarms that alert nearby AI agents.
    /// </summary>
    public class AlarmEmitter : MonoBehaviour
    {
        [Tooltip("The emitter will trigger each time this object get enabled.")]
        public bool playOnEnable;

        [Tooltip("The radius within which AI agents can detect and respond to this alarm.")]
        public float alarmRadius = 10f;

        [Tooltip("If true, the alarm will affect all AI groups. If false, only specific groups in 'alarmGroups' will be alerted.")]
        public bool alertAllGroups = false;

        [Tooltip("The specific AI groups that should be alerted by this alarm. Ignored if 'alertAllGroups' is true.")]
        public string[] alarmGroups;

        void OnEnable()
        {
            if (playOnEnable)
                TriggerAlarm();
        }

        /// <summary>
        /// Emits the alarm and alerts all eligible Brain agents within the specified radius.
        /// </summary>
        public void TriggerAlarm()
        {
            foreach (var brain in Brain.Brains)
            {
                // Check group filtering if not alerting all groups.
                if (!alertAllGroups)
                {
                    bool canHear = false;
                    foreach (var group in alarmGroups)
                    {
                        if (brain.groupName == group)
                        {
                            canHear = true;
                            break;
                        }
                    }

                    if (!canHear)
                        continue;
                }

                // Check distance between the alarm source and the AI agent.
                float distanceToBrain = Vector3.Distance(transform.position, brain.transform.position);
                if (distanceToBrain <= alarmRadius)
                {
                    // Start the agent's alarm reaction routine.
                    StartCoroutine(brain.OnAlarmHeard(transform.position));
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw a wire sphere to visualize the alarm radius in the editor.
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(transform.position, alarmRadius);
        }
    }
}
