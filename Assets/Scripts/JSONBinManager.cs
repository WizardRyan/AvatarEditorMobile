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

    public void UploadJSON(string binName, string jsonPayload, Action<bool, string> onComplete = null)
    {
        StartCoroutine(UploadRoutine(binName, jsonPayload, onComplete));
    }

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
        string targetBinId = null;
        string lastBinId = null; // Track the last bin ID for pagination
        bool keepSearching = true;
        int pageCount = 0;
        const int BINS_PER_PAGE = 10; // JSONBin returns 10 bins per page by default

        Debug.Log($"[JSONBin] Starting search for '{binName}'...");

        while (keepSearching)
        {
            pageCount++;
            
            // --- URL CONSTRUCTION ---
            // First page: /c/{COLLECTION_ID}/bins
            // Subsequent pages: /c/{COLLECTION_ID}/bins/{LAST_BIN_ID}
            string requestUrl = string.IsNullOrEmpty(lastBinId)
                ? $"{BASE_URL}/c/{COLLECTION_ID}/bins"
                : $"{BASE_URL}/c/{COLLECTION_ID}/bins/{lastBinId}";
            
            Debug.Log($"[JSONBin] Fetching Page {pageCount}: {requestUrl}");

            using (UnityWebRequest getCollectionReq = UnityWebRequest.Get(requestUrl))
            {
                getCollectionReq.SetRequestHeader("X-Master-Key", API_KEY);
                
                yield return getCollectionReq.SendWebRequest();

                if (getCollectionReq.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to fetch page {pageCount}: {getCollectionReq.error}");
                    onComplete?.Invoke(false, null);
                    yield break;
                }

                string rawJson = getCollectionReq.downloadHandler.text;

                // Empty array means no more results
                if (rawJson == "[]" || string.IsNullOrEmpty(rawJson))
                {
                    Debug.Log($"[JSONBin] Reached end of collection (empty response)");
                    keepSearching = false;
                    break;
                }

                // Wrap response for parsing
                string wrappedJson = "{\"bins\":" + rawJson + "}";
                WrapperArray response = JsonUtility.FromJson<WrapperArray>(wrappedJson);

                if (response != null && response.bins != null && response.bins.Length > 0)
                {
                    Debug.Log($"[JSONBin] Page {pageCount} returned {response.bins.Length} bins");
                    
                    // Search current page for matching name
                    foreach (var bin in response.bins)
                    {
                        string foundName = (bin.snippetMeta != null) ? bin.snippetMeta.name : "";
                        
                        if (foundName == binName)
                        {
                            targetBinId = bin.record;
                            Debug.Log($"[JSONBin] Found bin '{binName}' with ID: {targetBinId}");
                            keepSearching = false; 
                            break;
                        }
                    }

                    // If not found and we got a full page, prepare for next page
                    if (keepSearching)
                    {
                        if (response.bins.Length < BINS_PER_PAGE)
                        {
                            // Partial page means we've reached the end
                            Debug.Log($"[JSONBin] Reached end of collection (partial page: {response.bins.Length} bins)");
                            keepSearching = false;
                        }
                        else
                        {
                            // Use the last bin's ID for the next request
                            lastBinId = response.bins[response.bins.Length - 1].record;
                            Debug.Log($"[JSONBin] Next page will use lastBinId: {lastBinId}");
                        }
                    }
                }
                else
                {
                    Debug.Log($"[JSONBin] No bins returned on page {pageCount}");
                    keepSearching = false;
                }
            }
        }

        if (string.IsNullOrEmpty(targetBinId))
        {
            Debug.LogError($"Could not find a bin named '{binName}' after checking {pageCount} pages.");
            onComplete?.Invoke(false, null);
            yield break;
        }

        // --- Download Bin Data ---
        string binUrl = $"{BASE_URL}/b/{targetBinId}";
        using (UnityWebRequest getBinReq = UnityWebRequest.Get(binUrl))
        {
            getBinReq.SetRequestHeader("X-Master-Key", API_KEY);
            getBinReq.SetRequestHeader("X-Bin-Meta", "false"); 

            yield return getBinReq.SendWebRequest();

            if (getBinReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download bin data: {getBinReq.error}");
                onComplete?.Invoke(false, null);
            }
            else
            {
                Debug.Log($"Successfully downloaded data for: {binName}");
                onComplete?.Invoke(true, getBinReq.downloadHandler.text);
            }
        }
    }

    // ---------------- PARSING DATA STRUCTURES ---------------- //

    [Serializable]
    private class WrapperArray
    {
        public BinMetaInfo[] bins;
    }

    [Serializable]
    private class BinMetaInfo
    {
        public string record; 
        public SnippetMeta snippetMeta;
    }

    [Serializable]
    private class SnippetMeta
    {
        public string name;
    }
}