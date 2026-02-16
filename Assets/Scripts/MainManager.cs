using UnityEngine;
using UnityEngine.UI;
using Genies.Sdk;
using System.Threading.Tasks;
using Genies.Sdk.Samples.AvatarStarter;
using System.Collections.Generic;
using System;
using System.IO;
using Unity.VectorGraphics;
using UnityEngine.SceneManagement;
using Genies.Sdk.Samples.MultipleAvatars;
using Genies.Services.Api;


// [System.Serializable]
// public class EditorLogEvent 
// {
//     public enum ActionType
//     {
//         select_category,
//         select_subcategory,
//         select_color,
//         select_customization_option,
//         rotate_view,
//     }

//     public long Timestamp;
//     public string Action_type;
//     public string Parameter;
//     public string New_Value;

//     public EditorLogEvent()
//     {

//     }
// }

[System.Serializable]
public class TestRun 
{
    public string Participant_id;
    // public List<EditorLogEvent> action_log = new List<EditorLogEvent> ();
    public long start_ts;
    public long end_ts;
    public double time_elapsed_s;
    public int num_actions_taken;
    public int base_gender;
    public int perceived_success;
    public int perceived_difficulty;
    public int target_image;
    public string platform = "mobile";
    public string avatar_definition;
    public string final_image_front;
    public string final_image_side;
    public string final_image_portrait;
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
        Debug.Log("1. Processing Test Run Data...");
        ProcessTestRun();

        Debug.Log("2. Capturing Screenshots...");
        // Returns list of file paths: [0]=Portrait, [1]=Front, [2]=Side
        _savedFilePaths = await _characterPhotographer.CaptureAllShotsAsync();
        
        await AvatarSdk.CloseAvatarEditorAsync(true);
        _UIManager.ShowSurvey();
    }

private async Task UploadButtonPressedAsync()
    {

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
        Task<string> taskPortrait = _imgBBManager.UploadImageAsync(_savedFilePaths[0]);
        Task<string> taskFront = _imgBBManager.UploadImageAsync(_savedFilePaths[1]);
        Task<string> taskSide = _imgBBManager.UploadImageAsync(_savedFilePaths[2]);

        await Task.WhenAll(taskPortrait, taskFront, taskSide);

        _testRun.final_image_portrait = await taskPortrait;
        _testRun.final_image_front = await taskFront;
        _testRun.final_image_side = await taskSide;

        Debug.Log($"Images Uploaded! Portrait URL: {_testRun.final_image_portrait}");
    }

    // Helper to wrap your callback-based JSONBin upload into a Task
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
        _testRun.base_gender = _UIManager.GetBaseGender();
        _testRun.perceived_difficulty = _UIManager.GetPerceivedDifficulty();
        _testRun.perceived_success = _UIManager.GetPerceivedSuccess();
    }

public void LoadDefaultAvatar(Gender gender)
{
    string defaultName = "";

    if(gender == Gender.MALE)
    {
        defaultName = "default-male";
    }
    else if(gender == Gender.FEMALE)
    {
        defaultName = "default-female";
    }
    else
    {
        defaultName = "default-nonbinary";
    }

    _JSONBinManager.DownloadJSONByName(defaultName, (success, result) =>
    {
        if (success)
        {
            var _defaultAvatarJSON = result;
            
            _defaultAvatar = JsonUtility.FromJson<TestRun>(result);
            
            if (_defaultAvatar != null)
            {
                Debug.Log($"Loaded Default Avatar - Participant: {_defaultAvatar.Participant_id}, Platform: {_defaultAvatar.platform}");
            }
            else
            {
                Debug.LogError("Failed to parse default avatar JSON into TestRun object");
            }
        }
        else
        {
            Debug.LogError("Failed to download default avatar");
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
        _managedAvatar = _loadMyAvatar.LoadedAvatar;
        await AvatarSdk.OpenAvatarEditorAsync(_managedAvatar);
        Debug.Log("Setting Default Avatar Definition in Editor..." + _defaultAvatar.avatar_definition);
        await AvatarSdk.GetAvatarEditorAvatar().SetDefinitionAsync(_defaultAvatar.avatar_definition);
        // SetDefaultAvatar();
        _startTime = DateTime.UtcNow;
        Debug.Log("Started Timing, Experiment Begins Now");
    }

    private void SetDefaultAvatar()
    {
    }
}
