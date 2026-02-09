using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class JSONBinManager : MonoBehaviour
{
    // ---------------- CONFIGURATION ---------------- //
    private const string API_KEY = "$2a$10$o5DEKrYiTFH36FVh/9LfMupxAwZXhKwDJhKPcx9s1mqqZPcyesh/S";
    private const string COLLECTION_ID = "69892ca543b1c97be96fce6f";
    // ----------------------------------------------- //

    private const string BASE_URL = "https://api.jsonbin.io/v3";

    /// <summary>
    /// Uploads a JSON string to the collection with a specific bin name.
    /// </summary>
    public void UploadJSON(string binName, string jsonPayload, Action<bool, string> onComplete = null)
    {
        StartCoroutine(UploadRoutine(binName, jsonPayload, onComplete));
    }

    /// <summary>
    /// Downloads a JSON string from the collection by searching for its bin name.
    /// </summary>
    public void DownloadJSONByName(string binName, Action<bool, string> onComplete)
    {
        StartCoroutine(DownloadByNameRoutine(binName, onComplete));
    }

    private IEnumerator UploadRoutine(string binName, string json, Action<bool, string> onComplete)
    {
        string url = $"{BASE_URL}/b";
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Master-Key", API_KEY);
            request.SetRequestHeader("X-Collection-Id", COLLECTION_ID);
            
            // Assign the searchable name
            request.SetRequestHeader("X-Bin-Name", binName); 

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Upload Error: {request.error}\n{request.downloadHandler.text}");
                onComplete?.Invoke(false, request.error);
            }
            else
            {
                Debug.Log($"Successfully uploaded: {binName}");
                onComplete?.Invoke(true, request.downloadHandler.text);
            }
        }
    }

    private IEnumerator DownloadByNameRoutine(string binName, Action<bool, string> onComplete)
    {
        // --- STEP 1: Get all bins in the collection ---
        string collectionUrl = $"{BASE_URL}/c/{COLLECTION_ID}/bins";
        
        string targetBinId = null;

        using (UnityWebRequest getCollectionReq = UnityWebRequest.Get(collectionUrl))
        {
            getCollectionReq.SetRequestHeader("X-Master-Key", API_KEY);
            yield return getCollectionReq.SendWebRequest();

            if (getCollectionReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to fetch collection: {getCollectionReq.error}");
                onComplete?.Invoke(false, null);
                yield break; // Stop execution here
            }

            // JSONBin returns a raw JSON array. JsonUtility needs a root object.
            // We wrap the raw array into a mock object: {"bins": [ ... ]}
            string jsonArray = getCollectionReq.downloadHandler.text;
            string wrappedJson = "{\"bins\":" + jsonArray + "}";
            
            CollectionResponse response = JsonUtility.FromJson<CollectionResponse>(wrappedJson);
            
            // Search for the matching name
            if (response != null && response.bins != null)
            {
                foreach (var bin in response.bins)
                {
                    if (bin.snippetMeta.name == binName)
                    {
                        targetBinId = bin.record;
                        break;
                    }
                }
            }
        } // The 'using' block automatically cleans up getCollectionReq from memory

        if (string.IsNullOrEmpty(targetBinId))
        {
            Debug.LogError($"Could not find a bin named '{binName}' in the collection.");
            onComplete?.Invoke(false, null);
            yield break;
        }

        // --- STEP 2: Download the actual bin data using the Bin ID ---
        string binUrl = $"{BASE_URL}/b/{targetBinId}";
        using (UnityWebRequest getBinReq = UnityWebRequest.Get(binUrl))
        {
            getBinReq.SetRequestHeader("X-Master-Key", API_KEY);
            
            // This header tells JSONBin to ONLY return our raw data, hiding their metadata wrapper
            getBinReq.SetRequestHeader("X-Bin-Meta", "false"); 

            yield return getBinReq.SendWebRequest();

            if (getBinReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download bin data: {getBinReq.error}");
                onComplete?.Invoke(false, null);
            }
            else
            {
                string finalJsonData = getBinReq.downloadHandler.text;
                Debug.Log($"Successfully downloaded data for: {binName}");
                onComplete?.Invoke(true, finalJsonData);
            }
        }
    }

    // ---------------- PARSING DATA STRUCTURES ---------------- //
    // These classes exist solely to trick Unity into parsing the 
    // metadata array returned by JSONBin during Step 1.

    [Serializable]
    private class CollectionResponse
    {
        public BinMetaInfo[] bins;
    }

    [Serializable]
    private class BinMetaInfo
    {
        public string record; // This is the actual Bin ID
        public SnippetMeta snippetMeta;
    }

    [Serializable]
    private class SnippetMeta
    {
        public string name;
    }
}