using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class LeaderboardRow : MonoBehaviour
{
    [FormerlySerializedAs("UsernameText")]
    public TextMeshProUGUI usernameText;
    [FormerlySerializedAs("MassText")]
    public TextMeshProUGUI massText;

    public void SetData(string username, int mass)
	{
		usernameText.text = username;
		massText.text = mass.ToString();
	}
}