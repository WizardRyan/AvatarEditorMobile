using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Genies.Addressables.Naf;
using Genies.Addressables.Utils;
using Genies.Login.Native;
using Genies.ServiceManagement;

namespace Genies.Naf.Content
{
#if GENIES_SDK && !GENIES_INTERNAL
    internal static class NafContentInitializer
#else
    public static class NafContentInitializer
#endif
    {
        public static bool IsInitialized { get; private set; }

        public static bool IncludeV1Inventory { get; set; }

        public static async UniTask Initialize()
        {
            if (IsInitialized)
            {
                return;
            }
            IsInitialized = true;

            // If this class can be disposed, make sure to unsubscribe.
            GeniesLoginSdk.UserLoggedIn -= OnGeniesUserLoggedIn;
            GeniesLoginSdk.UserLoggedIn += OnGeniesUserLoggedIn;

            await FetchLocationsAsync(); // Returns if user is not logged in.
        }

        private static async void OnGeniesUserLoggedIn()
        {
            await FetchLocationsAsync();
        }

        private static async Task FetchLocationsAsync()
        {
            if (GeniesLoginSdk.IsUserSignedIn() is false)
            {
                return;
            }

            // Register the resource provider but do not fetch inventory.
            // Locations will be registered on-demand as avatar editor screens are opened
            GeniesAddressablesUtils.RegisterNewResourceProviderOnAddressables(new UniversalContentResourceProvider());

            await UniTask.CompletedTask;
        }
    }
}
