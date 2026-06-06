# Lucian CC5 Integration

## Summary

Lucian is a Reallusion / Character Creator character integrated from:

- Source folder: `Assets/Lucian_CC5_Embed`
- Source prefab: `Assets/Lucian_CC5_Embed/Prefabs/Lucian_CC5_Character_Model.prefab`
- HQ Unity prefab: `Assets/Lucian_CC5_Embed/Prefabs/Lucian_CC5_Unity_HQ.prefab`
- HQ material variants: `Assets/Lucian_CC5_Embed/Materials/Lucian_Unity_HQ`
- Render test scene: `Assets/Scenes/CharacterTests/Lucian_RenderTest.unity`
- Maintenance tool: `Assets/Editor/LucianCC5HqIntegration.cs`

The project render pipeline is HDRP. `ProjectSettings/GraphicsSettings.asset` points to:

- `Assets/Settings/HDRPDefaultResources/HDRenderPipelineAsset.asset`

Quality settings also use HDRP assets:

- `Assets/Settings/HDRP High Fidelity.asset`
- `Assets/Settings/HDRP Balanced.asset`
- `Assets/Settings/HDRP Performant.asset`

The global render pipeline was not changed.

## Source Export

The export appears to come from Reallusion Character Creator / CC5:

- `Lucian_CC5_Character_Model.Fbx`
- `Lucian_CC5_Character_Model_Motion.Fbx`
- `Lucian_CC5_Character_Model.json`
- `Lucian_CC5_Character_Model_ImportInfo.txt`

The import info contains:

- `logType=HighQuality`
- `generation=GameBase`
- `qualEyes=Parallax`
- `qualHair=TwoPass`
- `bakeCustomShaders=true`
- `rigOverride=Humanoid`

The FBX import keeps blendshapes enabled and uses a Humanoid avatar. The FBX also contains facial blendshape names such as brows, eyes, mouth, teeth and tongue shapes.

## Materials

Original Reallusion materials were not overwritten. The HQ integration duplicates them into:

`Assets/Lucian_CC5_Embed/Materials/Lucian_Unity_HQ`

The new prefab references the duplicated `_Unity_HQ` materials only. The original prefab and original material folder remain unchanged.

Material groups covered:

- Skin: `Ga_Skin_Head`, `Ga_Skin_Body`, `Ga_Skin_Arm`, `Ga_Skin_Leg`
- Hair: `Hair_Transparency`, `Hair1_Transparency`, `Hair2_Transparency`, `Hair3_Transparency`
- Eyebrows / lashes: `Female_Angled_*`, `Ga_Eyelash`, `Lash_*`
- Eyes / cornea: `Std_Eye_*`, `Std_Cornea_*`
- Teeth / tongue / nails
- Clothing: `M_Assassin_Skin1_Armor_*`

Some Reallusion hair, lash, eye, cornea and teeth materials reference custom shader GUIDs that are not present in this project. Those source materials can appear pink if they are assigned directly. The HQ variants keep the original materials untouched, but use an HDRP/Lit fallback when Unity cannot resolve a Reallusion shader. Hair and lash fallback materials keep their base color texture aliases and alpha clipping enabled.

`Player_Model_Lucian.prefab` is expected to reference the `_Unity_HQ` material variants, not the original Reallusion material folder.

## Texture Import Settings

Texture metadata under the Lucian source folders was adjusted for quality and correctness:

- Normal maps are kept as `Texture Type = Normal Map`.
- Data maps such as HDRP mask maps, metallic alpha, AO, flow/root/id/weight maps are linear (`sRGB OFF`).
- Color maps remain `sRGB ON`.
- Hair, lash, brow and transparency textures keep alpha from input and preserve alpha coverage in mipmaps.
- Standalone platform max texture size is set to 4096.
- Texture compression is set to high quality, with compression quality 100.

The actual source textures inspected are mostly 2048x2048, so the 4096 cap does not upscale them. It only prevents Unity from downscaling higher-resolution replacements during a future Reallusion update.

The generated report is:

`Assets/Lucian_CC5_Embed/Lucian_CC5_Unity_HQ_Report.txt`

## Rig And Animation

Observed FBX settings:

- Animation type: Humanoid
- Avatar setup: create from this model
- `importBlendShapes: 1`
- `meshCompression: 0`
- `maxBonesPerVertex: 4`
- `optimizeBones: 1`
- T-pose clip present

Do not convert the rig to Generic unless there is a clear gameplay reason. Do not disable blendshapes during reimport.

## Render Test

Use:

`Assets/Scenes/CharacterTests/Lucian_RenderTest.unity`

It contains:

- Lucian HQ prefab instance
- Camera
- Directional key light

After Unity imports the generated assets, use this scene to verify:

- Skin detail and smoothness
- Hair alpha and two-pass transparency
- Eye/cornea readability
- Clothing normal maps and roughness
- No pink/missing-shader materials
- No missing main textures

## Maintenance Workflow

When updating Lucian from Reallusion:

1. Export the new CC5 character into `Assets/Lucian_CC5_Embed` or a temporary sibling folder.
2. Keep the original texture names if possible, because the materials reference those assets by GUID after Unity import.
3. Open Unity and let the import finish.
4. Run `Tools/Lit/Characters/Lucian/Build CC5 HQ Integration`.
5. Open `Assets/Scenes/CharacterTests/Lucian_RenderTest.unity`.
6. Check skin, hair alpha, eyes, clothing normals and material shader status.
7. If the FBX was replaced, verify the Humanoid avatar and facial blendshapes in the Model Importer.

The build tool does not blindly reserialize existing HQ materials from the source materials. This avoids reintroducing missing Reallusion shader references after local Unity-side fixes. Delete or rename a specific `_Unity_HQ` material only when you intentionally want it regenerated from the source material.

## Known Limits

- Unity batchmode could not be run during this integration because another Unity instance had the project open.
- Several skin materials reference subsurface/thickness texture GUIDs that are not present as files in `Assets/Lucian_CC5_Embed`. The materials still keep their SSS/transmission numeric settings, but true map-driven thickness/SSS may be limited until those maps are exported or restored.
- Reallusion Auto Setup was not detected as an installed tool folder. The existing materials already contain HDRP-specific shader data, so no external package was installed.
- The scene and generated assets should be opened once in Unity so Unity can import new `.meta` files and surface any shader compatibility warnings in the Console.
