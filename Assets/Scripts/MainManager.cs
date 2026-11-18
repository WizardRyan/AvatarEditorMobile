using UnityEngine;
using UnityEngine.UI;
using Genies.Sdk;
using System.Threading.Tasks;
using Genies.Sdk.Samples.AvatarStarter;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using Unity.VectorGraphics;
using UnityEngine.SceneManagement;
using Genies.Sdk.Samples.MultipleAvatars;
using Genies.Services.Api;
using NUnit.Framework.Internal;
using System.Linq;

[System.Serializable]
public class TestRun 
{
    public string Participant_id;
    public long start_ts;
    public long end_ts;
    public double time_elapsed_s;
    public int num_actions_taken;
    // public int base_gender;
    public int perceived_success;
    public int perceived_difficulty;
    public int target_image;
    public string platform = "mobile";
    public string avatar_definition;
    public string initial_avatar_definition;   // definition at the start of editing
    public bool default_avatar_applied;        // false = participant started from the account (purple) avatar
    public string final_image_front;
    public string final_image_side;
    public string final_image_portrait;

    public List<EditorLogEvent> action_log = new List<EditorLogEvent>();
}

[System.Serializable]
public class EditorLogEvent 
{
    public enum ActionType
    {
        select_category,
        select_color,
        select_customization_option,
        rotate_view,
    }

    public long Timestamp;
    public string Action_type;
    public string Parameter;
    public string New_Value;

    public void ExtractParam(string oldAvatarDefinition, string newAvatarDefinition)
    {
        //
    }

    public EditorLogEvent()
    {
        
    }
}

public enum CategoryType
{
    Shirts,
    Outerwear,
    Bottoms,
    Pants,
    Shorts,
    Skirts,
    Dresses,
    Shoes,
    Accessories,
    Earrings,
    Glasses,
    Hats,
    Masks,
    Body,
    Face,
    Eyes,
    Jawline,
    Lips,
    Nose,
    Brows,
    Lashes,
    Hair,
    FacialHair,
    Makeup,
    BeautyMarks,
    Blush,
    Eyeshadow,
    Gems,
    Lipstick,
    Stickers,
    Tattoos,
    AboveKnee,
    Belly,
    BelowKnee,
    Bicecp,
    Calf,
    Forearm,
    LowerBack,
    Thigh
}

public enum Gender
{
    NONBINARY,
    MALE,
    FEMALE
}
public class MainManager : MonoBehaviour
{

    [SerializeField] private JSONBinManager _JSONBinManager;
    [SerializeField] private LoadMyAvatar _loadMyAvatar;
    [SerializeField] private UIManager _UIManager;
    [SerializeField] private CharacterPhotographer _characterPhotographer;
    [SerializeField] private ImgBBManager _imgBBManager;

    private TestRun _defaultAvatar;
    private ManagedAvatar _managedAvatar;
    private List<string> _savedFilePaths; // [0]=Portrait, [1]=Front, [2]=Side

    private TestRun _testRun = new TestRun();
    private DateTime _startTime;
    private DateTime _endTime;

    private string _lastKnownDefinition;
    private Coroutine _definitionPollCoroutine;

    void Start()
    {
        LoadDefaultAvatar(Gender.NONBINARY);
    }

    void Update()
    {
        
    }

    public void UploadButtonPressed()
    {
        UploadButtonPressedAsync();
    }

    public void FinishedEditingPressed()
    {
        FinishedEditingPressedAsync();
    }

    public void NewExperimentPressed()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private async Task FinishedEditingPressedAsync()
    {
        try
        {
            StopDefinitionPolling();

            Debug.Log("1. Capturing Screenshots...");
            // Returns list of file paths: [0]=Portrait, [1]=Front, [2]=Side
            _savedFilePaths = await _characterPhotographer.CaptureAllShotsAsync();
            Debug.Log("2. Processing Test Run Data...");
            ProcessTestRun();
            
            await AvatarSdk.CloseAvatarEditorAsync(true);
            _UIManager.ShowSurvey();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during finishing editing: {e.Message}");
        }
    }

    private async Task UploadButtonPressedAsync()
    {

        SetSurveyData();
        
        _UIManager.ShowUploadInProgress();

        Debug.Log("3. Uploading Images to ImgBB...");
        await UploadImages();

        var testJSON = JsonUtility.ToJson(_testRun);
        
        Debug.Log("4. Uploading JSON Data to JSONBin...");
        var jsonUploadSuccess = await UploadJsonAsyncWrapper(_testRun.Participant_id, testJSON);

        if(jsonUploadSuccess)
        {
            Debug.Log("Process Complete: Images hosted & JSON saved.");
            _UIManager.ShowUploadSuccess();
        }
        else
        {
            _UIManager.ShowUploadFailure();
        }
    }

