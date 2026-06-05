/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Editor.Controls.Types.AbilityDrawers
{
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Opsive.UltimateCharacterController.Character.Abilities;
    using Opsive.UltimateCharacterController.Game;
    using Opsive.UltimateCharacterController.Items;
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements AbilityDrawer for the Ragdoll ControlType.
    /// </summary>
    [ControlType(typeof(Ragdoll))]
    public class RagdollInspectorDrawer : AbilityDrawer
    {
        /// <summary>
        /// Returns the control that should be used for the specified ControlType.
        /// </summary>
        /// <param name="unityObject">A reference to the owning Unity Object.</param>
        /// <param name="target">The object that should have its fields displayed.</param>
        /// <param name="container">The container that the UIElements should be added to.</param>
        /// <param name="onChangeEvent">An event that is sent when the value changes. Returns false if the control cannot be changed.</param>
        /// <param name="onValidateChange">Event callback which validates if a field can be changed.</param>
        public override void CreateDrawer(UnityEngine.Object unityObject, object target, VisualElement container, System.Func<System.Reflection.FieldInfo, object, bool> onValidateChange, System.Action<object> onChangeEvent)
        {
            FieldInspectorView.AddFields(unityObject, target, Shared.Utility.MemberVisibility.Public, container, onChangeEvent, null, onValidateChange, false, null, LabelControl.WidthAdjustment.UnityDefault);

            var horizontalContainer = new VisualElement();
            horizontalContainer.AddToClassList("horizontal-layout");
            container.Add(horizontalContainer);
            var addCollidersButton = new Button(() =>
            {
                AddRagdollColliders((unityObject as Component).gameObject);
            });
            addCollidersButton.text = "Add Ragdoll Colliders";
            addCollidersButton.style.flexGrow = 1;
            horizontalContainer.Add(addCollidersButton);

            var removeCollidersButton = new Button(() =>
            {
                RemoveRagdollColliders((unityObject as Component).gameObject);
            });
            removeCollidersButton.text = "Remove Ragdoll Colliders";
            removeCollidersButton.style.flexGrow = 1;
            horizontalContainer.Add(removeCollidersButton);
        }

        /// <summary>
        /// Uses Unity's Ragdoll Builder to create the ragdoll.
        /// </summary>
        /// <param name="character">The character to add the ragdoll to.</param>
        /// <param name="autoCreate">Should the ragdoll be created automatically without any UI?</param>
        public static void AddRagdollColliders(GameObject character, bool autoCreate = false)
        {
            var animatorMonitor = character.GetComponentInChildren<Character.AnimatorMonitor>();
            if (animatorMonitor == null) {
                return;
            }
            var animator = animatorMonitor.GetComponent<Animator>();
            if (animator == null) {
                return;
            }

            EditorWindow ragdollWindow;
            var ragdollBuilder = GetRagdollBuilder(autoCreate, out ragdollWindow);
            if (ragdollBuilder == null) {
                Debug.LogError("Error: Unable to open Unity's Ragdoll Builder.");
                return;
            }

            SetRagdollBuilderValue(ragdollBuilder, "animator", animator);
            SetRagdollBuilderValue(ragdollBuilder, "pelvis", animator.GetBoneTransform(HumanBodyBones.Hips));
            SetRagdollBuilderValue(ragdollBuilder, "leftHips", animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg));
            SetRagdollBuilderValue(ragdollBuilder, "leftKnee", animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg));
            SetRagdollBuilderValue(ragdollBuilder, "leftFoot", animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            SetRagdollBuilderValue(ragdollBuilder, "rightHips", animator.GetBoneTransform(HumanBodyBones.RightUpperLeg));
            SetRagdollBuilderValue(ragdollBuilder, "rightKnee", animator.GetBoneTransform(HumanBodyBones.RightLowerLeg));
            SetRagdollBuilderValue(ragdollBuilder, "rightFoot", animator.GetBoneTransform(HumanBodyBones.RightFoot));
            SetRagdollBuilderValue(ragdollBuilder, "leftArm", animator.GetBoneTransform(HumanBodyBones.LeftUpperArm));
            SetRagdollBuilderValue(ragdollBuilder, "leftElbow", animator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
            SetRagdollBuilderValue(ragdollBuilder, "rightArm", animator.GetBoneTransform(HumanBodyBones.RightUpperArm));
            SetRagdollBuilderValue(ragdollBuilder, "rightElbow", animator.GetBoneTransform(HumanBodyBones.RightLowerArm));
            SetRagdollBuilderValue(ragdollBuilder, "middleSpine", animator.GetBoneTransform(HumanBodyBones.Spine));
            SetRagdollBuilderValue(ragdollBuilder, "head", animator.GetBoneTransform(HumanBodyBones.Head));

            var consistencyMethod = ragdollBuilder.GetType().GetMethod("CheckConsistency", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (consistencyMethod != null) {
                var errorString = (string)consistencyMethod.Invoke(ragdollBuilder, null);
                SetRagdollBuilderValue(ragdollBuilder, "errorString", errorString);
                SetRagdollBuilderValue(ragdollBuilder, "isValid", string.IsNullOrEmpty(errorString));
            }

            if (autoCreate) {
                var createMethod = ragdollBuilder.GetType().GetMethod("OnWizardCreate", BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (createMethod != null) {
                    try {
                        createMethod.Invoke(ragdollBuilder, null);
                        if (ragdollWindow != null) {
                            ragdollWindow.Close();
                        }
                    } catch (Exception /*e*/) {
                        Debug.LogError("Error: The Ragdoll Builder is not able to automatically create the ragdoll.");
                    }
                }
            } else if (ragdollWindow != null) {
                ragdollWindow.Repaint();
            }
        }

        /// <summary>
        /// Returns the active ragdoll builder instance, opening the UI when necessary.
        /// </summary>
        /// <param name="autoCreate">Should the builder be created without a window?</param>
        /// <param name="ragdollWindow">The ragdoll window associated with the builder.</param>
        /// <returns>The ragdoll builder instance.</returns>
        private static object GetRagdollBuilder(bool autoCreate, out EditorWindow ragdollWindow)
        {
            ragdollWindow = null;

            var ragdollBuilderType = Type.GetType("UnityEditor.RagdollBuilder, UnityEditor");
            if (ragdollBuilderType == null) {
                return null;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(ragdollBuilderType)) {
                var legacyWizard = FindOrCreateRagdollObject(ragdollBuilderType, "CreateWizard");
                ragdollWindow = legacyWizard as EditorWindow;
                return legacyWizard;
            }

            if (autoCreate) {
                return Activator.CreateInstance(ragdollBuilderType);
            }

            var ragdollWindowType = Type.GetType("UnityEditor.RagdollBuilderWindow, UnityEditor");
            var ragdollObject = FindOrCreateRagdollObject(ragdollWindowType, "CreateWindow");
            ragdollWindow = ragdollObject as EditorWindow;
            if (ragdollWindow == null || ragdollWindowType == null) {
                return null;
            }

            var ragdollBuilderField = ragdollWindowType.GetField("ragdollBuilder", BindingFlags.NonPublic | BindingFlags.Instance);
            if (ragdollBuilderField == null) {
                return null;
            }

            var ragdollBuilder = ragdollBuilderField.GetValue(ragdollWindow);
            if (ragdollBuilder == null) {
                ragdollBuilder = Activator.CreateInstance(ragdollBuilderType);
                ragdollBuilderField.SetValue(ragdollWindow, ragdollBuilder);
            }
            return ragdollBuilder;
        }

        /// <summary>
        /// Finds or creates the ragdoll wizard/window object.
        /// </summary>
        /// <param name="ragdollObjectType">The type of object representing the ragdoll UI.</param>
        /// <param name="createMethodName">The name of the static method used to create the UI.</param>
        /// <returns>The wizard or window object.</returns>
        private static UnityEngine.Object FindOrCreateRagdollObject(Type ragdollObjectType, string createMethodName)
        {
            if (ragdollObjectType == null || !typeof(UnityEngine.Object).IsAssignableFrom(ragdollObjectType)) {
                return null;
            }

            var objects = Resources.FindObjectsOfTypeAll(ragdollObjectType);
            if (objects == null || objects.Length == 0) {
                var createMethod = ragdollObjectType.GetMethod(createMethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (createMethod != null) {
                    createMethod.Invoke(null, null);
                } else {
                    EditorApplication.ExecuteMenuItem("GameObject/3D Object/Ragdoll...");
                }

                objects = Resources.FindObjectsOfTypeAll(ragdollObjectType);
            }

            return objects != null && objects.Length > 0 ? objects[0] : null;
        }

        /// <summary>
        /// Sets the field or property value on the ragdoll builder.
        /// </summary>
        /// <param name="obj">The object being updated.</param>
        /// <param name="name">The field or property name.</param>
        /// <param name="value">The value to assign.</param>
        private static void SetRagdollBuilderValue(object obj, string name, object value)
        {
            var type = obj.GetType();
            while (type != null) {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) {
                    field.SetValue(obj, value);
                    return;
                }

                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanWrite) {
                    property.SetValue(obj, value, null);
                    return;
                }

                type = type.BaseType;
            }
        }

        /// <summary>
        /// Removes the ragdoll colliders from the specified character.
        /// </summary>
        /// <param name="character">The character to remove the ragdoll colliders from.</param>
        public static void RemoveRagdollColliders(GameObject character)
        {
            // If the character is a humanoid then the ragdoll colliders are known ahead of time. Generic characters are required to be searched recursively.
            var animatorMonitor = character.GetComponentInChildren<Character.AnimatorMonitor>();
            if (animatorMonitor == null) {
                return;
            }
            var animator = animatorMonitor.GetComponent<Animator>();
            if (animator != null && animator.GetBoneTransform(HumanBodyBones.Head) != null) {
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.Hips), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.LeftFoot), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.RightFoot), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.RightUpperArm), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.RightLowerArm), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.Spine), false);
                RemoveRagdollColliders(animator.GetBoneTransform(HumanBodyBones.Head), false);
            } else {
                RemoveRagdollColliders(character.transform, true);
            }
        }

        /// <summary>
        /// Removes the ragdoll colliders from the transform. If removeChildColliders is true then the method will be called recursively.
        /// </summary>
        /// <param name="transform">The transform to remove the colliders from.</param>
        /// <param name="removeChildColliders">True if the colliders should be searched for recursively.</param>
        private static void RemoveRagdollColliders(Transform transform, bool removeChildColliders)
        {
            if (transform == null) {
                return;
            }

            if (removeChildColliders) {
                for (int i = transform.childCount - 1; i >= 0; --i) {
                    var child = transform.GetChild(i);
                    // No ragdoll colliders exist under the Character layer GameObjects no under the item GameObjects.
                    if (child.gameObject.layer == LayerManager.Character || child.GetComponent<ItemPlacement>() != null || child.GetComponent<CharacterItemSlot>() != null) {
                        continue;
                    }

#if FIRST_PERSON_CONTROLLER
                    // First person objects do not contain any ragdoll colliders.
                    if (child.GetComponent<UltimateCharacterController.FirstPersonController.Character.FirstPersonObjects>() != null) {
                        continue;
                    }
#endif
                    // Remove the ragdoll from the transform and recursively check the children.
                    RemoveRagdollCollider(child);
                    RemoveRagdollColliders(child, true);
                }
            } else {
                RemoveRagdollCollider(transform);
            }
        }

        /// <summary>
        /// Removes the ragdoll colliders from the specified transform.
        /// </summary>
        /// <param name="transform">The transform to remove the ragdoll colliders from.</param>
        private static void RemoveRagdollCollider(Transform transform)
        {
            var collider = transform.GetComponent<Collider>();
            var rigidbody = transform.GetComponent<Rigidbody>();
            // If the object doesn't have a collider and a rigidbody then it isn't a ragdoll collider.
            if (collider == null || rigidbody == null) {
                return;
            }
            UnityEngine.Object.DestroyImmediate(collider, true);
            var characterJoint = transform.GetComponent<CharacterJoint>();
            if (characterJoint != null) {
                UnityEngine.Object.DestroyImmediate(characterJoint, true);
            }
            // The rigidbody must be removed last to prevent conflicts.
            UnityEngine.Object.DestroyImmediate(rigidbody, true);
        }
    }
}
