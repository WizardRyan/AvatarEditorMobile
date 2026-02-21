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
    // Now passes both the clicked object and the name of its parent category
    public System.Action<GameObject, string> onNodeClicked;
    public string parentCategory;

    public void OnPointerClick(PointerEventData eventData)
    {
        onNodeClicked?.Invoke(gameObject, parentCategory);
    }
}

[System.Serializable]
public class UIInterceptTarget
{
    public string parentName;
    public string cloneNameTarget;
    [HideInInspector] public Transform cachedParent = null;
}


public class RotationWatcher : MonoBehaviour
{
    // The event we will fire when rotation officially stops
    public System.Action<GameObject> onRotationEnded;

    [Tooltip("How long the object must remain still before firing the event")]
    public float settleTime = 0.2f;
    
    [Tooltip("Minimum angle change (in degrees) to be considered 'rotating'")]
    public float angleThreshold = 0.05f;

    private Quaternion lastRotation;
    private bool isRotating = false;
    private float stationaryTimer = 0f;

    void Start()
    {
        // Initialize our baseline rotation
        lastRotation = transform.rotation;
    }

    void Update()
    {
        // Check the difference between current rotation and the last recorded rotation
        float angleDifference = Quaternion.Angle(transform.rotation, lastRotation);

        if (angleDifference > angleThreshold)
        {
            // The object is actively moving. 
            // Reset the timer and update the last known rotation.
            isRotating = true;
            stationaryTimer = 0f;
            lastRotation = transform.rotation;
        }
        else if (isRotating)
        {
            // The object hasn't moved past the threshold, but it WAS rotating.
            // Start counting up the settle timer.
            stationaryTimer += Time.deltaTime;

            if (stationaryTimer >= settleTime)
            {
                // The object has been still for long enough. Fire the event!
                isRotating = false;
                onRotationEnded?.Invoke(gameObject);
            }
        }
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

[Header("UI Targets")]
    // In the Inspector, set Size to 2.
    // Element 0: Parent = "GPCustomizerNavBar", Clone = "CustomizerNavBarNode(Clone)"
    // Element 1: Parent = "SecondaryItemPicker", Clone = "CellHolder(Clone)"
    public List<UIInterceptTarget> targets = new List<UIInterceptTarget>();


    [Header("RotateTargets")]
    public string cloneNameTarget = "NativeGenie(Clone)";
    public float checkIntervalRotation = 1.0f;

    // Optional: If NativeGenie(Clone) always spawns under a specific parent, 
    // put the parent's name here to heavily optimize the search.
    public string optionalParentName = "AvatarSpawn"; 

    private HashSet<GameObject> processedGenies = new HashSet<GameObject>();
    private Transform cachedParent = null;

    private HashSet<GameObject> processedNodes = new HashSet<GameObject>();
    public float checkInterval = 1.0f;
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
        StartCoroutine(PollForGeniesRoutine());
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
            foreach (var target in targets)
            {
                // Find parent if we lost it or haven't found it yet
                if (target.cachedParent == null)
                {
                    GameObject parentObj = GameObject.Find(target.parentName);
                    if (parentObj != null) target.cachedParent = parentObj.transform;
                }

                // If parent exists, search its children for the clones
                if (target.cachedParent != null)
                {
                    Transform[] childTransforms = target.cachedParent.GetComponentsInChildren<Transform>(true);

                    foreach (Transform t in childTransforms)
                    {
                        GameObject go = t.gameObject;

                        if (go.name == target.cloneNameTarget && !processedNodes.Contains(go))
                        {
                            SetupNode(go, target.parentName);
                            processedNodes.Add(go);
                        }
                    }
                }
            }

            // Clean up destroyed nodes
            processedNodes.RemoveWhere(node => node == null);

            yield return new WaitForSeconds(checkInterval);
        }
    }

    private void SetupNode(GameObject node, string parentCategory)
    {
        RawUIClickCatcher clickCatcher = node.GetComponent<RawUIClickCatcher>();
        if (clickCatcher == null)
        {
            clickCatcher = node.AddComponent<RawUIClickCatcher>();
        }

        clickCatcher.parentCategory = parentCategory;
        clickCatcher.onNodeClicked = OnInterceptedClick;
    }

    public void OnInterceptedClick(GameObject clickedNode, string parentCategory)
    {
        string nodeText = ExtractTextFromNode(clickedNode);

        Debug.Log($"[UI Clicked] Category: {parentCategory} | Node: {clickedNode.name} | Text: {nodeText}");
        
        if (parentCategory == "GPCustomizerNavBar")
        {
            Debug.Log("Clicked on Category");
            _mainManager.AddCategoryClickEvent(nodeText);
        }
        else if (parentCategory == "SecondaryItemPicker")
        {
            Debug.Log($"Clicked on Color");
            _mainManager.AddColorClickEvent();
        }

    }

    private string ExtractTextFromNode(GameObject node)
    {
        TextMeshProUGUI tmpText = node.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
        {
            return tmpText.text;
        }

        return "[No Text Found]";
    }

    private IEnumerator PollForGeniesRoutine()
    {
        while (true)
        {
            Transform[] transformsToSearch = null;

            // Decide whether to search the whole scene or just a specific parent
            if (!string.IsNullOrEmpty(optionalParentName))
            {
                if (cachedParent == null)
                {
                    GameObject parentObj = GameObject.Find(optionalParentName);
                    if (parentObj != null) cachedParent = parentObj.transform;
                }

                if (cachedParent != null)
                {
                    transformsToSearch = cachedParent.GetComponentsInChildren<Transform>(true);
                }
            }
            else
            {
                // Fallback: search everything if no parent is provided
                transformsToSearch = FindObjectsOfType<Transform>();
            }

            if (transformsToSearch != null)
            {
                foreach (Transform t in transformsToSearch)
                {
                    GameObject go = t.gameObject;

                    if (go.name == cloneNameTarget && !processedGenies.Contains(go))
                    {
                        SetupGenie(go);
                        processedGenies.Add(go);
                    }
                }
            }

            // Clean up the set in case genies are destroyed
            processedGenies.RemoveWhere(node => node == null);

            yield return new WaitForSeconds(checkInterval);
        }
    }

    private void SetupGenie(GameObject genie)
    {
        RotationWatcher watcher = genie.GetComponent<RotationWatcher>();
        if (watcher == null)
        {
            watcher = genie.AddComponent<RotationWatcher>();
        }

        watcher.onRotationEnded = HandleGenieRotationEnded;
    }

    private void HandleGenieRotationEnded(GameObject genieObj)
    {
        Vector3 finalEulerAngles = genieObj.transform.localEulerAngles;
        
        Debug.Log($"[Rotation Ended] {genieObj.name} stopped at Euler Angles: {finalEulerAngles}");
        _mainManager.AddRotateViewEvent(finalEulerAngles.ToString());
    }
}