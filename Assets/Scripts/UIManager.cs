using UnityEngine;
using Genies.Sdk;
using UnityEngine.UI;
using System;
using Genies.Sdk.Samples.Common;
using TMPro;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // Required for raw UI pointer events

public class RawUIClickCatcher : MonoBehaviour, IPointerClickHandler
{
    // A delegate to store the function we want to call back in the Manager
    public System.Action<GameObject> onNodeClicked;

    // This is fired directly by the Unity EventSystem when the object is clicked
    public void OnPointerClick(PointerEventData eventData)
    {
        // Invoke the manager's function, passing this gameObject
        onNodeClicked?.Invoke(gameObject);
    }
}

public class UIManager : MonoBehaviour
{
    [Header("UI Controls")]
    [SerializeField] private GameObject _buttonBeginEditing;
    [SerializeField] private GameObject _buttonFinishedEditing;
    [SerializeField] private GameObject _UIBackgroundPlane;
    [SerializeField] private GameObject _titleBar;
    [SerializeField] private GameObject _inputFieldParticipantID;
    [SerializeField] private GameObject _dropdownTargetImage;
    // [SerializeField] private GameObject _dropdownBaseGender;
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

    [Header("Reference Images")]
    [Tooltip("Assign the UI Image for the Portrait shot here")]
    [SerializeField] private Image _targetImagePortrait;
    [Tooltip("Assign the UI Image for the Body Front shot here")]
    [SerializeField] private Image _targetImageBody;

    [Header("Settings")]
    public string nodeNameTarget = "CustomizerNavBarNode(Clone)";
    public float checkInterval = 1.0f;
    public string parentNavBarName = "GPCustomizerNavBar";
    private Transform navBarParent = null;

    private HashSet<GameObject> processedNodes = new HashSet<GameObject>();

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
        // _dropdownBaseGender.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { DropdownBaseGenderValueChanged(); });
        _dropDownSurveyQuestion1.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { DropdownSurveyQuestion1ValueChanged(); });
        _dropDownSurveyQuestion2.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { DropdownSurveyQuestion2ValueChanged(); });
        _uploadSuccessMessage.text = "";
        _surveyCanvas.SetActive(false);
    }
    public void LoadReferenceImages()
    {
        string basePath;
    #if UNITY_EDITOR
        basePath = Path.Combine(Application.dataPath, "Resources", "CharacterShots");
    #else
        basePath = Path.Combine(Application.persistentDataPath, "CharacterShots");
    #endif

        LoadAndAssign(_targetImagePortrait, Path.Combine(basePath, "Captured_Portrait.png"));
        LoadAndAssign(_targetImageBody, Path.Combine(basePath, "Captured_BodyFront.png"));
    }

    private void LoadAndAssign(Image targetImage, string filePath)
    {
        if (targetImage == null) return;

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"File not found: {filePath}");
            return;
        }

        byte[] bytes = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(bytes);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

        targetImage.sprite = sprite;
        targetImage.preserveAspect = true;
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

    // private void DropdownBaseGenderValueChanged()
    // {
    //     _baseGender = _dropdownBaseGender.GetComponent<TMP_Dropdown>().value;
    //     Debug.Log("Base Gender: " + _baseGender);
    //     _mainManager.LoadDefaultAvatar((Gender)_baseGender);
    // }

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

    // public int GetBaseGender()
    // {
    //     return _baseGender;
    // }

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
        // _dropdownBaseGender.SetActive(true);
        _cinemachineFreeLookControls.SetActive(false);
    }

    private void HideInitialConfigUI()
    {
        _buttonBeginEditing.SetActive(false);
        _inputFieldParticipantID.SetActive(false);
        // _dropdownBaseGender.SetActive(false);
        _dropdownTargetImage.SetActive(false);
    }

    private void ShowEditorOpenUI()
    {
        StartCoroutine(PollForNewNodesRoutine());
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
        LoadReferenceImages();
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

private IEnumerator PollForNewNodesRoutine()
    {
        while (true)
        {
            // Step 1: Find the parent container. 
            // We do this inside the loop in case the game destroys and recreates the Nav Bar.
            if (navBarParent == null)
            {
                GameObject parentObj = GameObject.Find(parentNavBarName);
                if (parentObj != null)
                {
                    navBarParent = parentObj.transform;
                }
            }

            // Step 2: If we have the parent, search ONLY its descendants.
            if (navBarParent != null)
            {
                // Passing 'true' allows it to find inactive children as well, 
                // just in case the game hides them before showing them.
                Transform[] childTransforms = navBarParent.GetComponentsInChildren<Transform>(true);

                foreach (Transform t in childTransforms)
                {
                    GameObject go = t.gameObject;

                    if (go.name == nodeNameTarget && !processedNodes.Contains(go))
                    {
                        SetupNode(go);
                        processedNodes.Add(go);
                    }
                }
            }

            // Clean up the hashset in case any nodes were destroyed by the game
            processedNodes.RemoveWhere(node => node == null);

            yield return new WaitForSeconds(checkInterval);
        }
    }

    private void SetupNode(GameObject node)
    {
        // Add our custom low-level click catcher instead of a standard Button
        RawUIClickCatcher clickCatcher = node.GetComponent<RawUIClickCatcher>();
        if (clickCatcher == null)
        {
            clickCatcher = node.AddComponent<RawUIClickCatcher>();
        }

        // Wire up the callback to our manager's function
        clickCatcher.onNodeClicked = NavBarNodeClicked;
    }

    // The function called when a node is clicked
    public void NavBarNodeClicked(GameObject clickedNode)
    {
        Debug.Log($"[Success] Intercepted click on: {clickedNode.name}", clickedNode);
        
        // Your logic goes here
    }
}