    private async Task UploadImages()
    {
        Task<string> taskPortrait = UploadImageWithRetry(_savedFilePaths[0]);
        Task<string> taskFront    = UploadImageWithRetry(_savedFilePaths[1]);
        Task<string> taskSide     = UploadImageWithRetry(_savedFilePaths[2]);

        await Task.WhenAll(taskPortrait, taskFront, taskSide);

        _testRun.final_image_portrait = taskPortrait.Result;
        _testRun.final_image_front    = taskFront.Result;
        _testRun.final_image_side     = taskSide.Result;

        Debug.Log($"Images Uploaded! Portrait URL: {_testRun.final_image_portrait}");
    }

    private async Task<string> UploadImageWithRetry(string filePath, int maxAttempts = 3)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            string url = await _imgBBManager.UploadImageAsync(filePath);
            if (url != null) return url;
            Debug.LogWarning($"Image upload attempt {attempt}/{maxAttempts} failed: {filePath}");
        }
        return null;
    }

    private Task<bool> UploadJsonAsyncWrapper(string binName, string jsonPayload)
    {
        var tcs = new TaskCompletionSource<bool>();
        _JSONBinManager.UploadJSON(binName, jsonPayload, (success, result) => 
        {
            if(!success) Debug.LogError("JSON Upload Failed");
            tcs.SetResult(success);
        });
        return tcs.Task;
    }

    private string GetAvatarDefinition()
    {
        var avatar = AvatarSdk.GetAvatarEditorAvatar();
        var definition = avatar.GetDefinition();
        return definition;
    }

    private void ProcessTestRun()
    {
        _endTime = DateTime.UtcNow;
        _testRun.start_ts = ((DateTimeOffset)_startTime).ToUnixTimeSeconds();
        _testRun.end_ts = ((DateTimeOffset)_endTime).ToUnixTimeSeconds();
        _testRun.time_elapsed_s = (_endTime - _startTime).TotalSeconds;
        Debug.Log("Time Elapsed (s): " + _testRun.time_elapsed_s);
        _testRun.avatar_definition = GetAvatarDefinition();
        Debug.Log("Got Avatar Definition");
        _testRun.Participant_id = _UIManager.GetParticipantId();
        _testRun.target_image = _UIManager.GetTargetImage();

        // filter out color actions since they are already captured by avatar definition changes
        _testRun.action_log = _testRun.action_log.Where(ev => ev.Action_type != EditorLogEvent.ActionType.select_color.ToString()).ToList();
        // _testRun.base_gender = _UIManager.GetBaseGender();
    }

    private void SetSurveyData()
    {
        _testRun.perceived_difficulty = _UIManager.GetPerceivedDifficulty();
        _testRun.perceived_success = _UIManager.GetPerceivedSuccess();
    }

    // Default avatar bins, fetched directly by id. Looking them up by name meant paging through the
    // whole collection (newest first, so ~26 requests and growing with every participant), which
    // failed under JSONBin rate limiting/outages and left the participant on the account's
    // (purple-haired) avatar.
    private static readonly Dictionary<Gender, (string name, string binId)> DefaultAvatarBins = new Dictionary<Gender, (string, string)>
    {
        { Gender.NONBINARY, ("default-nonbinary", "69929658ae596e708f2e2f19") },
        { Gender.MALE,      ("default-male",      "699298dfd0ea881f40bdd046") },
        { Gender.FEMALE,    ("default-female",    "69929a4bae596e708f2e3604") },
    };
    private const string DefaultCacheKeyPrefix = "cached_default_avatar_";
    private const float DefaultAvatarWaitSeconds = 20f;

    public void LoadDefaultAvatar(Gender gender)
    {
        var (defaultName, binId) = DefaultAvatarBins[gender];
        string cacheKey = DefaultCacheKeyPrefix + defaultName;

        // Use the last successfully downloaded copy right away; the download below refreshes it.
        string cached = PlayerPrefs.GetString(cacheKey, "");
        if (!string.IsNullOrEmpty(cached))
        {
            var cachedRun = JsonUtility.FromJson<TestRun>(cached);
            if (!string.IsNullOrEmpty(cachedRun?.avatar_definition))
            {
                _defaultAvatar = cachedRun;
                Debug.Log($"Using cached default avatar '{defaultName}' until the download completes.");
            }
        }

        _JSONBinManager.DownloadJSONById(binId, (success, result) =>
        {
            if (success)
            {
                var downloaded = JsonUtility.FromJson<TestRun>(result);
                if (!string.IsNullOrEmpty(downloaded?.avatar_definition))
                {
                    _defaultAvatar = downloaded;
                    PlayerPrefs.SetString(cacheKey, result);
                    PlayerPrefs.Save();
                    Debug.Log($"Loaded Default Avatar '{defaultName}' - Participant: {_defaultAvatar.Participant_id}, Platform: {_defaultAvatar.platform}");
                }
                else
                {
                    Debug.LogError($"Default avatar '{defaultName}' downloaded but has no avatar_definition.");
                }
            }
            else
            {
                Debug.LogError(_defaultAvatar != null
                    ? $"Failed to download default avatar '{defaultName}'; using cached copy."
                    : $"Failed to download default avatar '{defaultName}' and no cached copy exists.");
            }
        });
    }

    public void BeginEditingAvatar()
    {
        if (_UIManager.AllValuesFilled())
        {
            BeginEditingAvatarAsync();
        }
    }

    public async Task BeginEditingAvatarAsync()
    {
        try
        {
            // Don't start on the wrong avatar just because the default is still downloading.
            float waited = 0f;
            while (_defaultAvatar == null && waited < DefaultAvatarWaitSeconds)
            {
                await Task.Delay(250);
                waited += 0.25f;
            }

            _managedAvatar = _loadMyAvatar.LoadedAvatar;
            await AvatarSdk.OpenAvatarEditorAsync(_managedAvatar);

            // Record start time immediately — before SetDefinitionAsync, so it's always set
            // even if the definition call fails.
            _testRun.num_actions_taken = 0;
            _startTime = DateTime.UtcNow;
            Debug.Log("Started Timing, Experiment Begins Now");

            bool applied = false;
            if (_defaultAvatar != null && !string.IsNullOrEmpty(_defaultAvatar.avatar_definition))
            {
                try
                {
                    Debug.Log("Setting Default Avatar Definition in Editor..." + _defaultAvatar.avatar_definition);
                    await AvatarSdk.GetAvatarEditorAvatar().SetDefinitionAsync(_defaultAvatar.avatar_definition);
                    applied = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to apply default avatar definition: {e.Message}. Continuing with current definition.");
                }
            }
            else
            {
                Debug.LogError($"Default avatar not available after {DefaultAvatarWaitSeconds}s; participant starts from the account avatar.");
            }

            // Record exactly what the participant started from, so analysis never has to infer it.
            _lastKnownDefinition = GetAvatarDefinition();
            _testRun.initial_avatar_definition = _lastKnownDefinition;
            _testRun.default_avatar_applied = applied;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error while starting the editing session: {e}");
        }
        finally
        {
            // Always log changes, whatever happened above (previously an exception here silently
            // stopped all select_customization_option logging for the session).
            StartDefinitionPolling();
        }
    }

    private void StartDefinitionPolling()
    {
        StopDefinitionPolling();
        _definitionPollCoroutine = StartCoroutine(PollDefinitionCoroutine());
    }

    private void StopDefinitionPolling()
    {
        if (_definitionPollCoroutine != null)
        {
            StopCoroutine(_definitionPollCoroutine);
            _definitionPollCoroutine = null;
        }
    }

    private IEnumerator PollDefinitionCoroutine()
    {
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            yield return wait;

            try
            {
                string currentDefinition = GetAvatarDefinition();

                if (currentDefinition != _lastKnownDefinition)
                {
                    _testRun.num_actions_taken++;
                    AddCustomizationOptionEvent(_lastKnownDefinition, currentDefinition);
                    _lastKnownDefinition = currentDefinition;
                    Debug.Log($"Avatar definition changed! num_actions_taken: {_testRun.num_actions_taken}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Definition poll error: {e.Message}");
            }
        }
    }

    public void AddCustomizationOptionEvent(string lastKnownDefinition, string newDefinition)
    {
        string param = "Shirts";

        foreach (var ev in _testRun.action_log.AsEnumerable().Reverse())
        {
            if(ev.Action_type == EditorLogEvent.ActionType.select_category.ToString())
            {
                param = ev.Parameter;
                break;
            }
            else if(ev.Action_type == EditorLogEvent.ActionType.select_color.ToString())
            {
                param = ev.Parameter;
                break;
            }
        }

        _testRun.action_log.Add(new EditorLogEvent 
        {
            Timestamp = TimeStampNow(),
            Action_type = EditorLogEvent.ActionType.select_customization_option.ToString(),
            Parameter = param,
            New_Value = newDefinition
        });
    }

    public void AddCategoryClickEvent(string categoryName)
    {
        _testRun.action_log.Add(new EditorLogEvent 
        {
            Timestamp = TimeStampNow(),
            Action_type = EditorLogEvent.ActionType.select_category.ToString(),
            Parameter = categoryName,
            New_Value = ""
        });
        _testRun.num_actions_taken++;
    }

    public void AddColorClickEvent()
    {

        string param = "Body";

        foreach (var ev in _testRun.action_log.AsEnumerable().Reverse())
        {
            if(ev.Action_type == EditorLogEvent.ActionType.select_category.ToString())
            {
                param = ev.Parameter;
                if(param == "Face")
                {
                    param = "Eyes";
                }
                break;
            }
        }

        _testRun.action_log.Add(new EditorLogEvent 
        {
            Timestamp = TimeStampNow(),
            Action_type = EditorLogEvent.ActionType.select_color.ToString(),
            Parameter = param,
            New_Value = ""
        });
    }

    public void AddRotateViewEvent(string newEulerAngles)
    {
        _testRun.action_log.Add(new EditorLogEvent 
        {
            Timestamp = TimeStampNow(),
            Action_type = EditorLogEvent.ActionType.rotate_view.ToString(),
            Parameter = "",
            New_Value = ""
        });
        _testRun.num_actions_taken++;
    }

    public long TimeStampNow()
    {
        return ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
    }
}