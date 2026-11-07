using SpacetimeDB.Types;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

public class CircleController : EntityController
{
	[NoAutoStaticsCleanup]
	private static readonly Color[] ColorPalette =
	{
		//Yellow
		new Color32(175, 159, 49, 255),
		new Color32(175, 116, 49, 255),

		//Purple
		new Color32(112, 47, 252, 255),
		new Color32(51, 91, 252, 255),

		//Red
		new Color32(176, 54, 54, 255),
		new Color32(176, 109, 54, 255),
		new Color32(141, 43, 99, 255),

		//Blue
		new Color32(2, 188, 250, 255),
		new Color32(7, 50, 251, 255),
		new Color32(2, 28, 146, 255),
	};

    private PlayerController owner;

    public void Spawn(Circle circle, PlayerController withOwner)
    {
        base.Spawn(circle.EntityId);
		SetColor(ColorPalette[circle.PlayerId % ColorPalette.Length]);

        owner = withOwner;
        GetComponentInChildren<TMPro.TextMeshProUGUI>().text = withOwner.Username;
    }

	public override void OnDelete(EventContext context)
	{
        owner.OnCircleDeleted(this);

		base.OnDelete(context);
	}
}