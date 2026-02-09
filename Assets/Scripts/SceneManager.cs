using UnityEngine;
using UnityEngine.UI;
using Genies.Sdk;
using System.Threading.Tasks;
using Genies.Sdk.Samples.AvatarStarter;

public enum Gender
{
    MALE,
    FEMALE,
    NONBINARY
}
public class SceneManager : MonoBehaviour
{

    [SerializeField]
    private JSONBinManager _JSONBinManager;
    [SerializeField]
    private LoadMyAvatar _loadMyAvatar;

    private string _defaultAvatarJSON;
    private ManagedAvatar _managedAvatar;

    void Start()
    {
        LoadDefaultAvatar(Gender.MALE);
    }

    void Update()
    {
        
    }

    public void SaveButtonPressed()
    {
        var avatar = AvatarSdk.GetAvatarEditorAvatar();
        var definition = avatar.GetDefinition();

        _JSONBinManager.UploadJSON("Test-Upload-1", definition, (success, result) =>
        {
            if (success)
            {
                Debug.Log("Avatar Uploaded!");
            }
        });

        AvatarSdk.CloseAvatarEditorAsync(true);
    }

    public void EditButtonPressed()
    {
        
    }

    public void LoadDefaultAvatar(Gender gender)
    {
        string defaultName = "";

        if(gender == Gender.MALE)
        {
            defaultName = "Test-Upload-1";
        }

        _JSONBinManager.DownloadJSONByName(defaultName, (success, result) =>
        {
            if (success)
            {
                _defaultAvatarJSON = result;
                Debug.Log("Loaded Default Avatar");
            }
        });
    }

    public void BeginEditingAvatar()
    {
        BeginEditingAvatarAsync();
    }

    public async Task BeginEditingAvatarAsync()
    {
        _managedAvatar = _loadMyAvatar.LoadedAvatar;
        await AvatarSdk.OpenAvatarEditorAsync(_managedAvatar);
        SetDefaultAvatar();
    }

    private void SetDefaultAvatar()
    {
        AvatarSdk.GetAvatarEditorAvatar().SetDefinitionAsync(_defaultAvatarJSON);
    }
}
