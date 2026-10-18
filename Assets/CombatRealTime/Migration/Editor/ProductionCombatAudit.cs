using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

/// <summary>Gameplay-rooted read-only audit. All caches are scoped to a single session.</summary>
public static class ProductionCombatAudit
{
    private static readonly Regex References = new Regex(@"\bguid:[ \t]*([a-fA-F0-9]{32})\b|\b(?:m_AssetGUID|m_AssetGuid):[ \t]*([a-fA-F0-9]{32})\b", RegexOptions.Compiled);
    private static readonly HashSet<string> TextTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        ".cs", ".asmdef", ".asmref", ".meta", ".asset", ".prefab", ".unity", ".controller", ".overrideController", ".playable", ".mat", ".anim", ".inputactions", ".json", ".yaml", ".yml"
    };
    // Explicit first-party policy. Reached vendor MonoScripts are also scanned, wherever located.
    private static readonly string[] GameCodeRoots = {
        "Assets/Scripts/", "Assets/CombatRealTime/", "Assets/Characters/", "Assets/Persistence/", "Assets/Narrative/", "Assets/Scenes/",
        "Assets/Gameplay/", "Assets/Core/", "Assets/UI/", "Assets/Camera/", "Assets/Environment/", "Assets/Interactive/", "Assets/Navigation/", "Assets/Timeline/", "Assets/Performance/"
    };

    [Serializable] public sealed class Cost
    {
        public int indexedAssets, assetIndexQueries = 1, roots, reachableAssets, authoringAssets, dependencyQueries, dependencyEdges, cacheHits, contentReads, filesHashed, warnings;
        public long bytesRead, bytesHashed, elapsedMilliseconds;
        public string policy = "gameplay-reachability-v3; exact GUID classification; bounded resource namespaces; per-session direct-edge/text/hash caches; no unreachable payload scanning/hashing";
    }
    private sealed class Content { public string hash, text, physical; public long length, ticks; }

    public sealed class Session
    {
        private readonly Stopwatch timer = Stopwatch.StartNew();
        private readonly Dictionary<string, Content> contents = new Dictionary<string, Content>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> guids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ProductionCombatMigration.Entry> notes = new List<ProductionCombatMigration.Entry>();
        private readonly List<ProductionCombatMigration.Entry> blockers = new List<ProductionCombatMigration.Entry>();
        private readonly string[] paths;
        private readonly HashSet<string> code = new HashSet<string>(StringComparer.Ordinal), scanned = new HashSet<string>(StringComparer.Ordinal), registries = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> resources = new Dictionary<string, List<string>>(StringComparer.Ordinal), addresses = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly List<string> loadSubjects = new List<string>();
        private readonly Dictionary<string, Type> resourceTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        private readonly Dictionary<string, Type> mainTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        private readonly string parentRevisionPath;
        private CombatReadinessEvidence.Snapshot snapshot;
        private int contentCacheHits;
        public readonly CombatReachabilityGraph Graph, Authoring;
        public readonly Cost cost = new Cost();

        public Session(string parentRevisionPath = null)
        {
            this.parentRevisionPath = parentRevisionPath ?? Argument("-combatParentRevision");
            paths = AssetDatabase.GetAllAssetPaths().Where(IsAsset).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            cost.indexedAssets = paths.Length;
            Graph = new CombatReachabilityGraph(Dependencies);
            Authoring = new CombatReachabilityGraph(Graph.Direct);
            foreach (string path in paths.Where(path => path.Contains("/Resources/") && !AssetDatabase.IsValidFolder(path))) AddIndex(resources, ResourceKey(path), path);
            foreach (string path in paths.Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) && path.Contains("/AddressableAssetsData/") && path.EndsWith(".asset", StringComparison.Ordinal)))
            {
                registries.Add(path); // Hash the registry, never recursively expand all its unrelated entry payloads.
                foreach (Match match in Regex.Matches(Read(path), @"m_GUID:[ \t]*([a-fA-F0-9]{32})\s*\r?\n[ \t]*m_Address:[ \t]*([^\r\n]+)"))
                    AddIndex(addresses, match.Groups[2].Value.Trim().Trim('"', '\''), ResolveGuid(match.Groups[1].Value));
            }
            try
            {
                foreach (string source in CompilationPipeline.GetAssemblies(AssembliesType.Player).SelectMany(assembly => assembly.sourceFiles))
                {
                    string path = Logical(source);
                    if (IsAsset(path)) code.Add(path); else Block("unmapped-runtime-source", source, "Cannot map compiler input to project/package source.");
                }
                if (code.Count == 0) Block("runtime-code-index-unavailable", "", "No player compilation source index; coverage cannot be inferred.");
            }
            catch (Exception exception) { Block("runtime-code-index-unavailable", "", exception.Message); }
            Root(ProductionCombatMigration.SessionPath, "gameplay-session");
            Root("Assets/Resources/CombatSkillKits/Default.asset", "production-roster-catalog");
            foreach (string name in new[] { "Lucian", "Vesta", "Soren", "Aurell" }) Root("Assets/Characters/1_Squad/" + name + "/" + name + ".asset", "production-roster");
            // These are distinct authored identities despite sharing the same world prefab.
            foreach (string identity in new[] { "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut_CharacterData.asset", "Assets/Characters/3_Enemy/Juggernaut/GiantJuggernaut.asset" })
                Root(identity, "distinct-production-giant-identity");
            foreach (var scene in EditorBuildSettings.scenes.Where(scene => scene.enabled)) Root(scene.path, "enabled-build-scene");
            foreach (string guid in AssetDatabase.FindAssets("t:ZoneManifest", new[] { "Assets" }))
            {
                string path = ResolveGuid(guid); Root(path, "zone-manifest");
                var zone = AssetDatabase.LoadAssetAtPath<ZoneManifest>(path);
                if (zone == null) { Block("unresolved-zone", path, "ZoneManifest cannot be loaded."); continue; }
                foreach (string name in new[] { zone.PrimarySceneName }.Concat(zone.LoadingSceneNames).Concat(zone.PostLoadingSceneNames))
                {
                    string[] matches = paths.Where(candidate => candidate.EndsWith(".unity", StringComparison.Ordinal) && Path.GetFileNameWithoutExtension(candidate) == name).ToArray();
                    if (matches.Length != 1) Block("unknown-zone-scene", path, "Missing/ambiguous runtime scene name: " + name);
                    foreach (string scene in matches) Root(scene, "zone-scene:" + path);
                }
            }
            // IsDefault is editor metadata, not proof a list is registered by a gameplay NetworkManager.
            // Only actual serialized manager/list references enter the closure. Runtime registrations remain explicit unknowns.
            foreach (string path in Graph.Reachable.Where(path => path.EndsWith(".asset", StringComparison.Ordinal)).ToArray())
                if (AssetDatabase.GetMainAssetTypeAtPath(path)?.Name == "NetworkPrefabsList") Root(path, "serialized-gameplay-network-prefabs-list");
            Authoring.AddRoot(ProductionCombatMigration.LabPath, "migration-authoring-input-only");
            ScanLoads();
        }

        private void ScanLoads()
        {
            while (true)
            {
                string[] pending = code.Where(path => !scanned.Contains(path) && (IsGameCode(path) || Graph.Reachable.Contains(path))).ToArray();
                if (pending.Length == 0) break;
                foreach (string path in pending)
                {
                    scanned.Add(path); string source = CombatInventoryReference.WithoutComments(Read(path));
                    foreach (Match match in CombatInventoryReference.Loads.Matches(source))
                    {
                        string subject = "load:" + path + ":" + (source.Take(match.Index).Count(value => value == '\n') + 1) + ":" + CombatReadinessEvidence.HashText(match.Value).Substring(0, 16);
                        loadSubjects.Add(subject);
                        string operation = match.Groups[1].Value;
                        string expression = match.Groups[2].Value.Trim();
                        bool known = CombatInventoryReference.TryConstant(source, expression, out string key);
                        bool prefix = !known && operation.StartsWith("Resources.", StringComparison.Ordinal) && CombatInventoryReference.TryResourcePrefix(expression, out key);
                        var targets = new List<string>();
                        if (!known && !prefix && operation.StartsWith("Resources.", StringComparison.Ordinal))
                        {
                            var choices = CombatInventoryReference.ConstantChoices(source, expression).ToList();
                            if (path == "Assets/Scripts/AudioManager.cs" && expression == "resourcePath" &&
                                source.Contains("defaultActionAudioLibraryResourcePath.Trim()") &&
                                CombatInventoryReference.TryConstant(source, "DefaultActionAudioLibraryResourcePath", out string defaultAudio))
                            {
                                choices.Add(defaultAudio);
                                foreach (string container in Graph.Reachable.Where(value => value.EndsWith(".unity", StringComparison.Ordinal) || value.EndsWith(".prefab", StringComparison.Ordinal)).ToArray())
                                    foreach (Match setting in Regex.Matches(Read(container), @"(?m)^[ \t]*defaultActionAudioLibraryResourcePath:[ \t]*([^\r\n]*)"))
                                    {
                                        string configured = setting.Groups[1].Value.Trim().Trim('"', '\'');
                                        if (configured.Length > 0) choices.Add(configured);
                                    }
                            }
                            foreach (string choice in choices.Distinct(StringComparer.Ordinal))
                            {
                                string[] found = resources.TryGetValue(choice, out var candidates) ? candidates.ToArray() : Array.Empty<string>();
                                Note("dynamic-reference-candidate", path, subject + "; discovered key=" + choice + "; candidates=" + string.Join(", ", found) + "; mutable expression remains unproven.");
                                targets.AddRange(found);
                            }
                        }
                        if (known || prefix)
                        {
                            if (operation.StartsWith("Resources.", StringComparison.Ordinal))
                            {
                                string typeName = match.Groups["type"].Value.Trim();
                                Type requested = ResourceType(typeName);
                                if (typeName.Length > 0 && requested == null) Block("unknown-resource-type", path, subject + "; cannot resolve generic type " + typeName);
                                else foreach (string resourceKey in CombatInventoryReference.ResourceCandidates(resources.Keys, key, prefix, operation == "Resources.LoadAll"))
                                {
                                    var candidates = resources[resourceKey].Where(candidate => MatchesResourceType(candidate, requested)).ToArray();
                                    targets.AddRange(candidates);
                                    if (candidates.Length > 1 && operation != "Resources.LoadAll") Block("ambiguous-runtime-key", path, subject + "; " + resourceKey + "; " + string.Join(", ", candidates));
                                }
                            }
                            else if (operation.StartsWith("Addressables.", StringComparison.Ordinal) && addresses.TryGetValue(key, out var found)) targets.AddRange(found);
                            if (!prefix && operation != "Resources.LoadAll" && targets.Count > 1) Block("ambiguous-runtime-key", path, subject + "; " + key);
                        }
                        Note("dynamic-reference", path, subject + "; expression=" + expression + "; resolution=" + (prefix ? "bounded-resource-namespace:" + key : known ? "literal-or-const:" + key : "unresolved") + "; candidates=" + string.Join(", ", targets) + "; inventory only, not proof every runtime request succeeds.");
                        if ((!known && !prefix) || targets.Count == 0 || targets.Any(string.IsNullOrEmpty)) Block("unknown-dynamic-reference", path, subject + "; expression=" + expression + "; key=" + key + "; no matching imported candidates or unbounded runtime key; no expand-everything fallback.");
                        foreach (string target in targets.Where(value => !string.IsNullOrEmpty(value))) Root(target, subject);
                    }
                    if (Regex.IsMatch(source, @"\.(?:LoadResourceLocationsAsync|LoadContentCatalogAsync|AddNetworkPrefab)\s*(?:<[^>]+>)?\s*\("))
                        Block("unknown-runtime-registry", path, "Dynamic label/catalog/network registration requires explicit runtime registry audit, not sample asset expansion.");
                }
            }
            if (code.Count > scanned.Count) Warn("unreached-library-code", "", (code.Count - scanned.Count) + " compiled vendor/library sources hashed, not interpreted as first-party loader call sites unless gameplay-reached; policy is explicit.");
        }

        private Type ResourceType(string name)
        {
            if (name.Length == 0 || name == "Object" || name == "UnityEngine.Object") return typeof(UnityEngine.Object);
            if (!resourceTypes.TryGetValue(name, out var type))
            {
                var found = TypeCache.GetTypesDerivedFrom<UnityEngine.Object>().Where(candidate => candidate.Name == name || candidate.FullName == name).ToArray();
                resourceTypes[name] = type = found.Length == 1 ? found[0] : null;
            }
            return type;
        }
        private bool MatchesResourceType(string path, Type requested)
        {
            if (!mainTypes.TryGetValue(path, out var main)) mainTypes[path] = main = AssetDatabase.GetMainAssetTypeAtPath(path);
            // Model sub-assets can be clips/meshes even when the main asset is a GameObject; retain those candidates.
            return main == null || requested.IsAssignableFrom(main) || main == typeof(GameObject) && (requested == typeof(AnimationClip) || requested == typeof(Mesh));
        }

        private IEnumerable<string> Dependencies(string path)
        {
            if (!Exists(path)) return Array.Empty<string>();
            var result = AssetDatabase.GetDependencies(path, false).Where(IsAsset).ToList();
            if (TextTypes.Contains(Path.GetExtension(path)) && !path.EndsWith(".cs", StringComparison.Ordinal))
                foreach (Match match in References.Matches(Read(path)))
                {
                    string guid = ReferenceGuid(match);
                    string target = ResolveGuid(guid);
                    if (IsAsset(target)) result.Add(target);
                }
            return result;
        }

        public void Collect(ProductionCombatMigration.Report report)
        {
            foreach (string path in Graph.Reachable.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!Exists(path)) { Block("unresolved-reachable-asset", path, "Gameplay root/dependency missing."); continue; }
                if (!TextTypes.Contains(Path.GetExtension(path)) || path.EndsWith(".cs", StringComparison.Ordinal)) continue;
                string text = Read(path);
                foreach (string guid in References.Matches(text).Cast<Match>().Select(ReferenceGuid).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    string target = ResolveGuid(guid);
                    string classification = CombatInventoryReference.ClassifyGuid(guid.ToLowerInvariant(), target, target.Length > 0 && Exists(target));
                    string detail = "guid=" + guid + "; classification=" + classification + "; target=" + target;
                    if (classification == "missing-guid" && path.EndsWith(".mat", StringComparison.Ordinal) && TryUnusedMaterialSlots(path, text, guid, out string unusedReason))
                    {
                        classification = "unused-material-texture-slot";
                        detail = "guid=" + guid + "; classification=" + classification + "; " + unusedReason;
                    }
                    if (classification.StartsWith("missing-", StringComparison.Ordinal))
                    {
                        detail += "; AssetDatabase cannot resolve an existing payload in this project; no parent/vendor/prefix exemption; route=" + string.Join(" -> ", Graph.RouteTo(path));
                        Block("unresolved-reachable-guid", path, detail);
                    }
                    Note("guid-reference:" + classification, path, detail);
                    loadSubjects.Add("guid-reference:" + AssetDatabase.AssetPathToGUID(path) + ":" + guid);
                }
                if (path.EndsWith(".unity", StringComparison.Ordinal) || path.EndsWith(".prefab", StringComparison.Ordinal)) AuditContainer(report, path, text);
            }
            foreach (var root in Graph.Roots.OrderBy(pair => pair.Key, StringComparer.Ordinal)) Note("gameplay-root", root.Key, string.Join("; ", root.Value));
            foreach (var root in Authoring.Roots) Note("authoring-root", root.Key, string.Join("; ", root.Value));
            int unused = paths.Count(path => path.StartsWith("Assets/", StringComparison.Ordinal) && !Graph.Reachable.Contains(path) && !Authoring.Reachable.Contains(path) &&
                !code.Contains(path) && !registries.Contains(path) && !path.EndsWith(".asmdef", StringComparison.Ordinal) && !path.EndsWith(".asmref", StringComparison.Ordinal) &&
                !path.StartsWith("Assets/CombatRealTime/Migration/Editor/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(path));
            if (unused > 0) Warn("unreachable-assets-not-audited", "Assets", unused + " unused/vendor/demo candidates omitted from payload scanning, asset fingerprint and release blockers; index only, not a validation claim.");
            report.entries.AddRange(notes); report.blockers.AddRange(blockers); report.inventorySubjects.AddRange(loadSubjects);
            report.auditCost = Costs();
        }

        private bool TryUnusedMaterialSlots(string path, string text, string guid, out string reason)
        {
            reason = null;
            string shaderReference = Field(text, "m_Shader");
            string shaderGuid = Regex.Match(shaderReference, @"\bguid:\s*([a-fA-F0-9]{32})").Groups[1].Value;
            string shaderPath = ResolveGuid(shaderGuid);
            if (!IsAsset(shaderPath) || !Exists(shaderPath)) return false;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null || AssetDatabase.GetAssetPath(material.shader) != shaderPath) return false;
            string[] slots = CombatInventoryReference.UnusedTextureSlots(text, guid, material.HasProperty);
            if (slots.Length == 0) return false;
            reason = "Saved texture slots=" + string.Join(",", slots) + "; absent from verified current shader=" + shaderPath + "; GUID remains absent, but no active property references it; not a blanket importer exemption.";
            return true;
        }

        private void AuditContainer(ProductionCombatMigration.Report report, string path, string text)
        {
            if (path.EndsWith(".unity", StringComparison.Ordinal)) report.zoneSubjects.Add("scene:" + AssetDatabase.AssetPathToGUID(path));
            foreach (Match match in Regex.Matches(text, @"(?m)^[ \t]*propertyPath:[ \t]*([^\r\n]+)"))
            {
                string tail = text.Substring(match.Index, Math.Min(1024, text.Length - match.Index)); int next = tail.IndexOf("- target:", StringComparison.Ordinal);
                if (next >= 0) tail = tail.Substring(0, next);
                Note("serialized-override", path, match.Groups[1].Value + "; value=" + Field(tail, "value") + "; reference=" + Field(tail, "objectReference"));
            }
            if (Regex.IsMatch(text, @"m_Script:\s*\{\s*fileID:\s*0\s*\}")) Block("unknown-missing-component", path, "Gameplay-reachable component has no script.");
            foreach (string document in Regex.Split(text, @"(?m)^--- "))
            {
                var script = Regex.Match(document, @"m_Script:\s*\{[^}]*guid:[ \t]*([a-fA-F0-9]{32})"); if (!script.Success) continue;
                string scriptPath = ResolveGuid(script.Groups[1].Value);
                if (scriptPath.EndsWith("/EnemyController.cs", StringComparison.Ordinal) || scriptPath.EndsWith("/LitBrainsEnemy.cs", StringComparison.Ordinal))
                    report.enemySubjects.Add("enemy-instance:" + AssetDatabase.AssetPathToGUID(path) + ":" + Regex.Match(document, @"&(-?\d+)").Groups[1].Value);
                if (!scriptPath.EndsWith("/SceneMarker.cs", StringComparison.Ordinal)) continue;
                string id = Field(document, "markerId"), character = Field(document, "characterData"), baked = Field(document, "bakedCharacterInstance");
                Note("scene-marker", path, "markerId=" + id + "; character=" + character + "; baked=" + baked);
                report.inventorySubjects.Add("marker:" + AssetDatabase.AssetPathToGUID(path) + ":" + id); report.saveSubjects.Add("marker:" + AssetDatabase.AssetPathToGUID(path) + ":" + id);
                if (id.Length == 0) Block("unknown-marker-identity", path, "Reachable marker lacks stable markerId.");
                if ((Field(document, "assetType") == "0" || Field(document, "assetType") == "") && (character.Length == 0 || character.Contains("fileID: 0") || baked.Length == 0 || baked.Contains("fileID: 0")))
                {
                    string fallback = CombatMarkerInventory.DirectChildFallback(text, document, guid => {
                        string prefab = ResolveGuid(guid);
                        return prefab.EndsWith(".prefab", StringComparison.Ordinal) && Exists(prefab) ? Read(prefab) : "";
                    }, ResolveGuid);
                    if (fallback == null) Block("unknown-marker-character", path, "markerId=" + id + "; character=" + character + "; baked=" + baked + "; no verified direct-child CharacterData match; no automatic bake.");
                    else Note("scene-marker-runtime-fallback", path, "markerId=" + id + "; " + fallback + "; hidden serialized reference unchanged; runtime/navigation not validated.");
                }
            }
        }

        public CombatReadinessEvidence.Snapshot CaptureSnapshot()
        {
            if (snapshot != null) { contentCacheHits++; return snapshot; }
            var files = new HashSet<string>(Graph.Reachable.Concat(Authoring.Reachable).Concat(code).Concat(registries), StringComparer.Ordinal);
            foreach (string path in Directory.EnumerateFiles("ProjectSettings", "*", SearchOption.AllDirectories)) files.Add(path.Replace('\\', '/'));
            foreach (string path in new[] { "Packages/manifest.json", "Packages/packages-lock.json" }) if (File.Exists(path)) files.Add(path);
            foreach (string path in paths.Where(path => path.EndsWith(".asmdef", StringComparison.Ordinal) || path.EndsWith(".asmref", StringComparison.Ordinal) || path.EndsWith("/package.json", StringComparison.Ordinal) || path.StartsWith("Assets/CombatRealTime/Migration/Editor/", StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal))) files.Add(path);
            foreach (string path in files.ToArray()) if (Exists(path + ".meta")) files.Add(path + ".meta");
            snapshot = new CombatReadinessEvidence.Snapshot { editorContext = Application.unityVersion + "; target=" + EditorUserBuildSettings.activeBuildTarget };
            var lines = new List<string> { cost.policy, snapshot.editorContext, string.Join(";", GameCodeRoots) };
            foreach (string path in files.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                try { lines.Add(path + "\0" + GetContent(path).hash); snapshot.files.Add(path); }
                catch (Exception exception) { snapshot.errors.Add(path + ": " + exception.Message); }
            }
            snapshot.fingerprint = CombatReadinessEvidence.HashText(string.Join("\n", lines)); ResolveRevision(snapshot); return snapshot;
        }

        private void ResolveRevision(CombatReadinessEvidence.Snapshot result)
        {
            result.revision = ReadLocalRevision();
            if (result.revision != null) { result.revisionSource = "local-git-project-root"; return; }
            result.revisionSource = "unavailable";
            if (!string.IsNullOrEmpty(parentRevisionPath))
            {
                try
                {
                    var export = JsonUtility.FromJson<CombatRevisionProvenance.ParentExport>(File.ReadAllText(parentRevisionPath));
                    string error = CombatRevisionProvenance.Validate(export, result.fingerprint);
                    if (error != null) { result.errors.Add(error); return; }
                    result.revision = export.revision; result.revisionSource = "parent-attested-not-local-git";
                    result.revisionAttestationSha256 = CombatReadinessEvidence.HashFile(parentRevisionPath); result.revisionVerifiedBy = export.verifiedBy; result.revisionVerifiedUtc = export.verifiedUtc; return;
                }
                catch (Exception exception) { result.errors.Add("Parent revision attestation: " + exception.Message); return; }
            }
            result.errors.Add("No Git metadata at project root. Supply a fingerprint-matching parent export via -combatParentRevision; no revision is invented.");
        }

        public bool VerifyUnchanged()
        {
            foreach (var content in contents.Values) { var info = new FileInfo(content.physical); if (!info.Exists || info.Length != content.length || info.LastWriteTimeUtc.Ticks != content.ticks) return false; }
            if (snapshot != null && snapshot.revisionSource == "local-git-project-root" && ReadLocalRevision() != snapshot.revision) return false;
            if (snapshot != null && snapshot.revisionSource == "parent-attested-not-local-git" && (!File.Exists(parentRevisionPath) || CombatReadinessEvidence.HashFile(parentRevisionPath) != snapshot.revisionAttestationSha256)) return false;
            cost.assetIndexQueries++;
            return paths.SequenceEqual(AssetDatabase.GetAllAssetPaths().Where(IsAsset).OrderBy(value => value, StringComparer.Ordinal));
        }
        private Content GetContent(string path)
        {
            if (contents.TryGetValue(path, out var content)) { contentCacheHits++; return content; }
            string physical = Physical(path); var info = new FileInfo(physical); if (!info.Exists) throw new FileNotFoundException("Scoped input missing", path);
            content = new Content { physical = physical, length = info.Length, ticks = info.LastWriteTimeUtc.Ticks };
            if (TextTypes.Contains(Path.GetExtension(path)))
            {
                byte[] bytes = File.ReadAllBytes(physical);
                using (var hash = System.Security.Cryptography.SHA256.Create()) content.hash = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                using (var reader = new StreamReader(new MemoryStream(bytes))) content.text = reader.ReadToEnd();
            }
            else content.hash = CombatReadinessEvidence.HashFile(physical);
            cost.contentReads++; cost.filesHashed++; cost.bytesRead += content.length; cost.bytesHashed += content.length; contents.Add(path, content); return content;
        }
        public string Read(string path) { try { return GetContent(path).text ?? ""; } catch (Exception exception) { Block("unreadable-scoped-input", path, exception.Message); return ""; } }
        public void AuditPlayerSkillMetadata(ProductionCombatMigration.Report report, CharacterData data)
        {
            var type = TypeCache.GetTypesDerivedFrom<ScriptableObject>().SingleOrDefault(candidate => candidate.Name == "CombatNetworkSkillCatalog");
            var classify = type?.GetMethod("Classify", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var skills = (data.combatSkills ?? new List<SkillSO>()).Concat((data.groundBasicSkills ?? new List<BasicSkillsSO>()).Cast<SkillSO>())
                .Concat((data.airBasicSkills ?? new List<BasicSkillsSO>()).Cast<SkillSO>()).Concat(new SkillSO[] { data.chargedSkill }).Where(skill => skill != null).Distinct();
            foreach (var skill in skills)
            {
                string path = AssetDatabase.GetAssetPath(skill);
                if (classify == null)
                {
                    report.blockers.Add(new ProductionCombatMigration.Entry("network-capability-metadata-unavailable", path, "Current network catalog classifier is unavailable; no capability or runtime proof inferred."));
                    continue;
                }
                try
                {
                    object[] args = { skill, null };
                    string family = classify.Invoke(null, args)?.ToString() ?? "Unsupported";
                    report.networkSubjects.Add("network-skill:" + AssetDatabase.AssetPathToGUID(path) + ":" + family);
                    report.entries.Add(new ProductionCombatMigration.Entry("network-capability-stage", path, "Current catalog classification=" + family + "; " + args[1] + ". Classification/compilation is not live host/client validation."));
                    if (family == "Unsupported") report.blockers.Add(new ProductionCombatMigration.Entry("unsupported-network-family", path, args[1]?.ToString() ?? "No supported authored network family."));
                }
                catch (Exception exception) { report.blockers.Add(new ProductionCombatMigration.Entry("network-capability-metadata-unavailable", path, exception.Message)); }
            }
        }
        public Cost Costs()
        {
            cost.roots = Graph.Roots.Count; cost.reachableAssets = Graph.Reachable.Count; cost.authoringAssets = Authoring.Reachable.Count;
            cost.dependencyQueries = Graph.DependencyQueries; cost.dependencyEdges = Graph.EdgeCount;
            cost.cacheHits = contentCacheHits + Graph.CacheHits + Authoring.CacheHits; cost.elapsedMilliseconds = timer.ElapsedMilliseconds; return cost;
        }
        private string ResolveGuid(string guid) { if (!guids.TryGetValue(guid, out var path)) guids[guid] = path = AssetDatabase.GUIDToAssetPath(guid); return path; }
        private void Root(string path, string reason) { if (string.IsNullOrEmpty(path)) Block("missing-gameplay-root", "", reason); else Graph.AddRoot(path, reason); }
        private void Note(string category, string path, string detail) => notes.Add(new ProductionCombatMigration.Entry(category, path, detail));
        private void Warn(string category, string path, string detail) { cost.warnings++; Note("warning:" + category, path, detail); }
        private void Block(string category, string path, string detail) { if (!blockers.Any(entry => entry.category == category && entry.path == path && entry.detail == detail)) blockers.Add(new ProductionCombatMigration.Entry(category, path, detail)); }
    }

    // Candidate closure only. Unknown runtime loads stay blockers in the paired scope report, never implicit completeness.
    public static IReadOnlyCollection<string> GetGameplayReachablePaths() => GetGameplayReachablePaths(out _);
    public static IReadOnlyCollection<string> GetGameplayReachablePaths(out ProductionCombatMigration.Report scopeReport)
    {
        var audit = new Session(); scopeReport = new ProductionCombatMigration.Report(); audit.Collect(scopeReport);
        scopeReport.succeeded = true;
        return audit.Graph.Reachable.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
    public static CombatReadinessEvidence.Snapshot CaptureSnapshot() => new Session().CaptureSnapshot();

    // Explicit parent export only; not runtime/test evidence. Run before copying the same scoped bytes/metas into a sandbox.
    public static void ExportVerifiedRevision(string outputPath, string verifiedBy)
    {
        if (string.IsNullOrWhiteSpace(verifiedBy)) throw new ArgumentException("Parent verifier identity required.");
        var snapshot = new Session().CaptureSnapshot();
        if (snapshot.errors.Count != 0 || snapshot.revisionSource != "local-git-project-root") throw new InvalidOperationException("Export requires actual local Git verification and complete scoped bytes.");
        string output = Path.GetFullPath(outputPath);
        foreach (string directory in new[] { "Assets", "Packages", "ProjectSettings" })
            if (output.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Export outside source/asset directories.");
        File.WriteAllText(output, JsonUtility.ToJson(new CombatRevisionProvenance.ParentExport { revision = snapshot.revision, fingerprint = snapshot.fingerprint, verifiedBy = verifiedBy,
            verifiedUtc = DateTimeOffset.UtcNow.ToString("O"), sourceRepository = Path.GetFullPath("."), exportId = Guid.NewGuid().ToString("N") }, true));
    }
    private static string ReadLocalRevision()
    {
        if (!Directory.Exists(".git") && !File.Exists(".git")) return null; // Do not inherit an enclosing checkout's revision.
        try
        {
            using (var process = Process.Start(new ProcessStartInfo("git", "rev-parse --verify HEAD") { WorkingDirectory = Path.GetFullPath("."), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                if (process == null) return null; if (!process.WaitForExit(5000)) { process.Kill(); return null; }
                string revision = process.StandardOutput.ReadToEnd().Trim(); return process.ExitCode == 0 && Regex.IsMatch(revision, "^[a-fA-F0-9]{40,64}$") ? revision : null;
            }
        }
        catch { return null; }
    }
    private static bool IsAsset(string path) => path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal);
    private static bool IsGameCode(string path) => GameCodeRoots.Any(root => path.StartsWith(root, StringComparison.Ordinal)) && !path.Split('/').Any(segment => segment.Equals("Samples", StringComparison.OrdinalIgnoreCase) || segment.Equals("Demo", StringComparison.OrdinalIgnoreCase) || segment.Equals("Demos", StringComparison.OrdinalIgnoreCase));
    private static string Logical(string path)
    {
        path = path.Replace('\\', '/'); if (!Path.IsPathRooted(path)) return path;
        string root = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && IsAsset(path.Substring(root.Length))) return path.Substring(root.Length);
        foreach (var package in PackageInfo.GetAllRegisteredPackages())
        {
            string prefix = package.resolvedPath.Replace('\\', '/').TrimEnd('/') + "/";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return "Packages/" + package.name + "/" + path.Substring(prefix.Length);
        }
        return path;
    }
    private static string Physical(string path)
    {
        if (File.Exists(path)) return path;
        var package = path.StartsWith("Packages/", StringComparison.Ordinal) ? PackageInfo.FindForAssetPath(path) : null;
        return package == null ? path : Path.Combine(package.resolvedPath, path.Substring(("Packages/" + package.name + "/").Length));
    }
    private static bool Exists(string path) => File.Exists(Physical(path));
    private static string Field(string text, string field) => Regex.Match(text, @"(?m)^[ \t]*" + Regex.Escape(field) + @":[ \t]*([^\r\n]*)").Groups[1].Value.Trim();
    private static string ReferenceGuid(Match match) => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    private static string ResourceKey(string path) { int start = path.IndexOf("/Resources/", StringComparison.Ordinal) + 11; return path.Substring(start, path.Length - start - Path.GetExtension(path).Length); }
    private static void AddIndex(Dictionary<string, List<string>> index, string key, string path) { if (!index.TryGetValue(key, out var list)) index[key] = list = new List<string>(); list.Add(path); }
    private static string Argument(string name) { string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
}
