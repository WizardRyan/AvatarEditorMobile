using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Genies.Avatars.Behaviors;
using Genies.Avatars.Services;
using Genies.CrashReporting;
using Genies.Inventory;
using Genies.Login.Native;
using Genies.Naf;
using Genies.Naf.Content;
using Genies.ServiceManagement;
using Genies.Services.Configs;
using Newtonsoft.Json;
using UnityEngine;

namespace Genies.Avatars.Sdk
{
    /// <summary>
    /// Static convenience facade for creating, loading, and manipulating Genies Avatars and controllers.
    /// - Auto-initializes required services on first use
    /// - Provides symmetrical wrappers for assets, tattoos, colors, body attributes, and definition import/export
    /// - Adds batch/optimized operations that minimize rebuilds
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal static class GeniesAvatarsSdk
#else
    public static class GeniesAvatarsSdk
#endif
    {
        private static bool IsInitialized =>
            InitializationCompletionSource is not null
            && InitializationCompletionSource.Task.Status == UniTaskStatus.Succeeded;
        private static UniTaskCompletionSource InitializationCompletionSource { get; set; }

        // Default controller prefab path in Resources.
        private const string DefaultControllerResource = "GeniesAvatarRig";

        public static event Action<bool> LoadedAvatar;

        #region Initialization / Service Access

        /// <summary>
        /// Ensures SDK dependencies are initialized.
        /// This method calls <see cref="ServiceManager.InitializeAppAsync"/> internally with the required installers.
        /// If the consuming application requires specific initialization order or custom installers,
        /// it should call <see cref="ServiceManager.InitializeAppAsync"/> before using any Avatars.Sdk API.
        /// See <see cref="GeniesAvatarSdkInstaller"/> for all required installer dependencies.
        /// </summary>
        public static async UniTask<bool> InitializeAsync()
        {
#if GENIES_DEV
            return await InitializeAsync(BackendEnvironment.Dev);
#else
            return await InitializeAsync(GeniesApiConfigManager.TargetEnvironment);
#endif
        }

        /// <summary>
        /// Ensures SDK dependencies are initialized with the specified target environment.
        /// This method calls <see cref="ServiceManager.InitializeAppAsync"/> internally with the required installers.
        /// If the consuming application requires specific initialization order or custom installers,
        /// it should call <see cref="ServiceManager.InitializeAppAsync"/> before using any Avatars.Sdk API.
        /// See <see cref="GeniesAvatarSdkInstaller"/> for all required installer dependencies.
        /// </summary>
        /// <param name="targetEnvironment">The backend environment to target for initialization</param>
        /// <returns>True if initialization was successful, false otherwise</returns>
        public static async UniTask<bool> InitializeAsync(BackendEnvironment targetEnvironment)
        {
            if (InitializationCompletionSource is not null)
            {
                await InitializationCompletionSource.Task;
                return IsInitialized;
            }

            InitializationCompletionSource = new UniTaskCompletionSource();

            if (ServiceManager.IsAppInitialized is false)
            {
                try
                {
                    var apiConfig = new GeniesApiConfig
                    {
                        TargetEnv = targetEnvironment,
                    };

                    await ServiceManager.InitializeAppAsync(
                        customInstallers: new GeniesInstallersSetup(apiConfig)
                            .ConstructInstallersList(),
                        disableAutoResolve: true);

                    InitializationCompletionSource.TrySetResult();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize ServiceManager: {ex.Message}");
                    ServiceManager.Dispose();

                    InitializationCompletionSource = null;
                    return false;
                }
            }

            InitializationCompletionSource.TrySetResult();
            return true;
        }

        /// <summary>
        /// Checks that the GeniesAvatarService exists and automatically initializes one if not present.
        /// </summary>
        internal static async UniTask<IGeniesAvatarSdkService> GetOrCreateAvatarSdkInstance()
        {
            if (await InitializeAsync() is false)
            {
                return default;
            }
            return AvatarSdkServiceProvider.Instance;
        }

        #endregion

        #region Data Loading

        public static async UniTask<List<Genies.Services.Model.Avatar>> LoadAvatarsDataByUserIdAsync(string userId)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize GeniesAvatarsSdk");
                }

                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId));
                }

                var genieAvatarSdkService = await GetOrCreateAvatarSdkInstance();
                return await genieAvatarSdkService.LoadAvatarsDataByUserIdAsync(userId);
            }
            catch (Exception ex) when (!(ex is ArgumentNullException))
            {
                CrashReporter.LogError($"Failed to load avatars data for user {userId}: {ex.Message}");
                return default;
            }
        }

        #endregion

        #region Legacy Runtime Avatar Loaders (GameObjects)

        /// <summary>Loads and creates a Genie using the currently logged-in user's data.</summary>
        public static async UniTask<GeniesAvatar> LoadUserAvatarAsync(
            string avatarName = null,
            Transform parent = null,
            RuntimeAnimatorController playerAnimationController = null,
            bool waitUntilUserIsLoggedIn = false)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize GeniesAvatarsSdk");
                }

                if (waitUntilUserIsLoggedIn)
                {
                    await GeniesLoginSdk.WaitUntilLoggedInAsync();
                }

                var result = await LoadUserAvatarController(parent);

                if (playerAnimationController != null)
                {
                    result.Animator.runtimeAnimatorController = playerAnimationController;
                }

                result.Root.gameObject.name = avatarName;

                return result;
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to load user avatar: {ex.Message}");
                return default;
            }
        }

        /// <summary>Loads and creates a default instance of an avatar.</summary>
        public static async UniTask<GeniesAvatar> LoadDefaultAvatarAsync(
            string avatarName = null,
            Transform parent = null,
            RuntimeAnimatorController playerAnimationController = null)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize GeniesAvatarsSdk");
                }

                var geniesAvatarSdkService = await GetOrCreateAvatarSdkInstance();
                var genie = await geniesAvatarSdkService.LoadDefaultRuntimeAvatarAsync(
                    avatarName,
                    parent,
                    playerAnimationController
                );

                LoadedAvatar?.Invoke(true);

                return new GeniesAvatar(genie);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to load default avatar: {ex.Message}");
                return default;
            }
        }

        /// <summary>Loads and creates an avatar using a string JSON definition.</summary>
        public static async UniTask<GeniesAvatar> LoadAvatarFromJsonAsync(
            string json,
            string avatarName = null,
            Transform parent = null,
            RuntimeAnimatorController playerAnimationController = null)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize GeniesAvatarsSdk");
                }

                if (string.IsNullOrEmpty(json))
                {
                    throw new ArgumentNullException(nameof(json));
                }

                var geniesAvatarSdkService = await GetOrCreateAvatarSdkInstance();
                var genie = await geniesAvatarSdkService.LoadRuntimeAvatarAsync(
                    json,
                    avatarName,
                    parent,
                    playerAnimationController
                );

                var returnAvatar = new GeniesAvatar(genie);
                LoadedAvatar?.Invoke(IsDefinitionDefault(returnAvatar));

                return returnAvatar;
            }
            catch (Exception ex) when (!(ex is ArgumentNullException))
            {
                CrashReporter.LogError($"Failed to load avatar from JSON: {ex.Message}");
                return default;
            }
        }

        /// <summary>Loads and creates an avatar using an explicit AvatarDefinition.</summary>
        public static async UniTask<GeniesAvatar> LoadAvatarFromDefinitionAsync(
            Naf.AvatarDefinition avatarDefinition,
            string avatarName = null,
            Transform parent = null,
            RuntimeAnimatorController playerAnimationController = null)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize GeniesAvatarsSdk");
                }

                if (avatarDefinition == null)
                {
                    throw new ArgumentNullException(nameof(avatarDefinition));
                }

                var geniesAvatarSdkService = await GetOrCreateAvatarSdkInstance();
                var genie = await geniesAvatarSdkService.LoadRuntimeAvatarAsync(
                    avatarDefinition,
                    avatarName,
                    parent,
                    playerAnimationController
                );

                var returnAvatar = new GeniesAvatar(genie);
                LoadedAvatar?.Invoke(IsDefinitionDefault(returnAvatar));

                return returnAvatar;
            }
            catch (Exception ex) when (!(ex is ArgumentNullException))
            {
                CrashReporter.LogError($"Failed to load avatar from definition: {ex.Message}");
                return default;
            }
        }

        #endregion

        #region Controller Loaders

        /// <summary>Creates a controller for the current user's avatar.</summary>
        public static async UniTask<GeniesAvatar> LoadUserAvatarController(Transform root = null)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize GeniesAvatarsSdk");
                }

                var geniesAvatarSdkService = await GetOrCreateAvatarSdkInstance();
                var myDefinition = await geniesAvatarSdkService.GetMyAvatarDefinition();

                return await LoadAvatarControllerWithClassDefinition(
                    JsonConvert.DeserializeObject<Genies.Naf.AvatarDefinition>(myDefinition), root);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to load user avatar controller: {ex.Message}");
                return default;
            }
        }

        /// <summary>Creates a controller for a specific user's avatar by userId.</summary>
        public static async UniTask<GeniesAvatar> LoadAvatarControllerById(
            string userId,
            Transform root = null)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize GeniesAvatarsSdk");
                }

                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId));
                }

                var geniesAvatarSdkService = await GetOrCreateAvatarSdkInstance();
                var defString = await geniesAvatarSdkService.LoadAvatarDefStringByUserId(userId);
                return await LoadAvatarControllerWithClassDefinition(
                    JsonConvert.DeserializeObject<Genies.Naf.AvatarDefinition>(defString), root);
            }
            catch (Exception ex) when (!(ex is ArgumentNullException))
            {
                CrashReporter.LogError($"Failed to load avatar controller by ID {userId}: {ex.Message}");
                return default;
            }
        }

        public static async UniTask<GeniesAvatar> LoadAvatarControllerByJsonDefinition(
            string jsonDef,
            Transform root = null)
        {
            return await LoadAvatarControllerWithClassDefinition(
                JsonConvert.DeserializeObject<Genies.Naf.AvatarDefinition>(jsonDef), root);
        }

        public static async UniTask<GeniesAvatar> LoadAvatarControllerWithClassDefinition(
            Genies.Services.Model.Avatar avatar,
            Transform root = null)
        {
            return await LoadAvatarControllerWithClassDefinition(
                JsonConvert.DeserializeObject<Genies.Naf.AvatarDefinition>(avatar.Definition), root);
        }

        public static async UniTask<GeniesAvatar> LoadAvatarControllerWithClassDefinition(
            Naf.AvatarDefinition avatar,
            Transform root = null)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize GeniesAvatarsSdk");
                }

                if (avatar != null)
                {
                    // First resolve pipeline information for all asset ids (including tattoos)
                    IAssetIdConverter converter = ServiceManager.Get<IAssetIdConverter>();
                    var assetsToResolve = new List<string>(avatar.equippedAssetIds);
                    assetsToResolve.AddRange(avatar.equippedTattooIds.Values);
                    await converter.ResolveAssetsAsync(assetsToResolve);
                }

                // Then convert IDs before passing json
                var convertedJson = await ConvertAndRemoveInvalidIds(avatar);

                IAssetParamsService paramsService = ServiceManager.GetService<IAssetParamsService>(null);
                NativeUnifiedGenieController controller = await AvatarControllerFactory.CreateSimpleNafGenie(convertedJson, root, paramsService);

                if (controller != null)
                {
                    LoadedAvatar?.Invoke(IsDefinitionDefault(controller));
                }

                return new GeniesAvatar(controller.Genie, controller);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to load avatar controller with class definition: {ex.Message}");
                return default;
            }
        }

        #endregion

        #region Helpers

        private static bool IsDefinitionDefault(NativeUnifiedGenieController controller)
        {
            controller.GetBodyVariation();
            var defaultDefinitionForGender = NafAvatarExtensions.GetDefaultDefinitionForGender(controller.GetBodyVariation()).SerializeDefinition();
            return controller.GetDefinition() == defaultDefinitionForGender;
        }

        private static bool IsDefinitionDefault(GeniesAvatar avatar)
        {
            var defaultDefinitionForGender = NafAvatarExtensions.GetDefaultDefinitionForGender(avatar.GetBodyVariation()).SerializeDefinition();
            return avatar.GetDefinition() == defaultDefinitionForGender;
        }

        // TEMPORARY METHODS...
        private static async UniTask<string> ConvertAndRemoveInvalidIds(Genies.Naf.AvatarDefinition definition)
        {
            // return JsonConvert.SerializeObject(definition);
            if (definition == null)
            {
                return null;
            }

            IAssetIdConverter converter = ServiceManager.GetService<IAssetIdConverter>(null);

            List<string> assetIds = definition.equippedAssetIds;

            if (assetIds == null)
            {
                return JsonConvert.SerializeObject(definition);
            }

            // Convert assets to Universal IDs
            var newIdMappings = await converter.ConvertToUniversalIdsAsync(assetIds);
            if (newIdMappings.Count == assetIds.Count)
            {
                definition.equippedAssetIds = newIdMappings.Values.ToList();
            }
            else
            {
                CrashReporter.LogError("Failed to convert asset ids in avatar definition");
            }

            return JsonConvert.SerializeObject(definition);

        }

        /// <summary>
        /// Instantiates the default controller prefab and optionally parents it under <paramref name="parent"/>.
        /// Returns the instantiated GeniesAvatarController.
        /// </summary>
        public static GeniesAvatarController InstantiateDefaultController(GeniesAvatar avatar)
        {
            var prefab = Resources.Load<GeniesAvatarController>(DefaultControllerResource);
            if (prefab == null)
            {
                Debug.LogError($"Controller prefab not found.");
                return null;
            }

            // If the avatar was parented to something, make sure we keep a reference to it so we can preserve the avatars location relative to its parent
            var currentAvatarParent = avatar.Root.gameObject.transform.parent;

            // Instantiate our loaded prefab with whatever the avatar parent was
            var instance = GameObject.Instantiate(prefab);
            instance.transform.position = currentAvatarParent.position;

            // Now, set the parent of the avatar to be the loaded controller instance
            avatar.Root.gameObject.transform.SetParent(instance.transform);

            // Since we set the new parent to the same local position as the avatar, we can just set this to 0.
            avatar.Root.gameObject.transform.localPosition = Vector3.zero;

            // Setup animation bridge.
            var animatorEventBridge = avatar.Root.gameObject.AddComponent<GeniesAnimatorEventBridge>();
            instance.SetAnimatorEventBridge(animatorEventBridge);

            return instance;
        }

        #endregion
    }
}
