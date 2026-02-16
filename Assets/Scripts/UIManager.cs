using UnityEngine;
using Genies.Sdk;
using UnityEngine.UI;
using System;
using Genies.Sdk.Samples.Common;
using TMPro;
using System.Threading.Tasks;

public class UIManager : MonoBehaviour
{

    [SerializeField] private GameObject _buttonBeginEditing;
    [SerializeField] private GameObject _buttonFinishedEditing;
    [SerializeField] private GameObject _UIBackgroundPlane;
    [SerializeField] private GameObject _titleBar;
    [SerializeField] private GameObject _inputFieldParticipantID;
    [SerializeField] private GameObject _dropdownTargetImage;
    [SerializeField] private GameObject _dropdownBaseGender;
    [SerializeField] private GameObject _cinemachineFreeLookControls;
    [SerializeField] private GameObject _world;
    [SerializeField] private GameObject _surveyCanvas;
    [SerializeField] private GameObject _mainCanvas;
    [SerializeField] private GameObject _dropDownSurveyQuestion1;
    [SerializeField] private GameObject _dropDownSurveyQuestion2;
    [SerializeField] private GameObject _buttonUploadResults;
    [SerializeField] private TMP_Text _uploadSuccessMessage;
    [SerializeField] private GameObject _freelookControls;
    [SerializeField] private GameObject _avatarSpawn;
    [SerializeField] private MainManager _mainManager;

    private int _targetImage = 1;
    private int _baseGender = 0;
    private string _participantId = "";
    private bool _shownInitialConfigUI = false;
    private bool _shownEditorOpenUI = false;
    private int _perceivedSuccess = 1;
    private int _perceivedDifficulty = 1;

    private bool _SDKInitialized = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        initSDK();
        HideInitialConfigUI();
        _dropdownTargetImage.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { DropdownTargetImageValueChanged(); });
        _inputFieldParticipantID.GetComponent<TMP_InputField>().onValueChanged.AddListener(delegate { InputFieldParticipantIDValueChanged(); });
        _dropdownBaseGender.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { DropdownBaseGenderValueChanged(); });
        _dropDownSurveyQuestion1.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { DropdownSurveyQuestion1ValueChanged(); });
        _dropDownSurveyQuestion2.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { DropdownSurveyQuestion2ValueChanged(); });
        _uploadSuccessMessage.text = "";
        _surveyCanvas.SetActive(false);

    }

    private async Task initSDK()
    {
        await AvatarSdk.InitializeAsync();
        _SDKInitialized = true;
    }

    private void InputFieldParticipantIDValueChanged()
    {
        _participantId = _inputFieldParticipantID.GetComponent<TMP_InputField>().text;
        Debug.Log("Participant ID: " + _participantId);
    }

    private void DropdownTargetImageValueChanged()
    {
        _targetImage = _dropdownTargetImage.GetComponent<TMP_Dropdown>().value + 1;
        Debug.Log("Target Image: " + _targetImage);
    }

    private void DropdownBaseGenderValueChanged()
    {
        _baseGender = _dropdownBaseGender.GetComponent<TMP_Dropdown>().value;
        Debug.Log("Base Gender: " + _baseGender);
        _mainManager.LoadDefaultAvatar((Gender)_baseGender);
    }

    private void DropdownSurveyQuestion1ValueChanged()
    {
        _perceivedSuccess = _dropDownSurveyQuestion1.GetComponent<TMP_Dropdown>().value + 1;
        Debug.Log("Perceived Success: " + _perceivedSuccess);
    }

    private void DropdownSurveyQuestion2ValueChanged()
    {
        _perceivedDifficulty = _dropDownSurveyQuestion2.GetComponent<TMP_Dropdown>().value + 1;
        Debug.Log("Perceived Difficulty: " + _perceivedDifficulty);
    }

    public bool AllValuesFilled()
    {
        return _participantId != "";
    }

    public int GetTargetImage()
    {
        return _targetImage;
    }
    public string GetParticipantId()
    {
        return _participantId;
    }

    public int GetBaseGender()
    {
        return _baseGender;
    }

    public int GetPerceivedDifficulty()
    {
        return _perceivedDifficulty;
    }

    public int GetPerceivedSuccess()
    {
        return _perceivedSuccess;
    }



    // Update is called once per frame
    void Update()
    {

        if(!_SDKInitialized)
        {
            Debug.Log("SDK not initialized yet...");
            return;
        }

        if (AvatarSdk.IsLoggedIn && !_shownInitialConfigUI)
        {
            ShowInitialConfigUI();
            _shownInitialConfigUI = true;
        }
        if(AvatarSdk.IsAvatarEditorOpen && !_shownEditorOpenUI)
        {
            ShowEditorOpenUI();
            HideInitialConfigUI();
            _shownEditorOpenUI = true;
        }
        if (!AvatarSdk.IsAvatarEditorOpen)
        {
            HideEditorOpenUI();
        }

        _titleBar.SetActive(false);
    }

    private void ShowInitialConfigUI()
    {
        _buttonBeginEditing.SetActive(true);
        _inputFieldParticipantID.SetActive(true);
        _dropdownTargetImage.SetActive(true);
        _dropdownBaseGender.SetActive(true);
        _cinemachineFreeLookControls.SetActive(false);
    }

    private void HideInitialConfigUI()
    {
        _buttonBeginEditing.SetActive(false);
        _inputFieldParticipantID.SetActive(false);
        _dropdownBaseGender.SetActive(false);
        _dropdownTargetImage.SetActive(false);
    }

    private void ShowEditorOpenUI()
    {
        _buttonFinishedEditing.SetActive(true);
        _UIBackgroundPlane.SetActive(false);
        _cinemachineFreeLookControls.SetActive(true);
    }

    private void HideEditorOpenUI()
    {
        _UIBackgroundPlane.SetActive(true);
        _buttonFinishedEditing.SetActive(false);
        _cinemachineFreeLookControls.SetActive(false);
    }

    public void ShowSurvey()
    {
        _mainCanvas.SetActive(false);
        _world.SetActive(false);
        // _UIBackgroundPlane.SetActive(true);
        _avatarSpawn.SetActive(false);
        _freelookControls.SetActive(false);
        _surveyCanvas.SetActive(true);
    }

    public void ShowUploadInProgress()
    {
        _buttonUploadResults.SetActive(false);
        _uploadSuccessMessage.text = "Uploading...";
    }
    
    public void ShowUploadSuccess()
    {
        _uploadSuccessMessage.text = "Upload Successful!";
    }

        public void ShowUploadFailure()
    {
        _uploadSuccessMessage.text = "Upload Failed. Please Check Your Connection.";
    }
}
