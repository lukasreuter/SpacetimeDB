using SpacetimeDB.Types;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

public class FoodController : EntityController
{
	[NoAutoStaticsCleanup]
	private static readonly Color[] ColorPalette =
	{
		new Color32(119, 252, 173, 255),
		new Color32(76, 250, 146, 255),
		new Color32(35, 246, 120, 255),

		new Color32(119, 251, 201, 255),
		new Color32(76, 249, 184, 255),
		new Color32(35, 245, 165, 255),
	};

	public void Spawn(Food food)
	{
		base.Spawn(food.EntityId);
		SetColor(ColorPalette[entityId % ColorPalette.Length]);
	}
}