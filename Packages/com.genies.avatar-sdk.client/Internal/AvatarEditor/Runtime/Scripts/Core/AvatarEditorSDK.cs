using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Genies.Avatars;
using Genies.Avatars.Sdk;
using Genies.CrashReporting;
using Genies.Naf;
using GnWrappers;
using Genies.ServiceManagement;

namespace Genies.AvatarEditor.Core
{
    /// <summary>
    /// Gender types for avatar body configuration
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal enum GenderType
#else
    public enum GenderType
#endif
    {
        Male,
        Female,
        Androgynous
    }

    /// <summary>
    /// Body size types for avatar body configuration
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal enum BodySize
#else
    public enum BodySize
#endif
    {
        Skinny,
        Medium,
        Heavy
    }

    /// <summary>
    /// Static convenience facade for opening and closing the Avatar Editor.
    /// - Auto-initializes required services on first use
    /// - Provides public static methods for opening and closing the editor
    /// - Follows the same pattern as GeniesAvatarsSdk for consistency
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal static class AvatarEditorSDK
#else
    public static class AvatarEditorSDK
#endif
    {
        private static bool IsInitialized =>
            InitializationCompletionSource is not null
            && InitializationCompletionSource.Task.Status == UniTaskStatus.Succeeded;
        private static UniTaskCompletionSource InitializationCompletionSource { get; set; }

        private static IAvatarEditorSdkService CachedService { get; set; }
        private static bool EventsSubscribed { get; set; }

        /// <summary>
        /// Event raised when the editor is opened.
        /// </summary>
        public static event Action EditorOpened = delegate { };

        /// <summary>
        /// Event raised when the editor is closed.
        /// </summary>
        public static event Action EditorClosed = delegate { };

        /// <summary>
        /// Event raised when an asset is equipped.
        /// Payload: wearableId
        /// </summary>
        public static event Action<string> EquippedAsset = delegate { };

        /// <summary>
        /// Event raised when an asset is unequipped.
        /// Payload: wearableId
        /// </summary>
        public static event Action<string> UnequippedAsset = delegate { };

        /// <summary>
        /// Event raised when a skin color is set.
        /// </summary>
        public static event Action SkinColorSet = delegate { };

        /// <summary>
        /// Event raised when a tattoo is equipped.
        /// Payload: tattooId
        /// </summary>
        public static event Action<string> TattooEquipped = delegate { };

        /// <summary>
        /// Event raised when a tattoo is unequipped.
        /// Payload: tattooId
        /// </summary>
        public static event Action<string> TattooUnequipped = delegate { };

        /// <summary>
        /// Event raised when a native avatar body preset is applied.
        /// </summary>
        public static event Action BodyPresetSet = delegate { };

        /// <summary>
        /// Event raised when avatar body type is set (gender + body size).
        /// </summary>
        public static event Action BodyTypeSet = delegate { };

        /// <summary>
        /// Event raised when an avatar definition is saved (local or cloud depending on mode).
        /// </summary>
        public static event Action AvatarDefinitionSaved = delegate { };

        /// <summary>
        /// Event raised when an avatar definition is saved locally.
        /// </summary>
        public static event Action AvatarDefinitionSavedLocally = delegate { };

        /// <summary>
        /// Event raised when an avatar definition is saved to cloud.
        /// </summary>
        public static event Action AvatarDefinitionSavedToCloud = delegate { };

        /// <summary>
        /// Event raised when the editor save option is changed.
        /// </summary>
        public static event Action EditorSaveOptionSet = delegate { };

        /// <summary>
        /// Event raised when editor save settings are changed.
        /// </summary>
        public static event Action EditorSaveSettingsSet = delegate { };

        /// <summary>
        /// Event raised when an avatar is loaded for editing.
        /// </summary>
        public static event Action AvatarLoadedForEditing = delegate { };

        #region Initialization / Service Access

        public static async UniTask<bool> InitializeAsync()
        {
            return await AvatarEditorInitializer.Instance.InitializeAsync();
        }

        internal static async UniTask<IAvatarEditorSdkService> GetOrCreateAvatarEditorSdkInstance()
        {
            if (await InitializeAsync() is false)
            {
                CrashReporter.LogError("Avatar editor could not be initialized.");
                return default;
            }

            var service = ServiceManager.Get<IAvatarEditorSdkService>();
            SubscribeToServiceEvents(service);
            return service;
        }

        private static void SubscribeToServiceEvents(IAvatarEditorSdkService service)
        {
            if (service == null)
            {
                return;
            }

            if (ReferenceEquals(service, CachedService)
                && EventsSubscribed)
            {
                return;
            }

            CachedService = service;
            CachedService.EditorOpened += OnEditorOpened;
            CachedService.EditorClosed += OnEditorClosed;

            EventsSubscribed = true;
        }

        private static void OnEditorOpened()
        {
            EditorOpened?.Invoke();
        }

        private static void OnEditorClosed()
        {
            EditorClosed?.Invoke();
        }

        #endregion

        #region Public Static API

        public static async UniTask OpenEditorAsync(GeniesAvatar avatar, Camera camera = null)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.OpenEditorAsync(avatar, camera);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to open avatar editor: {ex.Message}");
            }
        }

        public static async UniTask CloseEditorAsync(bool revertAvatar)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.CloseEditorAsync(revertAvatar);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to close avatar editor: {ex.Message}");
            }
        }

        public static GeniesAvatar GetCurrentActiveAvatar()
        {
            try
            {
                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                return avatarEditorSdkService?.GetCurrentActiveAvatar();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to get current active avatar: {ex.Message}");
                return null;
            }
        }

        public static bool IsEditorOpen
        {
            get
            {
                try
                {
                    var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                    return avatarEditorSdkService?.IsEditorOpen ?? false;
                }
                catch (Exception ex)
                {
                    CrashReporter.LogError($"Failed to get editor open state: {ex.Message}");
                    return false;
                }
            }
        }

        public static async UniTask<List<WearableAssetInfo>> GetWearableAssetInfoListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                return await avatarEditorSdkService.GetWearableAssetInfoListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to get wearable asset info list: {ex.Message}");
                return new List<WearableAssetInfo>();
            }
        }

        public static async UniTask<(bool, string)> GiveAssetToUserAsync(string assetId)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                return await avatarEditorSdkService.GiveAssetToUserAsync(assetId);
            }
            catch (Exception ex)
            {
                string error = $"Failed to give asset to user: {ex.Message}";
                CrashReporter.LogError(error);
                return (false, error);
            }
        }

        public static async UniTask<List<WearableAssetInfo>> GetUsersAssetsAsync()
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                return await avatarEditorSdkService.GetUsersAssetsAsync();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to get wearable asset info list: {ex.Message}");
                return new List<WearableAssetInfo>();
            }
        }

        public static async UniTask EquipOutfitAsync(GeniesAvatar avatar, string wearableId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.EquipOutfitAsync(avatar, wearableId, cancellationToken);
                EquippedAsset?.Invoke(wearableId);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to equip outfit: {ex.Message}");
            }
        }

        public static async UniTask UnEquipOutfitAsync(GeniesAvatar avatar, string wearableId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.UnEquipOutfitAsync(avatar, wearableId, cancellationToken);
                UnequippedAsset?.Invoke(wearableId);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to equip outfit: {ex.Message}");
            }
        }

        public static async UniTask SetSkinColorAsync(GeniesAvatar avatar, Color skinColor, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.SetSkinColorAsync(avatar, skinColor, cancellationToken);
                SkinColorSet?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to equip skin color: {ex.Message}");
            }
        }

        public static async UniTask EquipTattooAsync(GeniesAvatar avatar, string tattooId, MegaSkinTattooSlot tattooSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.EquipTattooAsync(avatar, tattooId, tattooSlot, cancellationToken);
                TattooEquipped?.Invoke(tattooId);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to equip tattoo: {ex.Message}");
            }
        }

        public static async UniTask UnEquipTattooAsync(GeniesAvatar avatar, string tattooId, MegaSkinTattooSlot tattooSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.UnEquipTattooAsync(avatar, tattooId, tattooSlot, cancellationToken);
                TattooUnequipped?.Invoke(tattooId);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to unequip tattoo: {ex.Message}");
            }
        }

        public static async UniTask SetNativeAvatarBodyPresetAsync(GeniesAvatar avatar, GSkelModifierPreset preset, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.SetNativeAvatarBodyPresetAsync(avatar, preset, cancellationToken);
                BodyPresetSet?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set body preset: {ex.Message}");
            }
        }

        public static async UniTask SetAvatarBodyTypeAsync(GeniesAvatar avatar, GenderType genderType, BodySize bodySize, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.SetAvatarBodyTypeAsync(avatar, genderType, bodySize, cancellationToken);
                BodyTypeSet?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set avatar body type: {ex.Message}");
            }
        }

        public static async UniTask SaveAvatarDefinitionAsync(GeniesAvatar avatar)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.SaveAvatarDefinitionAsync(avatar);
                AvatarDefinitionSaved?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to save avatar definition: {ex.Message}");
            }
        }

        public static async UniTask SaveAvatarDefinitionLocallyAsync(GeniesAvatar avatar, string profileId = null)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                avatarEditorSdkService.SaveAvatarDefinitionLocally(avatar, profileId);
                AvatarDefinitionSavedLocally?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to save avatar definition locally: {ex.Message}");
            }
        }

        public static async UniTask SaveAvatarDefinitionToCloudAsync(GeniesAvatar avatar)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                await avatarEditorSdkService.SaveAvatarDefinitionAsync(avatar);
                AvatarDefinitionSavedToCloud?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to save avatar definition to cloud: {ex.Message}");
            }
        }

        public static async UniTask<GeniesAvatar> LoadFromLocalAvatarDefinitionAsync(string profileId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                var avatar = await avatarEditorSdkService.LoadFromLocalAvatarDefinitionAsync(profileId, cancellationToken);
                if (avatar == null)
                {
                    CrashReporter.LogError($"Failed to load avatar with profileId: {profileId}");
                    return null;
                }

                AvatarLoadedForEditing?.Invoke();
                return avatar;
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to load avatar from definition: {ex.Message}");
                return null;
            }
        }

        public static async UniTask<GeniesAvatar> LoadFromLocalGameObjectAsync(string profileId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
                var avatar = await avatarEditorSdkService.LoadFromLocalGameObjectAsync(profileId, cancellationToken);
                if (avatar == null)
                {
                    CrashReporter.LogError($"Failed to load avatar from game object: {profileId}");
                    return null;
                }

                AvatarLoadedForEditing?.Invoke();
                return avatar;
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to load avatar definition: {ex.Message}");
                return null;
            }
        }

        public static async UniTask SetEditorSaveOptionAsync(AvatarSaveOption saveOption)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    CrashReporter.LogError("AvatarEditorSdkService not found. Cannot set save option.");
                    return;
                }

                avatarEditorSdkService.SetEditorSaveOption(saveOption);
                EditorSaveOptionSet?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set editor save option: {ex.Message}");
            }
        }

        public static async UniTask SetEditorSaveOptionAsync(AvatarSaveOption saveOption, string profileId)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    throw new NullReferenceException("AvatarEditorSdkService not found");
                }

                avatarEditorSdkService.SetEditorSaveOption(saveOption, profileId);
                EditorSaveOptionSet?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set editor save option: {ex.Message}");
            }
        }

        public static async UniTask SetEditorSaveSettingsAsync(AvatarSaveSettings saveSettings)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    throw new NullReferenceException("AvatarEditorSdkService not found");
                }

                avatarEditorSdkService.SetEditorSaveSettings(saveSettings);
                EditorSaveSettingsSet?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set editor save settings: {ex.Message}");
            }
        }

        public static async UniTask SetSaveAndExitButtonStatusAsync(bool enableSaveButton, bool enableExitButton)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    throw new NullReferenceException("AvatarEditorSdkService not found");
                }

                avatarEditorSdkService.SetSaveAndExitButtonStatus(enableSaveButton, enableExitButton);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set Save and Exit ActionBarFlags: {ex.Message}");
            }
        }

        public static void SetSaveAndExitButtonStatus(bool enableSaveButton, bool enableExitButton)
        {
            try
            {
                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    CrashReporter.LogWarning("AvatarEditorSdkService not found. Make sure the SDK is initialized before calling this method.");
                    return;
                }

                avatarEditorSdkService.SetSaveAndExitButtonStatus(enableSaveButton, enableExitButton);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set Save and Exit ActionBarFlags: {ex.Message}");
            }
        }

        #endregion
    }
}
