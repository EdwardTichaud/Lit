/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Character.Abilities
{
    using Opsive.Shared.Game;
    using Opsive.Shared.StateSystem;
    using Opsive.UltimateCharacterController.Character;
    using Opsive.UltimateCharacterController.Objects.CharacterAssist;
    using Opsive.UltimateCharacterController.Utility;
    using UnityEngine;
    using EventHandler = Opsive.Shared.Events.EventHandler;

    /// <summary>
    /// The Ride ability allows the character to ride another Ultimate Character Locomotion character (such as a horse).
    /// </summary>
    [DefaultStartType(AbilityStartType.ButtonDown)]
    [DefaultStopType(AbilityStopType.ButtonToggle)]
    [DefaultInputName("Action")]
    [DefaultAbilityIndex(12)]
    [DefaultAllowRotationalInput(false)]
    [DefaultUseGravity(AbilityBoolOverride.False)]
    [DefaultDetectHorizontalCollisions(AbilityBoolOverride.False)]
    [DefaultDetectVerticalCollisions(AbilityBoolOverride.False)]
    [DefaultUseRootMotionPosition(AbilityBoolOverride.True)]
    [DefaultUseRootMotionRotation(AbilityBoolOverride.True)]
    [DefaultEquippedSlots(0)]
    [AllowDuplicateTypes]
    [System.Serializable]
    public class Ride : DetectObjectAbilityBase, Items.IItemToggledReceiver
    {
        /// <summary>
        /// Specifies the current status of the character.
        /// </summary>
        protected enum RideState
        {
            Mount,              // The character is mounting the object.
            Ride,               // The character is riding the object.
            WaitForItemUnequip, // The character is waiting for the item to be unequipped so it can then start to dismount.
            Dismount,           // The character is dismounting from the object.
            DismountComplete    // The character is no longer on the rideable object.
        }

        [Tooltip("Specifies if the ability should wait for the OnAnimatorMount animation event or wait for the specified duration before mounting to the rideable object.")]
        [SerializeField] protected AnimationEventTrigger m_MountEvent = new AnimationEventTrigger(true, 0.2f);
        [Tooltip("Specifies if the ability should wait for the OnAnimatorRideMountComplete animation event or wait for the specified duration before allowing the character to dismount after mounting.")]
        [SerializeField] protected AnimationEventTrigger m_MountCompleteEvent = new AnimationEventTrigger(false, 0.2f);
        [Tooltip("Specifies the state name that should activate when the character is mounted.")]
        [SerializeField] protected string m_MountCompleteState = "RideMounted";
        [Tooltip("Specifies if the ability should wait for the OnAnimatorDismount animation event or wait for the specified duration before dismounting from the rideable object.")]
        [SerializeField] protected AnimationEventTrigger m_DismountEvent = new AnimationEventTrigger(true, 0.2f);
        [Tooltip("After the character mounts should the ability reequip the item that the character had before mounting?")]
        [SerializeField] protected bool m_ReequipItemAfterMount = true;

        public AnimationEventTrigger MountEvent { get { return m_MountEvent; } set { m_MountEvent.CopyFrom(value); } }
        public AnimationEventTrigger MountCompleteEvent { get { return m_MountCompleteEvent; } set { m_MountCompleteEvent.CopyFrom(value); } }
        public string MountCompleteState { get { return m_MountCompleteState; } set { m_MountCompleteState = value; } }
        public AnimationEventTrigger DismountEvent { get { return m_DismountEvent; } set { m_DismountEvent.CopyFrom(value); } }
        public bool ReequipItemAfterMount { get { return m_ReequipItemAfterMount; } set { m_ReequipItemAfterMount = value; } }

        public Rideable Rideable { get { return m_Rideable; } }
        [System.NonSerialized] protected Rideable m_Rideable;
        private UltimateCharacterLocomotionHandler m_CharacterLocomotionHandler;

        private int m_StartAllowEquippedItemSlotsMask;
        private bool m_LeftMount;
        private ScheduledEventBase m_MountDismountEvent;
        protected RideState m_RideState = RideState.DismountComplete;
        private float m_Epsilon = 0.99999f;
        private bool m_CanDismountAfterMount;

        public override int AbilityIntData
        {
            get
            {
                if (m_RideState == RideState.Mount) {
                    return m_LeftMount ? 1 : 2;
                } else if (m_RideState == RideState.Ride) {
                    return 3;
                } else if (m_RideState == RideState.Dismount) {
                    return m_LeftMount ? 4 : 5;
                } else if (m_RideState == RideState.DismountComplete) {
                    return 6;
                }

                return base.AbilityIntData;
            }
        }
        public UltimateCharacterLocomotion CharacterLocomotion { get { return m_CharacterLocomotion; } }
        public GameObject GameObject { get { return m_GameObject; } }

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_CharacterLocomotionHandler = m_GameObject.GetCachedComponent<UltimateCharacterLocomotionHandler>();
            if (m_CharacterLocomotionHandler == null) {
                Debug.LogError("Error: The Ride ability requires the UltimateCharacterLocomotionHandler.", m_GameObject);
            }

            m_MountEvent.RegisterUnregisterAnimationEvent(true, m_GameObject, "OnAnimatorRideMount", OnMount);
            m_MountCompleteEvent.RegisterUnregisterAnimationEvent(true, m_GameObject, "OnAnimatorRideMountComplete", OnMountComplete);
            m_DismountEvent.RegisterUnregisterAnimationEvent(true, m_GameObject, "OnAnimatorRideDismount", OnDismount);
        }

        /// <summary>
        /// Validates the object to ensure it is valid for the current ability.
        /// </summary>
        /// <param name="obj">The object being validated.</param>
        /// <param name="raycastHit">The raycast hit of the detected object. Will be null for trigger detections.</param>
        /// <returns>True if the object is valid. The object may not be valid if it doesn't have an ability-specific component attached.</returns>
        protected override bool ValidateObject(GameObject obj, RaycastHit? raycastHit)
        {
            if (!base.ValidateObject(obj, raycastHit)) {
                return false;
            }

            var CharacterLocomotion = obj.GetCachedParentComponent<UltimateCharacterLocomotion>();
            if (CharacterLocomotion == null) {
                return false;
            }

            // A rideable object must be added to the character.
            var rideable = CharacterLocomotion.GetAbility<Rideable>();
            if (rideable == null) {
                return false;
            }

            m_Rideable = rideable;
            return true;
        }

        /// <summary>
        /// Returns the possible MoveTowardsLocations that the character can move towards.
        /// </summary>
        /// <returns>The possible MoveTowardsLocations that the character can move towards.</returns>
        public override MoveTowardsLocation[] GetMoveTowardsLocations()
        {
            return m_Rideable.GameObject.GetComponentsInChildren<MoveTowardsLocation>();
        }

        /// <summary>
        /// The ability will start - perform any initialization before starting.
        /// </summary>
        /// <returns>True if the ability should start.</returns>
        public override bool AbilityWillStart()
        {
            m_StartAllowEquippedItemSlotsMask = m_AllowEquippedSlotsMask;

            return base.AbilityWillStart();
        }

        /// <summary>
        /// Called when the ablity is tried to be started. If false is returned then the ability will not be started.
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // An attribute may prevent the ability from starting.
            if (!base.CanStartAbility()) {
                return false;
            }

            if (m_Rideable == null) {
                return false;
            }

            if (!m_Rideable.CanMount(this)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// The ability has started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_LeftMount = m_Rideable.CharacterLocomotion.InverseTransformPoint(m_Transform.position).x < 0;
            m_Rideable.Mount(this);
            m_CharacterLocomotion.SetMovingPlatform(m_Rideable.GameObject.transform, true);
            m_CharacterLocomotion.AlignToUpDirection = true;

            // The character will look independently of the rotation.
            EventHandler.ExecuteEvent(m_GameObject, "OnCharacterForceIndependentLook", true);
            m_RideState = RideState.Mount;
            m_CanDismountAfterMount = false;
            UpdateAbilityAnimatorParameters();

            m_MountCompleteEvent.CancelWaitForEvent();
            m_DismountEvent.CancelWaitForEvent();
            m_MountEvent.WaitForEvent(true);

            EventHandler.RegisterEvent<Ability, bool>(m_GameObject, "OnCharacterAbilityActive", OnAbilityActive);
        }

        /// <summary>
        /// Called when another ability is attempting to start and the current ability is active.
        /// Returns true or false depending on if the new ability should be blocked from starting.
        /// </summary>
        /// <param name="startingAbility">The ability that is starting.</param>
        /// <returns>True if the ability should be blocked.</returns>
        public override bool ShouldBlockAbilityStart(Ability startingAbility)
        {
#if THIRD_PERSON_CONTROLLER
            if (startingAbility is ThirdPersonController.Character.Abilities.Items.ItemPullback) {
                return true;
            }
#endif
            if (startingAbility is Items.InAirMeleeUse) {
                return true;
            }
            
            // The character cannot interact with any items while mounting/dismounting.
            if (m_RideState != RideState.Ride && startingAbility is Items.ItemAbility) {
                return true;
            }
            return startingAbility is HeightChange || base.ShouldBlockAbilityStart(startingAbility);
        }

        /// <summary>
        /// Called when the current ability is attempting to start and another ability is active.
        /// Returns true or false depending on if the active ability should be stopped.
        /// </summary>
        /// <param name="activeAbility">The ability that is currently active.</param>
        /// <returns>True if the ability should be stopped.</returns>
        public override bool ShouldStopActiveAbility(Ability activeAbility)
        {
#if THIRD_PERSON_CONTROLLER
            if (activeAbility is ThirdPersonController.Character.Abilities.Items.ItemPullback) {
                return true;
            }
#endif
            return base.ShouldStopActiveAbility(activeAbility);
        }

        /// <summary>
        /// Mounts the character on the object.
        /// </summary>
        private void OnMount()
        {
            if (m_RideState != RideState.Mount || m_Rideable == null) {
                m_MountEvent.CancelWaitForEvent();
                return;
            }

            m_MountEvent.CancelWaitForEvent();
            m_DismountEvent.CancelWaitForEvent();
            
            m_RideState = RideState.Ride;
            m_CharacterLocomotion.ForceRootMotionPosition = false;
            m_CharacterLocomotion.AllowRootMotionPosition = false;
            UpdateAbilityAnimatorParameters(true);
            m_Rideable.OnCharacterMount();
            m_MountCompleteEvent.WaitForEvent(true);
            m_MountDismountEvent = null;

            // The item was unequipped when mounting - it may need to be reequiped again.
            if (m_CharacterLocomotion.ItemEquipVerifierAbility != null) {
                if (m_ReequipItemAfterMount) {
                    m_AllowEquippedSlotsMask = -1; // Set the mask to -1 so the EquipUnequip ability will not be blocked by a 0 mask value.
                    m_CharacterLocomotion.ItemEquipVerifierAbility.TryToggleItem(this, false);
                } else {
                    m_CharacterLocomotion.ItemEquipVerifierAbility.Reset();
                }
            }
        }

        /// <summary>
        /// The mounted animation has completed and the character can dismount.
        /// </summary>
        private void OnMountComplete()
        {
            if (m_RideState != RideState.Ride) {
                m_MountCompleteEvent.CancelWaitForEvent();
                return;
            }

            m_MountCompleteEvent.CancelWaitForEvent();
            m_CanDismountAfterMount = true;
            if (!string.IsNullOrEmpty(m_MountCompleteState)) {
                StateManager.SetState(m_GameObject, m_MountCompleteState, true);
                if (m_Inventory != null && m_StateAppendItemIdentifierName) {
                    for (int i = 0; i < m_Inventory.SlotCount; ++i) {
                        var item = m_Inventory.GetActiveCharacterItem(i);
                        if (item != null && item.IsActive()) {
                            var itemStateName = m_MountCompleteState + item.ItemDefinition.name;
                            StateManager.SetState(m_GameObject, itemStateName, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Can the input stop the ability?
        /// </summary>
        /// <param name="playerInput">A reference to the input component.</param>
        /// <returns>True if the input can stop the ability.</returns>
        public override bool CanInputStopAbility(Opsive.Shared.Input.IPlayerInput playerInput)
        {
            if (m_RideState != RideState.Ride || !m_CanDismountAfterMount) {
                return false;
            }

            return base.CanInputStopAbility(playerInput);
        }

        /// <summary>
        /// Updates the input vector.
        /// </summary>
        public override void Update()
        {
            if (m_RideState == RideState.Ride) {
                // The input parameters should be sent to the the rideable object.
                if (m_Rideable == null || m_Rideable.CharacterLocomotionHandler == null) {
                    StopAbility(true);
                    return;
                }
                m_Rideable.CharacterLocomotionHandler.OverriddenHorizontalMovement = m_CharacterLocomotion.RawInputVector.x;
                m_Rideable.CharacterLocomotionHandler.OverriddenForwardMovement = m_CharacterLocomotion.RawInputVector.y;
                m_Rideable.CharacterLocomotionHandler.OverriddenLookVector = m_CharacterLocomotionHandler.LookVector;
            } else if (m_RideState == RideState.WaitForItemUnequip) {
                ClearRideableInputOverride();
            } else if (m_RideState != RideState.DismountComplete) {
                ClearRideableInputOverride();
                m_CharacterLocomotion.InputVector = Vector3.zero;
            } else {
                StopAbility();
            }
        }

        /// <summary>
        /// Clears the input values that are being forwarded to the rideable object.
        /// </summary>
        private void ClearRideableInputOverride()
        {
            if (m_Rideable == null || m_Rideable.CharacterLocomotionHandler == null) {
                return;
            }

            m_Rideable.CharacterLocomotionHandler.OverriddenHorizontalMovement = 0;
            m_Rideable.CharacterLocomotionHandler.OverriddenForwardMovement = 0;
            m_Rideable.CharacterLocomotionHandler.OverriddenLookVector = Vector2.zero;
        }

        /// <summary>
        /// Update the controller's rotation values.
        /// </summary>
        public override void ApplyRotation()
        {
            if (m_RideState != RideState.Ride && m_RideState != RideState.WaitForItemUnequip) {
                return;
            }

            m_CharacterLocomotion.DesiredRotation = Quaternion.Inverse(m_Transform.rotation) * m_Rideable.RideLocation.rotation;
        }

        /// <summary>
        /// Update the controller's position values.
        /// </summary>
        public override void ApplyPosition()
        {
            if (m_RideState != RideState.Ride && m_RideState != RideState.WaitForItemUnequip) {
                return;
            }

            // The rideable character can move independently of the rideable location.
            m_CharacterLocomotion.DesiredMovement = m_Rideable.RideLocation.position - m_Transform.position;
        }

        /// <summary>
        /// The character's ability has been started or stopped.
        /// </summary>
        /// <param name="ability">The ability which was started or stopped.</param>
        /// <param name="active">True if the ability was started, false if it was stopped.</param>
        private void OnAbilityActive(Ability ability, bool active)
        {
            if (!(ability is SpeedChange)) {
                return;
            }

            var rideableSpeedChange = m_Rideable.CharacterLocomotion.GetAbility<SpeedChange>();
            if (rideableSpeedChange != null) {
                if (active) {
                    m_Rideable.CharacterLocomotion.TryStartAbility(rideableSpeedChange);
                } else {
                    m_Rideable.CharacterLocomotion.TryStopAbility(rideableSpeedChange);
                }
            }
        }

        /// <summary>
        /// Callback when the ability tries to be stopped. Start the dismount.
        /// </summary>
        public override void WillTryStopAbility()
        {
            if (m_RideState != RideState.Ride) {
                return;
            }

            if (!m_CanDismountAfterMount) {
                return;
            }

            // The character may not have space to dismount.
            if (!m_Rideable.CanDismount(ref m_LeftMount)) {
                return;
            }

            if (m_CharacterLocomotion.ItemEquipVerifierAbility != null) {
                // Don't allow a dismount if the character is equipping an item just after mounting.
                if (m_CharacterLocomotion.ItemEquipVerifierAbility.IsActive) {
                    return;
                }

                // If an item is equipped then it should first be unequipped before dismounting.
                m_AllowEquippedSlotsMask = m_StartAllowEquippedItemSlotsMask;
                if (m_CharacterLocomotion.ItemEquipVerifierAbility.TryToggleItem(this, true)) {
                    m_RideState = RideState.WaitForItemUnequip;
                    return;
                }
            }

            StartDismount();
        }

        /// <summary>
        /// Starts to dismount from the RideableObject.
        /// </summary>
        private void StartDismount()
        {
            if (m_Rideable == null) {
                return;
            }

            m_MountEvent.CancelWaitForEvent();
            m_MountCompleteEvent.CancelWaitForEvent();
            ClearRideableInputOverride();
            m_CanDismountAfterMount = false;
            m_RideState = RideState.Dismount;
            m_CharacterLocomotion.ForceRootMotionPosition = true;
            m_CharacterLocomotion.AllowRootMotionPosition = true;
            UpdateAbilityAnimatorParameters();
            m_Rideable.StartDismount();
            m_DismountEvent.WaitForEvent(true);
            if (!string.IsNullOrEmpty(m_MountCompleteState)) {
                StateManager.SetState(m_GameObject, m_MountCompleteState, false);
                if (m_Inventory != null && m_StateAppendItemIdentifierName) {
                    for (int i = 0; i < m_Inventory.SlotCount; ++i) {
                        var item = m_Inventory.GetActiveCharacterItem(i);
                        if (item != null && item.IsActive()) {
                            var itemStateName = m_MountCompleteState + item.ItemDefinition.name;
                            StateManager.SetState(m_GameObject, itemStateName, false);
                        }
                    }
                }
            }

            // If the ability is active then it should also be stopped.
            var aimAbility = m_CharacterLocomotion.GetAbility<Items.Aim>();
            if (aimAbility != null) {
                aimAbility.StopAbility();
            }
        }

        /// <summary>
        /// The character has dismounted - stop the ability.
        /// </summary>
        private void OnDismount()
        {
            if (m_RideState != RideState.Dismount) {
                m_DismountEvent.CancelWaitForEvent();
                return;
            }

            m_MountEvent.CancelWaitForEvent();
            m_MountCompleteEvent.CancelWaitForEvent();
            m_DismountEvent.CancelWaitForEvent();
            ClearRideableInputOverride();
            m_CanDismountAfterMount = false;

            m_RideState = RideState.DismountComplete;
            m_CharacterLocomotion.ForceRootMotionPosition = false;
            m_CharacterLocomotion.ForceRootMotionRotation = false;
            UpdateAbilityAnimatorParameters();
            if (m_Rideable != null) {
                m_Rideable.Dismounted();
                m_Rideable = null;
            }
            m_MountDismountEvent = null;
            m_CharacterLocomotion.SetMovingPlatform(null);
            m_CharacterLocomotion.AlignToUpDirection = false;
            EventHandler.ExecuteEvent(m_GameObject, "OnCharacterForceIndependentLook", false);

            StopAbility();
        }

        /// <summary>
        /// Can the ability be stopped?
        /// </summary>
        /// <param name="force">Should the ability be force stopped?</param>
        /// <returns>True if the ability can be stopped.</returns>
        public override bool CanStopAbility(bool force)
        {
            if (force) { return true; }

            // The character has to be dismounted in order to stop.
            return m_RideState == RideState.DismountComplete && Vector3.Dot(m_Transform.rotation * Vector3.up, m_CharacterLocomotion.Up) >= m_Epsilon;
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        /// <param name="force">Was the ability force stopped?</param>
        protected override void AbilityStopped(bool force)
        {
            base.AbilityStopped(force);

            m_AllowEquippedSlotsMask = m_StartAllowEquippedItemSlotsMask;
            m_MountEvent.CancelWaitForEvent();
            m_MountCompleteEvent.CancelWaitForEvent();
            m_DismountEvent.CancelWaitForEvent();
            ClearRideableInputOverride();
            m_CanDismountAfterMount = false;

            if (m_MountDismountEvent != null) {
                Scheduler.Cancel(m_MountDismountEvent);
                m_MountDismountEvent = null;
            }

            // If the state isn't complete then the ability was force stopped.
            if (m_RideState != RideState.DismountComplete && m_Rideable != null) {
                m_Rideable.StartDismount();
                m_Rideable.Dismounted();
                m_Rideable = null;
                m_CharacterLocomotion.SetMovingPlatform(null);
                m_CharacterLocomotion.AlignToUpDirection = false;
                EventHandler.ExecuteEvent(m_GameObject, "OnCharacterForceIndependentLook", false);
            }
            EventHandler.UnregisterEvent<Ability, bool>(m_GameObject, "OnCharacterAbilityActive", OnAbilityActive);
        }

        /// <summary>
        /// The ItemEquipVerifier ability has toggled an item slot.
        /// </summary>
        public void ItemToggled()
        {
            if (m_RideState != RideState.WaitForItemUnequip) {
                return;
            }

            // The character can dismount as soon as the item is no longer equipped.
            StartDismount();
        }

        /// <summary>
        /// The object has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();

            m_MountEvent.RegisterUnregisterAnimationEvent(false, m_GameObject, "OnAnimatorRideMount", OnMount);
            m_MountCompleteEvent.RegisterUnregisterAnimationEvent(false, m_GameObject, "OnAnimatorRideMountComplete", OnMountComplete);
            m_DismountEvent.RegisterUnregisterAnimationEvent(false, m_GameObject, "OnAnimatorRideDismount", OnDismount);
        }
    }
}
