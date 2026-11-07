using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Serialization;

public partial class UIUsernameChooser : MonoBehaviour
{
    [AutoStaticsCleanup]
    public static UIUsernameChooser Instance { get; private set; }

    // The elements that compose the username chooser UI
    public GameObject[] elements;
    [FormerlySerializedAs("UsernameInputField")]
    public TMPro.TMP_InputField usernameInputField;
    
    private void Start()
    {
        Instance = this;
    }

    public void PlayPressed()
    {
		Debug.Log("Creating player");

        var userName = usernameInputField.text.Trim();
        if (string.IsNullOrEmpty(userName))
        {
            userName = "<No Name>";
        }
		GameManager.Conn.Reducers.EnterGame(userName);
		Show(false);
	}

    public void Show(bool showing)
    {
        foreach (var element in elements)
        {
            element.SetActive(showing);
        }
    }
}