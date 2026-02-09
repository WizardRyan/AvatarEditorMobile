using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Genies.CrashReporting;
using Genies.Inventory;
using Genies.ServiceManagement;
using Genies.Services.Model;
using UnityEngine;

namespace Genies.Naf.Content
{
    /// <summary>
    /// Base class for NafContentService implementations that provides shared functionality
    /// for asset ID conversion and parameter fetching
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal abstract class NafContentServiceBase : IAssetParamsService, IAssetIdConverter
#else
    public abstract class NafContentServiceBase : IAssetParamsService, IAssetIdConverter
#endif
    {
        protected bool _initialized = false;
        protected readonly Dictionary<string, NafContentMetadata> _assetsByAddress = new();
        protected readonly IReadOnlyDictionary<string, string> _avatarBaseGuidMap = StaticToBaseMap.LocalGuidMap;
        protected readonly List<string> _staticMapping = new() {"AvatarDna", "AvatarTattoo"};
        protected readonly List<string> _overrideVersion = new() {"AvatarBase",};
        protected string _AvatarBaseVersionFromConfig = null;

        // For thread-safe cache access during on-demand resolution
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initialize the service with data from the specific source (inventory, CMS, etc.)
        /// </summary>
        public abstract UniTask Initialize();

        public async UniTask<string> ConvertToUniversalIdAsync(string assetId)
        {
            await InitializeIfNeededAsync();

            string universalId = GetUniversalId(assetId);

            // Temp Hotfix Remove spaces, using %20 does not work, until next Naf update
            universalId = universalId.Replace(' ', '+');
            return await UniTask.FromResult(universalId);
        }

        /// <summary>
        /// Batch converts multiple asset IDs to universal IDs.
        /// </summary>
        /// <param name="assetIds">List of asset IDs to convert</param>
        /// <returns>Dictionary mapping original asset IDs to their universal IDs</returns>
        public async UniTask<Dictionary<string, string>> ConvertToUniversalIdsAsync(List<string> assetIds)
        {
            await InitializeIfNeededAsync();

            if (assetIds == null || assetIds.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            // Convert all asset IDs to universal IDs
            var result = new Dictionary<string, string>();
            foreach (var assetId in assetIds)
            {
                var universalId = await ConvertToUniversalIdAsync(assetId);
                result[assetId] = universalId;
            }

            return result;
        }

        public async UniTask<Dictionary<string, string>> FetchParamsAsync(string assetId)
        {
            await InitializeIfNeededAsync();

            Dictionary<string, string> result = GetParams(assetId, LodLevels.DefaultLod);
            return await UniTask.FromResult(result);
        }

        /// <summary>
        /// Returns the input if it cant determine or translate the assetId
        /// </summary>
        protected string GetUniversalId(string assetId)
        {
            // Strip prefix for lookup
            var lookupKey = ToLookupKey(assetId);
            // Apply avatar base mapping (maps static IDs to GUIDs)
            var mappedId = ToAvatarBaseMapping(lookupKey);

            if (!_assetsByAddress.TryGetValue(mappedId, out var result))
            {
                // Also try without mapping in case it's not an avatar base asset
                if (!_assetsByAddress.TryGetValue(lookupKey, out result))
                {
                    return mappedId;
                }
            }

            var pipelineId = !string.IsNullOrEmpty(result.PipelineId)
                ? (_staticMapping.Contains(result.PipelineId) ? "Static/" : $"{result.PipelineId}/")
                : string.Empty;

            return string.IsNullOrEmpty(pipelineId)? assetId :
                string.Equals(pipelineId, "Static/")? $"{pipelineId}{result.AssetAddress}" : $"{pipelineId}{result.Guid}";
        }

        protected Dictionary<string, string> GetParams(string assetId, string lod = null)
        {
            if (!_assetsByAddress.TryGetValue(ToLookupKey(assetId), out NafContentMetadata result))
            {
                return default;
            }

            var assetParams = new Dictionary<string, string>();

            if (result.UniversalBuildVersion != null)
            {
                var version = string.IsNullOrEmpty(result.UniversalBuildVersion) ? "0" : result.UniversalBuildVersion;

                // use config override for AvatarBase if set, otherwise use the version from the metadata
                if (_overrideVersion.Contains(result.PipelineId))
                {
                    version = !string.IsNullOrEmpty(_AvatarBaseVersionFromConfig) ? _AvatarBaseVersionFromConfig : result.UniversalBuildVersion;
                }

                assetParams.Add("v", version);
            }

            if (!string.IsNullOrEmpty(lod))
            {
                assetParams.Add("lod", lod);
            }

            return assetParams;
        }

        protected async UniTask InitializeIfNeededAsync()
        {
            if (!_initialized)
            {
                await Initialize();
            }
        }

        /// <summary>
        /// Strips assetType/ from assetAddress or just returns the assetId if no type is present.
        /// eg: recSjNgdNxWYeuLeD || WardrobeGear/recSjNgdNxWYeuLeD => recSjNgdNxWYeuLeD
        /// eg: Genie_Unified_gen13gp_Race_Container || Static/Genie_Unified_gen13gp_Race_Container => Genie_Unified_gen13gp_Race_Container
        /// Finds key for both types of assetIds
        /// </summary>
        protected static string ToLookupKey(string assetId)
        {
            // just substrings last part after '/'
            var pathIdx = assetId.LastIndexOf('/');
            return pathIdx == -1 ? assetId : assetId.Substring(pathIdx + 1);
        }

        /// <summary>
        /// Temporary Fix while migration is underway, to get AvatarBase guids using static ids
        /// only works for BodyType Containers, assets that do not show on the UI
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        protected string ToAvatarBaseMapping(string assetId)
        {
            // also include the same metadata for static ids of BodyType Containers.
            return _avatarBaseGuidMap.TryGetValue(assetId, out var avatarBaseGuid) ? avatarBaseGuid : assetId;
        }

        /// <summary>
        /// Merges source dictionary into target dictionary, overwriting duplicates.
        /// </summary>
        protected static void Merge<TKey, TValue>(IDictionary<TKey, TValue> target, IDictionary<TKey, TValue> source)
        {
            foreach (KeyValuePair<TKey, TValue> kvp in source)
            {
                target[kvp.Key] = kvp.Value; // overwrites duplicates
            }
        }

        /// <summary>
        /// Resolves multiple asset IDs by fetching their pipeline data
        /// </summary>
        /// <param name="assetIds">List of asset IDs to resolve</param>
        public async UniTask ResolveAssetsAsync(List<string> assetIds)
        {
            if (assetIds == null || assetIds.Count == 0)
            {
                return;
            }

            // Convert IDs in case they haven't been already
            var convertedIdsDict = await ConvertToUniversalIdsAsync(assetIds);

            var defaultInventoryService = ServiceManager.Get<IDefaultInventoryService>();
            if (defaultInventoryService == null)
            {
                Debug.LogError("[NafContentServiceBase] DefaultInventoryService not found. Cannot resolve assets on-demand.");
                return;
            }

            try
            {
                var pipelineInfoDict = await defaultInventoryService.ResolvePipelineItemsAsync(convertedIdsDict.Values.ToList());

                await _cacheLock.WaitAsync();
                try
                {
                    foreach (var kvp in pipelineInfoDict)
                    {
                        var assetId = kvp.Key;
                        var assetPipelineInfo = kvp.Value;

                        // Resolve the correct pipeline item from the list
                        var resolvedPipelineItem = await ResolvePipelineItemFromList(
                            assetPipelineInfo.Pipeline,
                            assetPipelineInfo.AssetType
                        );

                        if (resolvedPipelineItem != null)
                        {
                            var metadata = new NafContentMetadata
                            {
                                AssetAddress = string.IsNullOrEmpty(resolvedPipelineItem.AssetAddress)
                                    ? assetId
                                    : resolvedPipelineItem.AssetAddress,
                                Guid = assetId,
                                Owner = "internal", // Assets from default inventory are internal
                                UniversalBuildVersion = resolvedPipelineItem.UniversalBuildVersion,
                                UniversalAvailable = resolvedPipelineItem.UniversalAvailable ?? false,
                                PipelineId = assetPipelineInfo.AssetType
                            };

                            _assetsByAddress[assetId] = metadata;

                            // Also map parent ID if different
                            if (!string.IsNullOrEmpty(resolvedPipelineItem.ParentId) &&
                                resolvedPipelineItem.ParentId != assetId)
                            {
                                _assetsByAddress[resolvedPipelineItem.ParentId] = metadata;
                            }
                        }
                    }
                }
                finally
                {
                    _cacheLock.Release();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NafContentServiceBase] Failed to resolve assets on-demand: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves the correct pipeline item from a list by selecting the most recent version
        /// that is universally available.
        /// </summary>
        private UniTask<PipelineItemV2> ResolvePipelineItemFromList(List<PipelineItemV2> pipelineItems, string assetType)
        {
            if (pipelineItems == null || pipelineItems.Count == 0)
            {
                if (assetType != AssetType.ColorPreset.ToString())
                {
                    CrashReporter.LogWarning($"[NafContentServiceBase] Asset type {assetType} has no pipeline defined.");
                }

                return UniTask.FromResult<PipelineItemV2>(null);
            }

            try
            {
                // Select the most recent pipeline item (highest version)
                // Prefer items that are universally available
                var selectedItem = pipelineItems
                    .OrderByDescending(p =>
                    {
                        // Parse the pipeline version to get a sortable value
                        if (int.TryParse(p.PipelineVersion?.Split('.')[0], out int majorVersion))
                        {
                            return majorVersion;
                        }
                        return 0;
                    })
                    .ThenByDescending(p => p.UniversalAvailable ?? false)
                    .ThenByDescending(p => p.AssetVersion ?? 0)
                    .FirstOrDefault();

                return UniTask.FromResult(selectedItem);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NafContentServiceBase] Error resolving pipeline version for asset type {assetType}: {ex}");
                return UniTask.FromResult(pipelineItems.LastOrDefault());
            }
        }

        // Lods on Mac need to be High by default due normals not matching for mac m gpus
        protected static class LodLevels
        {
            public const string High = "0";
            public const string Mid = "1";
            public const string Low = "2";
            public static string DefaultLod => Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer ? High : Low;
        }

        protected static class StaticToBaseMap
        {
            // during integration we have to support static ids and avatarBase ids
            // this map is fallback when controllers call using Legacy Core BodyTypes ids
            public static readonly IReadOnlyDictionary<string, string> LocalGuidMap = new Dictionary<string, string>()
            {
                // all Body Type Containers
                {"Genie_Unified_gen13gp_Race_Container", "recmDqoKYpEG1TQV"},
                {"Static/Genie_Unified_gen13gp_Race_Container", "recmDqoKYpEG1TQV"},

                {"Genie_Unified_gen12gp_Container", "recMdZ4WQ4HSkb8U"},
                {"Static/Genie_Unified_gen12gp_Container", "recMdZ4WQ4HSkb8U"},

                {"Genie_Unified_gen11gp_Container", "recmdz4WQ4hM30ZC"},
                {"Static/Genie_Unified_gen11gp_Container", "recmdz4WQ4hM30ZC"},

                {"DollGen1_RaceData_Container", "recMdZ4wQ4HQS1uC"},
                {"Static/DollGen1_RaceData_Container", "recMdZ4wQ4HQS1uC"},

                {"BlendShapeContainer_body_female", "recmdZ4C4enmt630"},
                {"Static/BlendShapeContainer_body_female", "recmdZ4C4enmt630"},

                {"BlendShapeContainer_body_male", "recmdZ4c4ENEO817"},
                {"Static/BlendShapeContainer_body_male", "recmdZ4c4ENEO817"},
            };
        }
    }
}
