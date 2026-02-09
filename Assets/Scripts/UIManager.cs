using UnityEngine;
using Genies.Sdk;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    [SerializeField] private GameObject buttonBeginEditing;
    [SerializeField] private GameObject buttonSave;
    [SerializeField] private GameObject _UIBackgroundPlane;
    [SerializeField] private GameObject _titleBar;



    private bool _shownInitialConfigUI = false;
    private bool _shownEditorOpenUI = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HideInitialConfigUI();
    }

    // Update is called once per frame
    void Update()
    {
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
        buttonBeginEditing.SetActive(true);
    }

    private void HideInitialConfigUI()
    {
        buttonBeginEditing.SetActive(false);
    }

    private void ShowEditorOpenUI()
    {
        buttonSave.SetActive(true);
        _UIBackgroundPlane.SetActive(false);
    }

    private void HideEditorOpenUI()
    {
        _UIBackgroundPlane.SetActive(true);
        buttonSave.SetActive(false);
    }
}
