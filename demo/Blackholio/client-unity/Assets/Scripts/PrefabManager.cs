using SpacetimeDB.Types;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Serialization;

public partial class PrefabManager : MonoBehaviour
{
	[AutoStaticsCleanup]
	private static PrefabManager instance;

	[FormerlySerializedAs("CirclePrefab")]
	public CircleController circlePrefab;
	[FormerlySerializedAs("FoodPrefab")]
	public FoodController foodPrefab;
	[FormerlySerializedAs("PlayerPrefab")]
	public PlayerController playerPrefab;

	private void Awake()
	{
		instance = this;
	}

	public static CircleController SpawnCircle(Circle circle, PlayerController owner)
	{
		var entityController = Instantiate(instance.circlePrefab);
		entityController.name = $"Circle - {circle.EntityId}";
		entityController.Spawn(circle, owner);
		owner.OnCircleSpawned(entityController);
		return entityController;
	}

	public static FoodController SpawnFood(Food food)
	{
		var entityController = Instantiate(instance.foodPrefab);
		entityController.name = $"Food - {food.EntityId}";
		entityController.Spawn(food);
		return entityController;
	}

	public static PlayerController SpawnPlayer(Player player)
	{
		var playerController = Instantiate(instance.playerPrefab);
		playerController.name = $"PlayerController - {player.Name}";
		playerController.Initialize(player);
		return playerController;
	}
}