using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Genies.Naf.Content
{
#if GENIES_SDK && !GENIES_INTERNAL
    internal interface IAssetIdConverter
#else
    public interface IAssetIdConverter
#endif
    {
        /// <summary>
        /// Resolves pipeline data for the given asset IDs by fetching from the inventory API.
        /// This populates the internal cache with metadata needed for conversion.
        /// Should be called before ConvertToUniversalIdsAsync for best performance.
        /// </summary>
        UniTask ResolveAssetsAsync(List<string> assetIds);

        /// <summary>
        /// Converts a single asset ID to its universal ID format.
        /// Assumes asset has already been resolved via ResolveAssetsAsync.
        /// </summary>
        UniTask<string> ConvertToUniversalIdAsync(string assetId);

        /// <summary>
        /// Converts multiple asset IDs to their universal ID format.
        /// Assumes assets have already been resolved via ResolveAssetsAsync.
        /// </summary>
        UniTask<Dictionary<string, string>> ConvertToUniversalIdsAsync(List<string> assetIds);
    }
}
