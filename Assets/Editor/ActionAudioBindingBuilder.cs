using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ActionAudioBindingBuilder
{
    private const string ActionDefaultsPath = "Assets/Audio/AudioClips/ActionDefaults";
    private const string BindingClipsPath = "Assets/Audio/AudioClips/ActionBindings";
    private const string ActionLibraryPath = "Assets/Core/Resources/Audio/ActionAudioLibrary_Default.asset";
    private readonly struct ClipSpec
    {
        public readonly string key;
        public readonly string directory;
        public readonly string title;
        public readonly string sourcePath;
        public readonly float volume;

        public ClipSpec(string key, string directory, string title, string sourcePath, float volume = 0.8f)
        {
            this.key = key;
            this.directory = directory;
            this.title = title;
            this.sourcePath = sourcePath;
            this.volume = volume;
        }
    }

    [MenuItem("Tools/Lit/Audio/Apply Action Audio Bindings")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Action audio bindings cannot be changed while entering or running Play Mode.");
        }

        Dictionary<string, AudioClipSO> clips = CreateOrUpdateClips();
        BindActionLibrary(clips);
        BindSurfaces(clips);
        BindSkillAssets(clips);
        BindLightSkillAssets(clips);
        BindCounterAndAttackAssets(clips);
        BindStandaloneEffectAssets(clips);
        BindPrefabs(clips);
        BindScenes(clips);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateInternal(clips);
        Debug.Log("[ActionAudioBindingBuilder] Action audio assets and bindings are valid.");
    }

    [MenuItem("Tools/Lit/Audio/Validate Action Audio Bindings")]
    public static void Validate()
    {
        ValidateInternal(CreateOrUpdateClips());
        Debug.Log("[ActionAudioBindingBuilder] Action audio bindings are valid.");
    }

    private static Dictionary<string, AudioClipSO> CreateOrUpdateClips()
    {
        EnsureFolder(ActionDefaultsPath);
        EnsureFolder(BindingClipsPath);

        ClipSpec[] specs =
        {
            new ClipSpec("SFX_UI_Open", ActionDefaultsPath, "UI Open", "Assets/Audio/Fantasy UI SFX - Lite Edition/Interface 1-1.wav", 0.55f),
            new ClipSpec("SFX_UI_Close", ActionDefaultsPath, "UI Close", "Assets/Audio/Fantasy UI SFX - Lite Edition/Interface 1-2.wav", 0.55f),
            new ClipSpec("SFX_UI_Confirm", ActionDefaultsPath, "UI Confirm", "Assets/Audio/Sounds/A classer/Validation curseur.mp3", 0.6f),
            new ClipSpec("SFX_UI_Cancel", ActionDefaultsPath, "UI Cancel", "Assets/Audio/Sounds/Annulation curseur.mp3", 0.6f),
            new ClipSpec("SFX_UI_Invalid", ActionDefaultsPath, "UI Invalid", "Assets/Audio/Fantasy UI SFX - Lite Edition/Interface 6-5.wav", 0.55f),
            new ClipSpec("SFX_Inventory_Open", ActionDefaultsPath, "Inventory Open", "Assets/Audio/Fantasy UI SFX - Lite Edition/Bag Handle 1-1.wav", 0.65f),
            new ClipSpec("SFX_Inventory_Close", ActionDefaultsPath, "Inventory Close", "Assets/Audio/Fantasy UI SFX - Lite Edition/Bag Handle 1-5.wav", 0.65f),
            new ClipSpec("SFX_Inventory_Take", ActionDefaultsPath, "Inventory Take", "Assets/Audio/Fantasy UI SFX - Lite Edition/Objects Small 1-1.wav", 0.65f),
            new ClipSpec("SFX_Inventory_Deposit", ActionDefaultsPath, "Inventory Deposit", "Assets/Audio/Fantasy UI SFX - Lite Edition/Bag Handle 2-1.wav", 0.65f),
            new ClipSpec("SFX_Inventory_Drop", ActionDefaultsPath, "Inventory Drop", "Assets/Audio/Fantasy UI SFX - Lite Edition/Objects Small 1-3.wav", 0.65f),
            new ClipSpec("SFX_Inventory_PlaceStart", ActionDefaultsPath, "Inventory Place Start", "Assets/Audio/Fantasy UI SFX - Lite Edition/Card Place 1-1.wav", 0.65f),
            new ClipSpec("SFX_Inventory_PlaceConfirm", ActionDefaultsPath, "Inventory Place Confirm", "Assets/Audio/Fantasy UI SFX - Lite Edition/Card Place 1-5.wav", 0.65f),
            new ClipSpec("SFX_Inventory_PlaceCancel", ActionDefaultsPath, "Inventory Place Cancel", "Assets/Audio/Fantasy UI SFX - Lite Edition/Interface 6-5.wav", 0.55f),
            new ClipSpec("SFX_Inventory_Use", ActionDefaultsPath, "Inventory Use", "Assets/Audio/Fantasy UI SFX - Lite Edition/Potion Item 1-1.wav", 0.7f),
            new ClipSpec("SFX_Inventory_Break", ActionDefaultsPath, "Inventory Break", "Assets/Audio/Sounds/[SFX] BrokenGlass 1.mp3", 0.75f),
            new ClipSpec("SFX_Inventory_Unlock", ActionDefaultsPath, "Inventory Unlock", "Assets/Audio/Fantasy UI SFX - Lite Edition/Key & Lock 1-1.wav", 0.65f),
            new ClipSpec("SFX_Inventory_LockpickSuccess", ActionDefaultsPath, "Lockpick Success", "Assets/Audio/Fantasy UI SFX - Lite Edition/Key & Lock 1-2.wav", 0.65f),
            new ClipSpec("SFX_Inventory_LockpickFailure", ActionDefaultsPath, "Lockpick Failure", "Assets/Audio/Fantasy UI SFX - Lite Edition/Key & Lock 2-2.wav", 0.65f),
            new ClipSpec("SFX_Inventory_Trap", ActionDefaultsPath, "Inventory Trap", "Assets/Audio/Sounds/MetalicImpact.mp3", 0.75f),
            new ClipSpec("SFX_Inventory_ReadOpen", ActionDefaultsPath, "Read Open", "Assets/Audio/Fantasy UI SFX - Lite Edition/Book Handle 1-2.wav", 0.6f),
            new ClipSpec("SFX_Inventory_ReadPage", ActionDefaultsPath, "Read Page", "Assets/Audio/Fantasy UI SFX - Lite Edition/Book Page 1_1.wav", 0.55f),
            new ClipSpec("SFX_Inventory_ReadClose", ActionDefaultsPath, "Read Close", "Assets/Audio/Fantasy UI SFX - Lite Edition/Book Handle 1-4.wav", 0.6f),
            new ClipSpec("SFX_Beacon_ColorSelect", ActionDefaultsPath, "Beacon Color Select", "Assets/Audio/Fantasy UI SFX - Lite Edition/Magical Interface 1-1.wav", 0.6f),
            new ClipSpec("SFX_Build_PanelOpen", ActionDefaultsPath, "Build Panel Open", "Assets/Audio/Fantasy UI SFX - Lite Edition/Building Interface 1-1.wav", 0.65f),
            new ClipSpec("SFX_Build_PanelClose", ActionDefaultsPath, "Build Panel Close", "Assets/Audio/Fantasy UI SFX - Lite Edition/Building Interface 1-2.wav", 0.65f),
            new ClipSpec("SFX_Build_PlacementStart", ActionDefaultsPath, "Build Placement Start", "Assets/Audio/Fantasy UI SFX - Lite Edition/Building Interface 2-1.wav", 0.7f),
            new ClipSpec("SFX_Build_Complete", ActionDefaultsPath, "Build Complete", "Assets/Audio/Fantasy UI SFX - Lite Edition/Nail Wood 1-1.wav", 0.75f),
            new ClipSpec("SFX_Build_Upgrade", ActionDefaultsPath, "Build Upgrade", "Assets/Audio/Fantasy UI SFX - Lite Edition/Blacksmith 1-1.wav", 0.75f),
            new ClipSpec("SFX_Craft_Success", ActionDefaultsPath, "Craft Success", "Assets/Audio/Fantasy UI SFX - Lite Edition/Blacksmithing 2-1.wav", 0.75f),
            new ClipSpec("SFX_Craft_Failure", ActionDefaultsPath, "Craft Failure", "Assets/Audio/Fantasy UI SFX - Lite Edition/Interface 6-5.wav", 0.55f),
            new ClipSpec("SFX_Character_Damage", ActionDefaultsPath, "Character Damage", "Assets/Audio/Sounds/Lucian_Hurt.mp3", 0.8f),
            new ClipSpec("SFX_Character_Heal", ActionDefaultsPath, "Character Heal", "Assets/Audio/Fantasy UI SFX - Lite Edition/Magical Texture Chimes 1-1.wav", 0.7f),
            new ClipSpec("SFX_Character_Death", ActionDefaultsPath, "Character Death", "Assets/Audio/Sounds/Heroic Demise (New).mp3", 0.75f),
            new ClipSpec("SFX_Character_Jump", ActionDefaultsPath, "Character Jump", "Assets/Audio/Sounds/Jump.mp3", 0.7f),
            new ClipSpec("SFX_Character_Land", ActionDefaultsPath, "Character Land", "Assets/Audio/Fantasy UI SFX - Lite Edition/Wood Impact 02.wav", 0.7f),
            new ClipSpec("SFX_Flame_Toggle", ActionDefaultsPath, "Flame Toggle", "Assets/Audio/Sounds/A classer/[Sound] CursorFlames.mp3", 0.55f),
            new ClipSpec("SFX_Ladder_Use", ActionDefaultsPath, "Ladder Use", "Assets/Audio/Sounds/SFx Pas sur ponton -1.mp3", 0.6f),
            new ClipSpec("SFX_Teleport", ActionDefaultsPath, "Teleport", "Assets/Audio/Sounds/[SFX][TP].mp3", 0.7f),
            new ClipSpec("SFX_ReturnHome", ActionDefaultsPath, "Return Home", "Assets/Audio/Sounds/A classer/T\u00e9l\u00e9porteur en arriv\u00e9e.mp3", 0.7f),
            new ClipSpec("SFX_Labyrinth_Start", ActionDefaultsPath, "Labyrinth Start", "Assets/Audio/Sounds/DoorGate.mp3", 0.7f),
            new ClipSpec("SFX_SkillCheck_Success", ActionDefaultsPath, "Skill Check Success", "Assets/Audio/Sounds/Achievement Sound Effect.mp3", 0.7f),
            new ClipSpec("SFX_SkillCheck_Failure", ActionDefaultsPath, "Skill Check Failure", "Assets/Audio/Sounds/Annulation curseur.mp3", 0.55f),
            new ClipSpec("SFX_Combat_Attack", ActionDefaultsPath, "Combat Attack", "Assets/Audio/Sounds/SwordBlade.mp3", 0.75f),
            new ClipSpec("SFX_Combat_Hit", ActionDefaultsPath, "Combat Hit", "Assets/Audio/Sounds/[Sound] Sword Hit.mp3", 0.85f),
            new ClipSpec("SFX_Combat_Turn", ActionDefaultsPath, "Combat Turn", "Assets/Audio/Sounds/Curseur.mp3", 0.5f),
            new ClipSpec("SFX_Combat_Victory", ActionDefaultsPath, "Combat Victory", "Assets/Audio/Sounds/Achievement Sound Effect.mp3", 0.75f),
            new ClipSpec("SFX_Combat_Defeat", ActionDefaultsPath, "Combat Defeat", "Assets/Audio/Sounds/Heroic Demise (New).mp3", 0.75f),
            new ClipSpec("SFX_Combat_TimeSlow", ActionDefaultsPath, "Combat Time Slow", "Assets/Audio/Sounds/[Sound] Slow Motion Sound Effect.mp3", 0.65f),
            new ClipSpec("SFX_Combat_TimeResume", ActionDefaultsPath, "Combat Time Resume", "Assets/Audio/Sounds/Short Slow Motion Sound Effec - Out.mp3", 0.65f),
            new ClipSpec("SFX_Puzzle_Success", ActionDefaultsPath, "Puzzle Success", "Assets/Audio/Sounds/Achievement Sound Effect.mp3", 0.75f),
            new ClipSpec("SFX_Puzzle_Failure", ActionDefaultsPath, "Puzzle Failure", "Assets/Audio/Sounds/Annulation curseur.mp3", 0.55f),
            new ClipSpec("SFX_Destructible_Destroy", ActionDefaultsPath, "Destructible Destroy", "Assets/Audio/Sounds/[SFX] BrokenGlass 2.mp3", 0.8f),
            new ClipSpec("SFX_UI_Navigate", BindingClipsPath, "UI Navigate", "Assets/Audio/Sounds/Curseur.mp3", 0.5f),
            new ClipSpec("SFX_Door_Open", BindingClipsPath, "Door Open", "Assets/Audio/Sounds/DoorCreak.mp3", 0.65f),
            new ClipSpec("SFX_Door_Close", BindingClipsPath, "Door Close", "Assets/Audio/Sounds/DoorGate.mp3", 0.65f),
            new ClipSpec("AudioClip_DoorLocked", ActionDefaultsPath, "Door Locked", "Assets/Audio/Fantasy UI SFX - Lite Edition/Key & Lock 2-2.wav", 0.65f),
            new ClipSpec("SFX_Door_Locked", BindingClipsPath, "Door Locked", "Assets/Audio/Fantasy UI SFX - Lite Edition/Key & Lock 2-2.wav", 0.65f),
            new ClipSpec("SFX_Chest_Open", BindingClipsPath, "Chest Open", "Assets/Audio/Sounds/SFx Ouverture coffre.mp3", 0.7f),
            new ClipSpec("SFX_Lever_Activate", BindingClipsPath, "Lever Activate", "Assets/Audio/Sounds/[Sound] Draw Sword.mp3", 0.65f),
            new ClipSpec("SFX_Lever_Deactivate", BindingClipsPath, "Lever Deactivate", "Assets/Audio/Sounds/[Sound] Sword Sheathead_1.mp3", 0.65f),
            new ClipSpec("SFX_World_Reward", BindingClipsPath, "World Reward", "Assets/Audio/Sounds/Achievement Sound Effect.mp3", 0.75f),
            new ClipSpec("SFX_World_KnowledgeUnlock", BindingClipsPath, "Knowledge Unlock", "Assets/Audio/Fantasy UI SFX - Lite Edition/Magical Texture Chimes 1-1.wav", 0.7f),
            new ClipSpec("SFX_World_Echo", BindingClipsPath, "World Echo", "Assets/Audio/Fantasy UI SFX - Lite Edition/Magical Interface 10-1.wav", 0.55f),
            new ClipSpec("SFX_Combat_Lock", BindingClipsPath, "Combat Lock", "Assets/Audio/Fantasy UI SFX - Lite Edition/Magical Interface 10-1.wav", 0.55f),
            new ClipSpec("SFX_Combat_Dash", BindingClipsPath, "Combat Dash", "Assets/Audio/Sounds/[SFX]DashEffect.mp3", 0.7f),
            new ClipSpec("SFX_Combat_BowRelease", BindingClipsPath, "Combat Bow Release", "Assets/Audio/Fantasy UI SFX - Lite Edition/Arrow & Bow 1-2.wav", 0.7f),
            new ClipSpec("SFX_Combat_EnemyWarning", BindingClipsPath, "Combat Enemy Warning", "Assets/Audio/Fantasy UI SFX - Lite Edition/Magical Interface 3-3.wav", 0.65f),
            new ClipSpec("SFX_Combat_PerfectWindow", BindingClipsPath, "Combat Perfect Window", "Assets/Audio/Sounds/ShieldOnSound.mp3", 0.7f),
            new ClipSpec("SFX_Combat_ReactionSuccess", BindingClipsPath, "Combat Reaction Success", "Assets/Audio/Sounds/ShieldHitSound.mp3", 0.8f),
            new ClipSpec("SFX_Combat_GuardStart", BindingClipsPath, "Combat Guard Start", "Assets/Audio/Sounds/ShieldOnSound.mp3", 0.7f),
            new ClipSpec("SFX_Combat_GuardedImpact", BindingClipsPath, "Combat Guarded Impact", "Assets/Audio/Sounds/ShieldHitSound.mp3", 0.8f),
            new ClipSpec("SFX_Footstep_Castle_1", BindingClipsPath, "Footstep Castle 1", "Assets/Audio/Sounds/[SFX][Pas] Marche dans un couloir.mp3", 0.7f),
            new ClipSpec("SFX_Footstep_Castle_2", BindingClipsPath, "Footstep Castle 2", "Assets/Audio/Sounds/[SFX][Pas] Marche en chaussures de ville_1.mp3", 0.7f),
            new ClipSpec("SFX_Footstep_Castle_3", BindingClipsPath, "Footstep Castle 3", "Assets/Audio/Sounds/[SFX][Pas] Marche en chaussures de ville_2.mp3", 0.7f),
            new ClipSpec("SFX_Footstep_Wood_1", BindingClipsPath, "Footstep Wood 1", "Assets/Audio/Sounds/SFx Pas sur ponton -1.mp3", 0.7f),
            new ClipSpec("SFX_Footstep_Wood_2", BindingClipsPath, "Footstep Wood 2", "Assets/Audio/Sounds/SFx Pas sur ponton -2.mp3", 0.7f)
        };

        Dictionary<string, AudioClipSO> clips = new Dictionary<string, AudioClipSO>(StringComparer.Ordinal);
        for (int i = 0; i < specs.Length; i++)
        {
            ClipSpec spec = specs[i];
            clips.Add(spec.key, CreateOrUpdateClip(spec));
        }

        clips.Add("SwordWhoosh", LoadRequiredClip("Assets/Audio/AudioClips/AudioClip_SFX_SwordWhoosh.asset"));
        clips.Add("SwordImpact", LoadRequiredClip("Assets/Audio/AudioClips/AudioClip_SFX_SwordImpact.asset"));
        clips.Add("LightCharge", LoadRequiredClip("Assets/Audio/AudioClips/AudioClip_SFX_LightCharge.asset"));
        clips.Add("LightImpact", LoadRequiredClip("Assets/Audio/AudioClips/AudioClip_SFX_LightImpact.asset"));
        clips.Add("JumpKickImpact", LoadRequiredClip("Assets/Audio/AudioClips/AudioClip_SFX_JumpKickImpact.asset"));
        return clips;
    }

    private static AudioClipSO CreateOrUpdateClip(ClipSpec spec)
    {
        string path = spec.directory + "/" + spec.key + ".asset";
        AudioClipSO clip = AssetDatabase.LoadAssetAtPath<AudioClipSO>(path);
        if (clip == null)
        {
            clip = ScriptableObject.CreateInstance<AudioClipSO>();
            AssetDatabase.CreateAsset(clip, path);
        }

        AudioClip source = AssetDatabase.LoadAssetAtPath<AudioClip>(spec.sourcePath);
        if (source == null)
        {
            throw new FileNotFoundException("Missing source audio clip.", spec.sourcePath);
        }

        clip.title = spec.title;
        clip.composer = string.Empty;
        clip.audioClip = source;
        clip.volume = Mathf.Clamp01(spec.volume);
        clip.loop = false;
        clip.affectedByTimeScale = false;
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AudioClipSO LoadRequiredClip(string path)
    {
        AudioClipSO clip = AssetDatabase.LoadAssetAtPath<AudioClipSO>(path);
        if (clip == null || clip.audioClip == null)
        {
            throw new FileNotFoundException("Missing configured AudioClipSO.", path);
        }

        clip.loop = false;
        clip.affectedByTimeScale = false;
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void BindActionLibrary(Dictionary<string, AudioClipSO> clips)
    {
        ActionAudioLibrarySO library = AssetDatabase.LoadAssetAtPath<ActionAudioLibrarySO>(ActionLibraryPath);
        if (library == null)
        {
            throw new FileNotFoundException("Action audio library is missing.", ActionLibraryPath);
        }

        Dictionary<string, string> bindings = new Dictionary<string, string>
        {
            { "uiOpen", "SFX_UI_Open" }, { "uiClose", "SFX_UI_Close" }, { "uiConfirm", "SFX_UI_Confirm" }, { "uiCancel", "SFX_UI_Cancel" }, { "uiInvalid", "SFX_UI_Invalid" },
            { "inventoryOpen", "SFX_Inventory_Open" }, { "inventoryClose", "SFX_Inventory_Close" }, { "inventoryTake", "SFX_Inventory_Take" }, { "inventoryDeposit", "SFX_Inventory_Deposit" },
            { "inventoryDrop", "SFX_Inventory_Drop" }, { "inventoryPlaceStart", "SFX_Inventory_PlaceStart" }, { "inventoryPlaceConfirm", "SFX_Inventory_PlaceConfirm" },
            { "inventoryPlaceCancel", "SFX_Inventory_PlaceCancel" }, { "inventoryUse", "SFX_Inventory_Use" }, { "inventoryBreak", "SFX_Inventory_Break" },
            { "inventoryUnlock", "SFX_Inventory_Unlock" }, { "inventoryLockpickSuccess", "SFX_Inventory_LockpickSuccess" }, { "inventoryLockpickFailure", "SFX_Inventory_LockpickFailure" },
            { "inventoryTrap", "SFX_Inventory_Trap" }, { "inventoryReadOpen", "SFX_Inventory_ReadOpen" }, { "inventoryReadPage", "SFX_Inventory_ReadPage" },
            { "inventoryReadClose", "SFX_Inventory_ReadClose" }, { "beaconColorSelect", "SFX_Beacon_ColorSelect" }, { "buildPanelOpen", "SFX_Build_PanelOpen" },
            { "buildPanelClose", "SFX_Build_PanelClose" }, { "buildPlacementStart", "SFX_Build_PlacementStart" }, { "buildComplete", "SFX_Build_Complete" },
            { "buildUpgrade", "SFX_Build_Upgrade" }, { "craftSuccess", "SFX_Craft_Success" }, { "craftFailure", "SFX_Craft_Failure" },
            { "characterDamage", "SFX_Character_Damage" }, { "characterHeal", "SFX_Character_Heal" }, { "characterDeath", "SFX_Character_Death" },
            { "characterJump", "SFX_Character_Jump" }, { "characterLand", "SFX_Character_Land" }, { "flameToggle", "SFX_Flame_Toggle" },
            { "ladderUse", "SFX_Ladder_Use" }, { "teleport", "SFX_Teleport" }, { "returnHome", "SFX_ReturnHome" }, { "labyrinthStart", "SFX_Labyrinth_Start" },
            { "skillCheckSuccess", "SFX_SkillCheck_Success" }, { "skillCheckFailure", "SFX_SkillCheck_Failure" }, { "combatAttack", "SFX_Combat_Attack" },
            { "combatHit", "SFX_Combat_Hit" }, { "combatTurn", "SFX_Combat_Turn" }, { "combatVictory", "SFX_Combat_Victory" },
            { "combatDefeat", "SFX_Combat_Defeat" }, { "combatTimeSlow", "SFX_Combat_TimeSlow" }, { "combatTimeResume", "SFX_Combat_TimeResume" },
            { "puzzleSuccess", "SFX_Puzzle_Success" }, { "puzzleFailure", "SFX_Puzzle_Failure" }, { "destructibleDestroy", "SFX_Destructible_Destroy" }
        };

        SerializedObject serialized = new SerializedObject(library);
        foreach (KeyValuePair<string, string> binding in bindings)
        {
            SetReference(serialized, binding.Key, clips[binding.Value]);
        }

        Apply(serialized, library);
    }

    private static void BindSurfaces(Dictionary<string, AudioClipSO> clips)
    {
        BindSurface("Assets/Environment/0_Script/Surface/CastleFloor.asset", "castle", "Castle", clips["SFX_Footstep_Castle_1"], clips["SFX_Footstep_Castle_2"], clips["SFX_Footstep_Castle_3"]);
        BindSurface("Assets/Environment/0_Script/Surface/WoodSurface.asset", "wood", "Wood", clips["SFX_Footstep_Wood_1"], clips["SFX_Footstep_Wood_2"]);
    }

    private static void BindSurface(string path, string surfaceId, string displayName, params AudioClipSO[] footstepClips)
    {
        SurfaceDefinition surface = AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(path);
        if (surface == null)
        {
            throw new FileNotFoundException("Surface definition is missing.", path);
        }

        SerializedObject serialized = new SerializedObject(surface);
        serialized.FindProperty("surfaceId").stringValue = surfaceId;
        serialized.FindProperty("displayName").stringValue = displayName;
        SetReferenceArray(serialized, "footstepClips", footstepClips);
        Apply(serialized, surface);
    }

    private static void BindSkillAssets(Dictionary<string, AudioClipSO> clips)
    {
        HashSet<string> guids = new HashSet<string>(AssetDatabase.FindAssets("t:SkillSO"));
        guids.UnionWith(AssetDatabase.FindAssets("t:BasicSkillsSO"));
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsOwnedPath(path))
            {
                continue;
            }

            SkillSO skill = AssetDatabase.LoadAssetAtPath<SkillSO>(path);
            if (skill == null)
            {
                continue;
            }

            AudioClipSO playerStart = clips["SwordWhoosh"];
            AudioClipSO enemyStart = clips["SwordWhoosh"];
            AudioClipSO additionalImpact = null;
            AudioClipSO[] cueAudio = { clips["SwordImpact"] };

            if (path.IndexOf("/Skill_1_Eclair.asset", StringComparison.Ordinal) >= 0)
            {
                playerStart = clips["LightCharge"];
                enemyStart = clips["LightCharge"];
                cueAudio = new[] { (AudioClipSO)null, clips["LightImpact"] };
            }
            else if (path.IndexOf("/Skill_2_", StringComparison.Ordinal) >= 0)
            {
                playerStart = clips["LightCharge"];
                enemyStart = clips["SFX_Combat_BowRelease"];
                additionalImpact = clips["LightImpact"];
                cueAudio = new[] { (AudioClipSO)null, clips["SFX_Combat_BowRelease"] };
            }
            else if (path.IndexOf("/Skill_3_Entaille.asset", StringComparison.Ordinal) >= 0)
            {
                playerStart = clips["SFX_Combat_Dash"];
                cueAudio = new[] { clips["SwordWhoosh"], clips["SwordImpact"] };
            }
            else if (path.IndexOf("/Skill_4_JumpKick_Rupture.asset", StringComparison.Ordinal) >= 0)
            {
                playerStart = clips["SFX_Character_Jump"];
                enemyStart = clips["SFX_Character_Jump"];
                cueAudio = new[] { clips["LightImpact"], clips["JumpKickImpact"] };
            }

            SerializedObject serialized = new SerializedObject(skill);
            SetReference(serialized, "playerAttackSfx", playerStart);
            SetReference(serialized, "enemyAttackSfx", enemyStart);
            SetReference(serialized, "impactFeedback.additionalImpactAudio", additionalImpact);
            SetReference(serialized, "reactionTelegraph.anticipationAudio", clips["SFX_Combat_EnemyWarning"]);
            SetReference(serialized, "reactionTelegraph.perfectWindowAudio", clips["SFX_Combat_PerfectWindow"]);
            SetReference(serialized, "reactionTelegraph.successfulReactionAudio", clips["SFX_Combat_ReactionSuccess"]);
            SetReference(serialized, "combatWarning.warningAudio", clips["SFX_Combat_EnemyWarning"]);
            SetCueAudio(serialized, cueAudio);
            Apply(serialized, skill);
        }
    }

    private static void BindLightSkillAssets(Dictionary<string, AudioClipSO> clips)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:LightSkillSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsOwnedPath(path))
            {
                continue;
            }

            LightSkillSO skill = AssetDatabase.LoadAssetAtPath<LightSkillSO>(path);
            if (skill == null)
            {
                continue;
            }

            AudioClipSO start = clips["SwordWhoosh"];
            AudioClipSO impulse = clips["SFX_Combat_Dash"];
            AudioClipSO impact = clips["SwordImpact"];
            if (path.IndexOf("Devastation", StringComparison.Ordinal) >= 0)
            {
                start = clips["LightCharge"];
                impulse = clips["SFX_Combat_BowRelease"];
                impact = clips["LightImpact"];
            }
            else if (path.IndexOf("Envol", StringComparison.Ordinal) >= 0)
            {
                start = clips["SFX_Character_Jump"];
                impulse = clips["SFX_Combat_Dash"];
                impact = clips["JumpKickImpact"];
            }

            SerializedObject serialized = new SerializedObject(skill);
            SetReference(serialized, "startSfx", start);
            SetReference(serialized, "impulseSfx", impulse);
            SetReference(serialized, "impactSfx", impact);
            Apply(serialized, skill);
        }
    }

    private static void BindCounterAndAttackAssets(Dictionary<string, AudioClipSO> clips)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:CounterSkillSO"))
        {
            CounterSkillSO counter = AssetDatabase.LoadAssetAtPath<CounterSkillSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (counter == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(counter);
            SetReference(serialized, "startSfx", clips["SFX_Combat_GuardStart"]);
            SetReference(serialized, "impactSfx", clips["SFX_Combat_GuardedImpact"]);
            Apply(serialized, counter);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:CombatAttackDefinition"))
        {
            CombatAttackDefinition attack = AssetDatabase.LoadAssetAtPath<CombatAttackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (attack == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(attack);
            SetReference(serialized, "impactSfx", clips["SwordImpact"]);
            Apply(serialized, attack);
        }
    }

    private static void BindStandaloneEffectAssets(Dictionary<string, AudioClipSO> clips)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:EchoPassiveEffect"))
        {
            EchoPassiveEffect effect = AssetDatabase.LoadAssetAtPath<EchoPassiveEffect>(AssetDatabase.GUIDToAssetPath(guid));
            if (effect == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(effect);
            SetReference(serialized, "audioClip", clips["SFX_World_Echo"]);
            Apply(serialized, effect);
        }
    }

    private static void BindPrefabs(Dictionary<string, AudioClipSO> clips)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsOwnedPath(path))
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (BindComponents(root, clips))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void BindScenes(Dictionary<string, AudioClipSO> clips)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/Scenes/", StringComparison.Ordinal))
            {
                continue;
            }

            Scene loadedScene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = loadedScene.IsValid() && loadedScene.isLoaded;
            Scene scene = wasLoaded ? loadedScene : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    changed |= BindComponents(roots[i], clips);
                }

                if (changed && !wasLoaded)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }

    private static bool BindComponents(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        changed |= BindAudioManagers(root, clips);
        changed |= BindDoors(root, clips);
        changed |= BindLevers(root, clips);
        changed |= BindTwoLeverPuzzles(root, clips);
        changed |= BindMuninRewards(root, clips);
        changed |= BindKnowledgeManagers(root, clips);
        changed |= BindCursors(root, clips);
        changed |= BindMainMenus(root, clips);
        changed |= BindCombatLocks(root, clips);
        changed |= BindCounterControllers(root, clips);
        changed |= BindDestructibles(root, clips);
        return changed;
    }

    private static bool BindAudioManagers(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        ActionAudioLibrarySO library = AssetDatabase.LoadAssetAtPath<ActionAudioLibrarySO>(ActionLibraryPath);
        foreach (AudioManager manager in root.GetComponentsInChildren<AudioManager>(true))
        {
            SerializedObject serialized = new SerializedObject(manager);
            changed |= SetReference(serialized, "actionAudioLibrary", library);
            Apply(serialized, manager);
        }

        return changed;
    }

    private static bool BindDoors(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (Door door in root.GetComponentsInChildren<Door>(true))
        {
            SerializedObject serialized = new SerializedObject(door);
            changed |= SetReference(serialized, "openSfx", clips["SFX_Door_Open"]);
            changed |= SetReference(serialized, "closeSfx", clips["SFX_Door_Close"]);
            changed |= SetReference(serialized, "lockedSfx", clips["AudioClip_DoorLocked"]);
            Apply(serialized, door);
        }

        return changed;
    }

    private static bool BindLevers(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (Lever lever in root.GetComponentsInChildren<Lever>(true))
        {
            SerializedObject serialized = new SerializedObject(lever);
            changed |= SetReference(serialized, "activateSfx", clips["SFX_Lever_Activate"]);
            changed |= SetReference(serialized, "deactivateSfx", clips["SFX_Lever_Deactivate"]);
            Apply(serialized, lever);
        }

        return changed;
    }

    private static bool BindTwoLeverPuzzles(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (TwoLeverPuzzle puzzle in root.GetComponentsInChildren<TwoLeverPuzzle>(true))
        {
            SerializedObject serialized = new SerializedObject(puzzle);
            changed |= SetReference(serialized, "successSfx", clips["SFX_Puzzle_Success"]);
            Apply(serialized, puzzle);
        }

        return changed;
    }

    private static bool BindMuninRewards(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (MuninChargeReward reward in root.GetComponentsInChildren<MuninChargeReward>(true))
        {
            SerializedObject serialized = new SerializedObject(reward);
            changed |= SetReference(serialized, "rewardSfx", clips["SFX_World_Reward"]);
            Apply(serialized, reward);
        }

        return changed;
    }

    private static bool BindKnowledgeManagers(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (KnowledgeManager knowledge in root.GetComponentsInChildren<KnowledgeManager>(true))
        {
            SerializedObject serialized = new SerializedObject(knowledge);
            changed |= SetReference(serialized, "unlockSfx", clips["SFX_World_KnowledgeUnlock"]);
            Apply(serialized, knowledge);
        }

        return changed;
    }

    private static bool BindCursors(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (CursorController cursor in root.GetComponentsInChildren<CursorController>(true))
        {
            SerializedObject serialized = new SerializedObject(cursor);
            changed |= SetReference(serialized, "moveSfx", clips["SFX_UI_Navigate"]);
            Apply(serialized, cursor);
        }

        return changed;
    }

    private static bool BindMainMenus(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (MainMenuController menu in root.GetComponentsInChildren<MainMenuController>(true))
        {
            SerializedObject serialized = new SerializedObject(menu);
            changed |= SetReference(serialized, "menuButtonSfx", clips["SFX_UI_Navigate"]);
            changed |= SetReferenceIfNull(serialized, "titleCardProceedSfx", clips["SFX_UI_Confirm"]);
            Apply(serialized, menu);
        }

        return changed;
    }

    private static bool BindCombatLocks(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (CombatLockIndicator indicator in root.GetComponentsInChildren<CombatLockIndicator>(true))
        {
            SerializedObject serialized = new SerializedObject(indicator);
            changed |= SetReference(serialized, "lockSfx", clips["SFX_Combat_Lock"]);
            Apply(serialized, indicator);
        }

        return changed;
    }

    private static bool BindCounterControllers(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (CounterSkillCombatController controller in root.GetComponentsInChildren<CounterSkillCombatController>(true))
        {
            SerializedObject serialized = new SerializedObject(controller);
            changed |= SetReference(serialized, "guardStartAudio", clips["SFX_Combat_GuardStart"]);
            changed |= SetReference(serialized, "guardedImpactAudio", clips["SFX_Combat_GuardedImpact"]);
            Apply(serialized, controller);
        }

        return changed;
    }

    private static bool BindDestructibles(GameObject root, Dictionary<string, AudioClipSO> clips)
    {
        bool changed = false;
        foreach (DestructibleObject destructible in root.GetComponentsInChildren<DestructibleObject>(true))
        {
            SerializedObject serialized = new SerializedObject(destructible);
            SerializedProperty legacy = serialized.FindProperty("legacyDestroySound");
            AudioClip legacyClip = legacy != null ? legacy.objectReferenceValue as AudioClip : null;
            AudioClipSO destroySfx = legacyClip != null ? CreateDestructibleClip(legacyClip) : clips["SFX_Destructible_Destroy"];
            changed |= SetReference(serialized, "destroySfx", destroySfx);
            if (legacy != null && legacy.objectReferenceValue != null)
            {
                legacy.objectReferenceValue = null;
                changed = true;
            }

            Apply(serialized, destructible);
        }

        return changed;
    }

    private static AudioClipSO CreateDestructibleClip(AudioClip source)
    {
        string safeName = SanitizeAssetName(source.name);
        string path = BindingClipsPath + "/SFX_Destructible_" + safeName + ".asset";
        AudioClipSO clip = AssetDatabase.LoadAssetAtPath<AudioClipSO>(path);
        if (clip == null)
        {
            clip = ScriptableObject.CreateInstance<AudioClipSO>();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.title = "Destructible " + source.name;
        clip.composer = string.Empty;
        clip.audioClip = source;
        clip.volume = 0.8f;
        clip.loop = false;
        clip.affectedByTimeScale = false;
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetCueAudio(SerializedObject serialized, AudioClipSO[] clips)
    {
        SerializedProperty cues = serialized.FindProperty("vfxCues");
        if (cues == null)
        {
            return;
        }

        int count = Mathf.Min(cues.arraySize, clips.Length);
        for (int i = 0; i < count; i++)
        {
            SerializedProperty audio = cues.GetArrayElementAtIndex(i).FindPropertyRelative("audioClip");
            if (audio != null)
            {
                audio.objectReferenceValue = clips[i];
            }
        }
    }

    private static bool SetReference(SerializedObject serialized, string propertyPath, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyPath);
        if (property == null)
        {
            throw new MissingFieldException(serialized.targetObject.GetType().Name, propertyPath);
        }

        if (property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetReferenceIfNull(SerializedObject serialized, string propertyPath, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyPath);
        if (property == null)
        {
            throw new MissingFieldException(serialized.targetObject.GetType().Name, propertyPath);
        }

        if (property.objectReferenceValue != null)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static void SetReferenceArray(SerializedObject serialized, string propertyPath, AudioClipSO[] values)
    {
        SerializedProperty property = serialized.FindProperty(propertyPath);
        if (property == null)
        {
            throw new MissingFieldException(serialized.targetObject.GetType().Name, propertyPath);
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void Apply(SerializedObject serialized, UnityEngine.Object target)
    {
        if (serialized.ApplyModifiedPropertiesWithoutUndo())
        {
            EditorUtility.SetDirty(target);
        }
    }

    private static void ValidateInternal(Dictionary<string, AudioClipSO> clips)
    {
        ActionAudioLibrarySO library = AssetDatabase.LoadAssetAtPath<ActionAudioLibrarySO>(ActionLibraryPath);
        if (library == null)
        {
            throw new FileNotFoundException("Action audio library is missing.", ActionLibraryPath);
        }

        foreach (ActionAudioCue cue in Enum.GetValues(typeof(ActionAudioCue)))
        {
            if (cue == ActionAudioCue.None)
            {
                continue;
            }

            ValidateClip(library.Resolve(cue), "ActionAudioCue." + cue);
        }

        foreach (KeyValuePair<string, AudioClipSO> pair in clips)
        {
            ValidateClip(pair.Value, pair.Key);
        }

        ValidateSurface("Assets/Environment/0_Script/Surface/CastleFloor.asset");
        ValidateSurface("Assets/Environment/0_Script/Surface/WoodSurface.asset");
    }

    private static void ValidateSurface(string path)
    {
        SurfaceDefinition surface = AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(path);
        if (surface == null || !surface.HasFootstepClips)
        {
            throw new InvalidOperationException("Surface has no configured footstep AudioClipSO: " + path);
        }

        AudioClipSO[] footstepClips = surface.FootstepClips;
        for (int i = 0; i < footstepClips.Length; i++)
        {
            ValidateClip(footstepClips[i], path + " footstep " + i);
        }
    }

    private static void ValidateClip(AudioClipSO clip, string context)
    {
        if (clip == null || clip.audioClip == null)
        {
            throw new InvalidOperationException("Missing AudioClipSO binding: " + context);
        }

        if (clip.loop || clip.affectedByTimeScale)
        {
            throw new InvalidOperationException("Action AudioClipSO has invalid playback settings: " + context);
        }
    }

    private static bool IsOwnedPath(string path)
    {
        return path.StartsWith("Assets/", StringComparison.Ordinal) &&
               !path.StartsWith("Assets/0 - UnityPackages/", StringComparison.Ordinal) &&
               !path.StartsWith("Assets/Legacy/", StringComparison.Ordinal);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
        {
            throw new DirectoryNotFoundException("Parent folder is missing: " + parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private static string SanitizeAssetName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string result = value;
        for (int i = 0; i < invalid.Length; i++)
        {
            result = result.Replace(invalid[i], '_');
        }

        return result.Replace(' ', '_');
    }
}
