using UnityEngine;
using UnityEngine.Serialization;

public class ParallaxBackground : MonoBehaviour
{
    [FormerlySerializedAs("Multiplier")]
    public float multiplier;

	private void LateUpdate()
	{
		var pos = Camera.main.transform.position * multiplier;
		pos.z = 0;
		transform.position = pos;
	}
}