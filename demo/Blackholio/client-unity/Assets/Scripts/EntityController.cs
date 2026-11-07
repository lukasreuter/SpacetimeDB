using System;
using SpacetimeDB.Types;
using System.Collections;
using UnityEngine;

public abstract class EntityController : MonoBehaviour
{
	private const float LERP_DURATION_SEC = 0.1f;

	private static readonly int ShaderColorProperty = Shader.PropertyToID("_Color");

	[NonSerialized] public int entityId;

	protected float lerpTime;
	protected Vector3 lerpStartPosition;
	protected Vector3 lerpTargetPosition;
	protected Vector3 targetScale;

	protected virtual void Spawn(int withEntityId)
	{
		entityId = withEntityId;

		var entity = GameManager.Conn.Db.Entity.EntityId.Find(withEntityId);
		lerpStartPosition = lerpTargetPosition = transform.position = (Vector2) entity.Position;
		transform.localScale = Vector3.one;
		targetScale = MassToScale(entity.Mass);
	}

	public void SetColor(Color color)
	{
		GetComponent<SpriteRenderer>().material.SetColor(ShaderColorProperty, color);
	}

	public virtual void OnEntityUpdated(Entity newVal)
	{
		lerpTime = 0.0f;
		lerpStartPosition = transform.position;
		lerpTargetPosition = (Vector2)newVal.Position;
		targetScale = MassToScale(newVal.Mass);
	}

	public virtual void OnDelete(EventContext context)
	{
		Destroy(gameObject);
	}

	public IEnumerator DespawnCoroutine(Transform targetTransform)
	{
		const float despawnTime = 0.2f;
		var startPosition = transform.position;
		var startScale = transform.localScale;
		GetComponent<SpriteRenderer>().sortingOrder++; //Render consumed food above the circle that's consuming it
		for (float time = Time.deltaTime; time < despawnTime; time += Time.deltaTime)
		{
			float t = time / despawnTime;
			transform.position = Vector3.Lerp(startPosition, targetTransform.position, t);
			transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
			yield return null;
		}
		Destroy(gameObject);
	}

	public virtual void Update()
	{
		//Interpolate position and scale
		lerpTime = Mathf.Min(lerpTime + Time.deltaTime, LERP_DURATION_SEC);
		transform.position = Vector3.Lerp(lerpStartPosition, lerpTargetPosition, lerpTime / LERP_DURATION_SEC);
		transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 8);
	}

	public static Vector3 MassToScale(int mass)
	{
		var diameter = MassToDiameter(mass);
		return new Vector3(diameter, diameter, 1);
	}

	public static float MassToRadius(int mass) => Mathf.Sqrt(mass);
	public static float MassToDiameter(int mass) => MassToRadius(mass) * 2;
}