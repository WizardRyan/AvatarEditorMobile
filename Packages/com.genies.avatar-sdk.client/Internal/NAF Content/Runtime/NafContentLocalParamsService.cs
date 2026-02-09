using System;
using System.Collections.Generic;
using Genies.Naf.Content.AvatarBaseConfig;
using Cysharp.Threading.Tasks;

namespace Genies.Naf.Content
{
    /// <summary>
    /// Hardcoded set of guids used as fallback when core Avatar assets cannot be retrieved
    /// from any of the cms services.
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal class NafContentLocalParamsService : IAssetParamsService, IAssetIdConverter
#else
    public class NafContentLocalParamsService : IAssetParamsService, IAssetIdConverter
#endif
    {
        private IReadOnlyDictionary<string, string> _universalGuidMap;

        public UniTask<string> ConvertToUniversalIdAsync(string assetId)
        {
            InitializeLocalGuidMap();
            
            return UniTask.FromResult(_universalGuidMap.TryGetValue(assetId, out var uri) ? UriToUniversalId(uri) : assetId);
        }

        public async UniTask<Dictionary<string, string>> ConvertToUniversalIdsAsync(List<string> assetIds)
        {
            var result = new Dictionary<string, string>();
            foreach (var assetId in assetIds)
            {
                var newId = await ConvertToUniversalIdAsync(assetId);
                result[assetId] = newId;
            }

            return result;
        }
        public UniTask ResolveAssetsAsync(List<string> assetIds)
        {
            // Nothing needed to resolve for local
            return UniTask.CompletedTask;
        }

        public UniTask<Dictionary<string, string>> FetchParamsAsync(string assetId)
        {
            InitializeLocalGuidMap();
                
            return UniTask.FromResult(_universalGuidMap.TryGetValue(assetId, out var uri) ? UriToDict(uri) : default);
        }
        
        private void InitializeLocalGuidMap()
        {
            if (_universalGuidMap != null)
            {
                return;
            }

            var avatarBaseVersion = AvatarBaseVersionService.GetAvatarBaseVersion().GetAwaiter().GetResult();
            _universalGuidMap = LocalParamsMap.GetLocalGuidMap(avatarBaseVersion: avatarBaseVersion);
        }

        private static string UriToUniversalId(string uri)
        {
            var result = string.Empty;

            var pathIdx = uri.LastIndexOf('/');
            if (pathIdx == -1)
            {
                return result;
            }

            return uri.Substring(0, pathIdx);
        }

        private static Dictionary<string, string> UriToDict(string uri)
        {
            var result = new Dictionary<string, string>();

            var queryIdx = uri.IndexOf('?');
            if (queryIdx == -1)
            {
                return result;
            }

            var query = uri.Substring(queryIdx + 1);
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2)
                {
                    result[kv[0]] = kv[1];
                }
            }
            // patch to allow dynamically loading content with or without lod0
            if (result.TryGetValue("v", out var version))
            {
                result.Add("lod", "0");
            }

