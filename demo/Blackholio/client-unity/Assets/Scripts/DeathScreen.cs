using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class DeathScreen : MonoBehaviour
{
	[FormerlySerializedAs("RespawnButton")]
	public Button respawnButton;

	public void SetVisible(bool visible)
	{
		gameObject.SetActive(visible);
	}

	public void Respawn()
	{
		GameManager.Conn.Reducers.Respawn();
		SetVisible(false);
	}
}