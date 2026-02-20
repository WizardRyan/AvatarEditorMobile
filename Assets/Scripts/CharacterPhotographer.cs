using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

#if UNITY_EDITOR
using UnityEditor; // Required to refresh the Assets view automatically
#endif

public class CharacterPhotographer : MonoBehaviour
{
    [Header("Stable References")]
    [Tooltip("The parent object that will contain the spawned NativeGenie(Clone).")]
    [SerializeField] private Transform _avatarSpawnParent;

    [Tooltip("The Head transform. (If this is also spawned at runtime, you must assign this via script before capturing).")]
    [SerializeField] private Transform _playerCameraTarget; 

    [Header("Runtime Search Settings")]
    [Tooltip("The name of the spawned child object that has the correct rotation.")]
    [SerializeField] private string _runtimeObjectName = "NativeGenie(Clone)";

    [Header("Distance Settings")]
    [SerializeField] private float _portraitDistance = 0.8f;
    [SerializeField] private float _bodyDistance = 1.5f;
    [SerializeField] private float _torsoHeightOffset = -0.45f;

    [Header("Capture Settings")]
    [SerializeField] private Vector2Int _resolution = new Vector2Int(1024, 1024);
    
    // Optional: Add a subfolder to keep Resources clean
    [SerializeField] private string _subFolder = "CharacterShots"; 

    public async Task<List<string>> CaptureAllShotsAsync()
    {
        // 1. Find the runtime object (The Orientation Source)
        Transform orientationSource = _avatarSpawnParent.Find(_runtimeObjectName);

        // Safety Check: If exact name fails, try finding the first active child
        if (orientationSource == null && _avatarSpawnParent.childCount > 0)
        {
            Debug.LogWarning($"Could not find exact child '{_runtimeObjectName}'. Using first child instead.");
            orientationSource = _avatarSpawnParent.GetChild(0);
        }

        if (orientationSource == null)
        {
            Debug.LogError($"FAILED: Could not find '{_runtimeObjectName}' inside '{_avatarSpawnParent.name}'. Cannot determine facing direction.");
            return null;
        }

        if (_playerCameraTarget == null)
        {
            Debug.LogError("FAILED: _playerCameraTarget (Head) is null. Assign it before capturing.");
            return null;
        }

        // 2. Start the Coroutine with the found transform
        var tcs = new TaskCompletionSource<List<string>>();
        StartCoroutine(CaptureRoutine(tcs, orientationSource));
        return await tcs.Task;
    }

    private IEnumerator CaptureRoutine(TaskCompletionSource<List<string>> tcs, Transform orientationSource)
    {
        List<string> savedFiles = new List<string>();

        // Setup Camera
        GameObject camObj = new GameObject("PhotoCamera");
        Camera photoCam = camObj.AddComponent<Camera>();
        photoCam.targetTexture = new RenderTexture(_resolution.x, _resolution.y, 24);

        // --- CALCULATE VECTORS ---
        Vector3 modelForward = Vector3.ProjectOnPlane(orientationSource.forward, Vector3.up).normalized;
        Vector3 modelRight = Vector3.Cross(Vector3.up, modelForward); 

        // Get Positions
        Vector3 headPos = _playerCameraTarget.position;
        Vector3 torsoPos = headPos + (Vector3.up * _torsoHeightOffset);

        var shots = new List<ShotDefinition>();

        // --- SHOT 1: PORTRAIT ---
        shots.Add(new ShotDefinition {
            Name = "Portrait",
            Position = headPos + (modelForward * _portraitDistance),
            LookAtTarget = headPos
        });

        // --- SHOT 2: BODY FRONT ---
        shots.Add(new ShotDefinition {
            Name = "BodyFront",
            Position = torsoPos + (modelForward * _bodyDistance),
            LookAtTarget = torsoPos
        });

        // --- SHOT 3: BODY SIDE ---
        shots.Add(new ShotDefinition {
            Name = "BodySide",
            Position = torsoPos + (modelRight * _bodyDistance) + (modelForward * 0.5f), 
            LookAtTarget = torsoPos
        });

        string fullSavePath;
        #if UNITY_EDITOR
            fullSavePath = Path.Combine(Application.dataPath, "Resources", _subFolder);
        #else
            fullSavePath = Path.Combine(Application.persistentDataPath, _subFolder);
        #endif

        // Ensure the directory exists
        if (!Directory.Exists(fullSavePath))
        {
            Directory.CreateDirectory(fullSavePath);
        }

        // Execute Shots
        foreach (var shot in shots)
        {
            photoCam.transform.position = shot.Position;
            photoCam.transform.LookAt(shot.LookAtTarget);

            yield return new WaitForEndOfFrame();

            RenderTexture.active = photoCam.targetTexture;
            photoCam.Render();
            
            Texture2D image = new Texture2D(photoCam.targetTexture.width, photoCam.targetTexture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, photoCam.targetTexture.width, photoCam.targetTexture.height), 0, 0);
            image.Apply();

            byte[] bytes = image.EncodeToPNG();
            
            // Save file inside the folder
            string fileName = $"Captured_{shot.Name}.png";
            string path = Path.Combine(fullSavePath, fileName);
            
            File.WriteAllBytes(path, bytes);
            savedFiles.Add(path);

            Destroy(image); 
        }

        // Cleanup
        photoCam.targetTexture.Release();
        Destroy(camObj);

        // --- REFRESH ASSET DATABASE (EDITOR ONLY) ---
        // This makes the files appear in Unity immediately without needing to click away
#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
        
        Debug.Log($"<color=green>Captured {savedFiles.Count} images to: {fullSavePath}</color>");
        tcs.SetResult(savedFiles);
    }

    private class ShotDefinition
    {
        public string Name;
        public Vector3 Position;
        public Vector3 LookAtTarget;
    }
}