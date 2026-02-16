using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Threading.Tasks;

public class ImgBBManager : MonoBehaviour
{
    // GET YOUR KEY HERE: https://api.imgbb.com/
    [SerializeField] private string _apiKey = "f51dbe7ed49d8d5790efba5e4c2d3429";
    private const string UPLOAD_URL = "https://api.imgbb.com/1/upload";

    [System.Serializable]
    private class ImgBBResponse
    {
        public ImgBBData data;
        public bool success;
    }

    [System.Serializable]
    private class ImgBBData
    {
        public string url;
    }

    /// <summary>
    /// Uploads an image from a local file path and returns the hosted URL.
    /// </summary>
    public async Task<string> UploadImageAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return null;
        }

        // 1. Convert File to Base64
        byte[] fileBytes = File.ReadAllBytes(filePath);
        string base64Image = System.Convert.ToBase64String(fileBytes);

        // 2. Prepare Form
        WWWForm form = new WWWForm();
        form.AddField("key", _apiKey);
        form.AddField("image", base64Image);
        
        // Optional: Set expiration (in seconds) if you want them to auto-delete
        // form.AddField("expiration", "600"); 

        // 3. Send Request
        using (UnityWebRequest www = UnityWebRequest.Post(UPLOAD_URL, form))
        {
            var operation = www.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"ImgBB Upload Error: {www.error} \nResponse: {www.downloadHandler.text}");
                return null;
            }
            else
            {
                var response = JsonUtility.FromJson<ImgBBResponse>(www.downloadHandler.text);
                if (response.success && response.data != null)
                {
                    return response.data.url;
                }
                return null;
            }
        }
    }
}