            return result;
        }

        private static bool IsNewerVersionThan(string version, string compareVersion)
        {
            try
            {
                var ver = long.Parse(version);
                var normVer = int.Parse(compareVersion);
                return ver >= normVer;
            }
            catch
            {
                return false;
            }
        }


        private static class LocalParamsMap
        {
            // Working version in prod & dev at the time of naf-gp integration
            private const string _defaultVersion = "1758268349";
            private const string _defaultAvatarBaseVer = "1.0.0";

            public static IReadOnlyDictionary<string, string> GetLocalGuidMap(string staticVersion = _defaultVersion, string avatarBaseVersion = _defaultAvatarBaseVer)
            {
                return new Dictionary<string, string>()
                {
                    // all Body Type Containers static (legacy)
                    {"Genie_Unified_gen13gp_Race_Container", $"Static/Genie_Unified_gen13gp_Race_Container/manifest.bin?v={staticVersion}"},
                    {"Static/Genie_Unified_gen13gp_Race_Container", $"Static/Genie_Unified_gen13gp_Race_Container/manifest.bin?v={staticVersion}"},

                    {"Genie_Unified_gen12gp_Container", $"Static/Genie_Unified_gen12gp_Container/manifest.bin?v={staticVersion}"},
                    {"Static/Genie_Unified_gen12gp_Container", $"Static/Genie_Unified_gen12gp_Container/manifest.bin?v={staticVersion}"},

                    {"Genie_Unified_gen11gp_Container", $"Static/Genie_Unified_gen11gp_Container/manifest.bin?v={staticVersion}"},
                    {"Static/Genie_Unified_gen11gp_Container", $"Static/Genie_Unified_gen11gp_Container/manifest.bin?v={staticVersion}"},

                    {"DollGen1_RaceData_Container", $"Static/DollGen1_RaceData_Container/manifest.bin?v={staticVersion}"},
                    {"Static/DollGen1_RaceData_Container", $"Static/DollGen1_RaceData_Container/manifest.bin?v={staticVersion}"},

                    {"BlendShapeContainer_body_female", $"Static/BlendShapeContainer_body_female/manifest.bin?v={staticVersion}"},
                    {"Static/BlendShapeContainer_body_female", $"Static/BlendShapeContainer_body_female/manifest.bin?v={staticVersion}"},

                    {"BlendShapeContainer_body_male", $"Static/BlendShapeContainer_body_male/manifest.bin?v={staticVersion}"},
                    {"Static/BlendShapeContainer_body_male", $"Static/BlendShapeContainer_body_male/manifest.bin?v={staticVersion}"},

                    // Avatar base default bodytype
                    {"recmDqoKYpEG1TQV", $"AvatarBase/recmDqoKYpEG1TQV/manifest.bin?v={avatarBaseVersion}"},
                    {"AvatarBase/recmDqoKYpEG1TQV", $"AvatarBase/recmDqoKYpEG1TQV/manifest.bin?v={avatarBaseVersion}"},

                    {"recMdZ4WQ4HSkb8U", $"AvatarBase/recMdZ4WQ4HSkb8U/manifest.bin?v={avatarBaseVersion}"},
                    {"AvatarBase/recMdZ4WQ4HSkb8U", $"AvatarBase/recMdZ4WQ4HSkb8U/manifest.bin?v={avatarBaseVersion}"},

                    {"recmdz4WQ4hM30ZC", $"AvatarBase/recmdz4WQ4hM30ZC/manifest.bin?v={avatarBaseVersion}"},
                    {"AvatarBase/recmdz4WQ4hM30ZC", $"AvatarBase/recmdz4WQ4hM30ZC/manifest.bin?v={avatarBaseVersion}"},

                    {"recMdZ4wQ4HQS1uC", $"AvatarBase/recMdZ4wQ4HQS1uC/manifest.bin?v={avatarBaseVersion}"},
                    {"AvatarBase/recMdZ4wQ4HQS1uC", $"AvatarBase/recMdZ4wQ4HQS1uC/manifest.bin?v={avatarBaseVersion}"},

                    {"recmdZ4C4enmt630", $"AvatarBase/recmdZ4C4enmt630/manifest.bin?v={avatarBaseVersion}"},
                    {"AvatarBase/recmdZ4C4enmt630", $"AvatarBase/recmdZ4C4enmt630/manifest.bin?v={avatarBaseVersion}"},

                    {"recmdZ4c4ENEO817", $"AvatarBase/recmdZ4c4ENEO817/manifest.bin?v={avatarBaseVersion}"},
                    {"AvatarBase/recmdZ4c4ENEO817", $"AvatarBase/recmdZ4c4ENEO817/manifest.bin?v={avatarBaseVersion}"},

                    // Edge case legacy eye materials with spaces
                    {"EyeMaterialData_NewBlue Light", $"Static/EyeMaterialData_NewBlueLight/manifest.bin?v={staticVersion}"},
                    {"Static/EyeMaterialData_NewBlue Light", $"Static/EyeMaterialData_NewBlueLight/manifest.bin?v={staticVersion}"},

                    {"EyeMaterialData_NewBrown light", $"Static/EyeMaterialData_NewBrownLight/manifest.bin?v={staticVersion}"},
                    {"Static/EyeMaterialData_NewBrown light", $"Static/EyeMaterialData_NewBrownLight/manifest.bin?v={staticVersion}"},

                    {"EyeMaterialData_NewGreenBlue Dark", $"Static/EyeMaterialData_NewGreenBlueDark/manifest.bin?v={staticVersion}"},
                    {"Static/EyeMaterialData_NewGreenBlue Dark", $"Static/EyeMaterialData_NewGreenBlueDark/manifest.bin?v={staticVersion}"},

                    {"EyeMaterialData_NewGreenBlue Light", $"Static/EyeMaterialData_NewGreenBlueLight/manifest.bin?v={staticVersion}"},
                    {"Static/EyeMaterialData_NewGreenBlue Light", $"Static/EyeMaterialData_NewGreenBlueLight/manifest.bin?v={staticVersion}"},
                };
            }
        }
    }
}
