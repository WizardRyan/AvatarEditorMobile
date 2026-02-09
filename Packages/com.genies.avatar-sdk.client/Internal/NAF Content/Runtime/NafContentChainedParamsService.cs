using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Genies.Naf.Content
{
    /// <summary>
    /// Chained set of IAssetParamsServices that will try to resolve params and ids for a given assetId
    /// order of resolution depends on input
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal class NafContentChainedParamsService : IAssetParamsService, IAssetIdConverter
#else
    public class NafContentChainedParamsService : IAssetParamsService, IAssetIdConverter
#endif
    {
        private readonly IEnumerable<IAssetParamsService> _services;
        private readonly IEnumerable<IAssetIdConverter> _converters;

        public NafContentChainedParamsService(IEnumerable<IAssetParamsService> paramServices, IEnumerable<IAssetIdConverter> converterServices)
        {
            if (paramServices is null)
            {
                _services = Array.Empty<IAssetParamsService>();
                return;
            }
            _services = paramServices;

            if (converterServices is null)
            {
                _converters = Array.Empty<IAssetIdConverter>();
                return;
            }
            _converters = converterServices;
        }


        public async UniTask<string> ConvertToUniversalIdAsync(string assetId)
        {
            foreach (IAssetIdConverter converters in _converters)
            {
                var universalId = await converters.ConvertToUniversalIdAsync(assetId);
                // return first success
                if (!string.IsNullOrEmpty(universalId))
                {
                    return universalId;
                }
            }
            return assetId;
        }

        public async UniTask ResolveAssetsAsync(List<string> assetIds)
        {
            foreach (IAssetIdConverter converters in _converters)
            {
                await converters.ResolveAssetsAsync(assetIds);
                return;
            }
        }

        public async UniTask<Dictionary<string, string>> ConvertToUniversalIdsAsync(List<string> assetIds)
        {
            foreach (IAssetIdConverter converters in _converters)
            {
                var universalIdMappings = await converters.ConvertToUniversalIdsAsync(assetIds);
                if (universalIdMappings?.Count > 0)
                {
                    return universalIdMappings;
                }
            }

            return new Dictionary<string, string>();
        }

        public async UniTask<Dictionary<string, string>> FetchParamsAsync(string assetId)
        {
            foreach (IAssetParamsService service in _services)
            {
                Dictionary<string, string> fParams = await service.FetchParamsAsync(assetId);
                if (fParams != null)
                {
                    return fParams;
                }
            }
            return default;
        }

    }
}
