using SpacetimeDB;

namespace StdbModule;

public static partial class Module
{
	private const int START_PLAYER_MASS = 15;
	private const int START_PLAYER_SPEED = 10;
	private const int FOOD_MASS_MIN = 2;
	private const int FOOD_MASS_MAX = 4;
	private const int TARGET_FOOD_COUNT = 600;
	private const float MINIMUM_SAFE_MASS_RATIO = 0.85f;
	private const float MIN_OVERLAP_PCT_TO_CONSUME = 0.1f;

	private const int MIN_MASS_TO_SPLIT = START_PLAYER_MASS * 2;
	private const int MAX_CIRCLES_PER_PLAYER = 16;
	private const float SPLIT_RECOMBINE_DELAY_SEC = 5f;
	private const float SPLIT_GRAV_PULL_BEFORE_RECOMBINE_SEC = 2f;
	private const float ALLOWED_SPLIT_CIRCLE_OVERLAP_PCT = 0.9f;
	private const float SELF_COLLISION_SPEED = 0.05f; //1 == instantly separate circles. less means separation takes time

	#region Tables

	[Table(Accessor = "config", Public = true)]
	public partial struct Config
	{
		[PrimaryKey]
		public int id;
		public long world_size;
	}

	[Table(Accessor = "entity", Public = true)]
	[Table(Accessor = "logged_out_entity")]
	public partial struct Entity
	{
		[PrimaryKey, AutoInc]
		public int entity_id;
		public DbVector2 position;
		public int mass;
	}

	[Table(Accessor = "circle", Public = true)]
	[SpacetimeDB.Index.BTree(Accessor = "player_id", Columns = [nameof(player_id)])]
	[Table(Accessor = "logged_out_circle")]
	public partial struct Circle
	{
		[PrimaryKey]
		public int entity_id;
		public int player_id;
		public DbVector2 direction;
		public float speed;
		public Timestamp last_split_time;
	}

	[Table(Accessor = "player", Public = true)]
	[Table(Accessor = "logged_out_player")]
	public partial struct Player
	{
		[PrimaryKey]
		public Identity identity;
		[Unique, AutoInc]
		public int player_id;
		public string name;
	}

	[Table(Accessor = "food", Public = true)]
	public partial struct Food
	{
		[PrimaryKey]
		public int entity_id;
	}

	[Table(Accessor = "move_all_players_timer", Scheduled = nameof(MoveAllPlayers), ScheduledAt = nameof(scheduled_at))]
	public partial struct MoveAllPlayersTimer
	{
		[PrimaryKey, AutoInc]
		public ulong scheduled_id;
		public ScheduleAt scheduled_at;
	}

	[Table(Accessor = "spawn_food_timer", Scheduled = nameof(SpawnFood), ScheduledAt = nameof(scheduled_at))]
	public partial struct SpawnFoodTimer
	{
		[PrimaryKey, AutoInc]
		public ulong scheduled_id;
		public ScheduleAt scheduled_at;
	}

	[Table(Accessor = "circle_decay_timer", Scheduled = nameof(CircleDecay), ScheduledAt = nameof(scheduled_at))]
	public partial struct CircleDecayTimer
	{
		[PrimaryKey, AutoInc]
		public ulong scheduled_id;
		public ScheduleAt scheduled_at;
	}

	[Table(Accessor = "circle_recombine_timer", Scheduled = nameof(CircleRecombine), ScheduledAt = nameof(scheduled_at))]
	public partial struct CircleRecombineTimer
	{
		[PrimaryKey, AutoInc]
		public ulong scheduled_id;
		public ScheduleAt scheduled_at;
		public int player_id;
	}
    
    [Table(Accessor = "consume_entity_event", Public = true, Event = true)]
    public partial struct ConsumeEntityEvent
    {
        public int consumed_entity_id;
        public int consumer_entity_id;
    }

	[Table(Accessor = "consume_entity_timer", Scheduled = nameof(ConsumeEntity), ScheduledAt = nameof(scheduled_at))]
	public partial struct ConsumeEntityTimer
	{
		[PrimaryKey, AutoInc]
		public ulong scheduled_id;
		public ScheduleAt scheduled_at;
		public int consumed_entity_id;
		public int consumer_entity_id;
	}

	#endregion

	#region Reducers

	[Reducer(ReducerKind.Init)]
	public static void Init(ReducerContext ctx)
	{
		Log.Info("Initializing...");
		ctx.Db.config.Insert(new Config { world_size = 1000 });
		ctx.Db.circle_decay_timer.Insert(new CircleDecayTimer
		{
			scheduled_at = new ScheduleAt.Interval(TimeSpan.FromSeconds(5)),
		});
		ctx.Db.spawn_food_timer.Insert(new SpawnFoodTimer
		{
			scheduled_at = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(500)),
		});
		ctx.Db.move_all_players_timer.Insert(new MoveAllPlayersTimer
		{
			scheduled_at = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(50)),
		});
	}

	[Reducer(ReducerKind.ClientConnected)]
	public static void Connect(ReducerContext ctx)
	{
		Log.Info($"Connecting client identity: {ctx.Sender} to database identity: {ctx.DatabaseIdentity}");

		var player = ctx.Db.logged_out_player.identity.Find(ctx.Sender);
		if (player != null)
		{
			ctx.Db.player.Insert(player.Value);
			ctx.Db.logged_out_player.identity.Delete(player.Value.identity);

			foreach (var circle in ctx.Db.logged_out_circle.player_id.Filter(player.Value.player_id))
			{
				var entity = ctx.Db.logged_out_entity.entity_id.Find(circle.entity_id) ??
				             throw new Exception("Could not find Entity");
				ctx.Db.entity.Insert(entity);
				ctx.Db.logged_out_entity.entity_id.Delete(entity.entity_id);
				ctx.Db.circle.Insert(circle);
				ctx.Db.logged_out_circle.entity_id.Delete(entity.entity_id);
			}
		}
		else
		{
			ctx.Db.player.Insert(new Player
			{
				identity = ctx.Sender,
				player_id = 0,
				name = "",
			});
		}
	}

	[Reducer(ReducerKind.ClientDisconnected)]
	public static void Disconnect(ReducerContext ctx)
	{
		var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
		foreach (var circle in ctx.Db.circle.player_id.Filter(player.player_id))
		{
			var entity = ctx.Db.entity.entity_id.Find(circle.entity_id) ?? throw new Exception("Could not find circle");
			ctx.Db.logged_out_entity.Insert(entity);
			ctx.Db.entity.entity_id.Delete(entity.entity_id);
			ctx.Db.logged_out_circle.Insert(circle);
			ctx.Db.circle.entity_id.Delete(entity.entity_id);
		}
		ctx.Db.logged_out_player.Insert(player);
		ctx.Db.player.identity.Delete(player.identity);
	}

	[Reducer]
	public static void EnterGame(ReducerContext ctx, string name)
	{
		Log.Info($"Creating player with name {name}");
		var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
		player.name = name;
		ctx.Db.player.identity.Update(player);
		SpawnPlayerInitialCircle(ctx, player.player_id);
	}

	[Reducer]
	public static void Respawn(ReducerContext ctx)
	{
		var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("No such player found");

		SpawnPlayerInitialCircle(ctx, player.player_id);
	}

	[Reducer]
	public static void Suicide(ReducerContext ctx)
	{
		var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("No such player found");

		foreach (var circle in ctx.Db.circle.player_id.Filter(player.player_id))
		{
			DestroyEntity(ctx, circle.entity_id);
		}
	}

	public static Entity SpawnPlayerInitialCircle(ReducerContext ctx, int playerId)
	{
		var rng = ctx.Rng;
		var worldSize = (ctx.Db.config.id.Find(0) ?? throw new Exception("Config not found")).world_size;
		var playerStartRadius = MassToRadius(START_PLAYER_MASS);
		var x = rng.Range(playerStartRadius, worldSize - playerStartRadius);
		var y = rng.Range(playerStartRadius, worldSize - playerStartRadius);
		return SpawnCircleAt(
			ctx,
			playerId,
			START_PLAYER_MASS,
			new DbVector2(x, y),
			ctx.Timestamp
		);
	}

	public static Entity SpawnCircleAt(
		ReducerContext ctx,
		int playerId,
		int mass,
		DbVector2 position,
		Timestamp timestamp)
	{
		var entity = ctx.Db.entity.Insert(new Entity
		{
			position = position,
			mass = mass,
		});

		ctx.Db.circle.Insert(new Circle
		{
			entity_id = entity.entity_id,
			player_id = playerId,
			direction = new DbVector2(0, 1),
			speed = 0f,
			last_split_time = timestamp,
		});
		return entity;
	}

	[Reducer]
	public static void UpdatePlayerInput(ReducerContext ctx, DbVector2 direction)
	{
		var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
		foreach (var c in ctx.Db.circle.player_id.Filter(player.player_id))
		{
			var circle = c;
			circle.direction = direction.Normalized;
			circle.speed = Math.Clamp(direction.Magnitude, 0f, 1f);
			ctx.Db.circle.entity_id.Update(circle);
		}
	}

	public static bool IsOverlapping(Entity a, Entity b)
	{
		var dx = a.position.x - b.position.x;
		var dy = a.position.y - b.position.y;
		var distanceSq = dx * dx + dy * dy;

		var radiusA = MassToRadius(a.mass);
		var radiusB = MassToRadius(b.mass);
		var radiusSum = (radiusA + radiusB) * (1.0 - MIN_OVERLAP_PCT_TO_CONSUME);

		return distanceSq <= radiusSum * radiusSum;
	}

	public static float MassToRadius(int mass) => MathF.Sqrt(mass);

	public static float MassToMaxMoveSpeed(int mass) =>
		2f * START_PLAYER_SPEED / (1f + MathF.Sqrt((float) mass / START_PLAYER_MASS));

	[Reducer]
	public static void MoveAllPlayers(ReducerContext ctx, MoveAllPlayersTimer timer)
	{
		//var span = new SpacetimeDB.LogStopwatch("tick");

		if (ctx.Db.player.Count == 0)
		{
			// no reason to update if no players are connected
			return;
		}

		var worldSize = (ctx.Db.config.id.Find(0) ?? throw new Exception("Config not found")).world_size;

		var circleDirections = ctx.Db.circle.Iter().Select(static c => (c.entity_id, c.direction * c.speed)).
			ToDictionary();

		//Split circle movement
		foreach (var player in ctx.Db.player.Iter())
		{
			var circles = ctx.Db.circle.player_id.Filter(player.player_id).ToList();
			var playerEntities = circles.
				Select(c => ctx.Db.entity.entity_id.Find(c.entity_id) ?? throw new Exception("No entity for circle")).
				ToList();
			if (playerEntities.Count <= 1)
			{
				continue;
			}
			var count = playerEntities.Count;

			//Gravitate circles towards other circles before they recombine
			for (int i = 0; i < playerEntities.Count; i++)
			{
				var circleI = circles[i];
				var timeSinceSplit = ctx.Timestamp.TimeDurationSince(circleI.last_split_time).Microseconds / 1_000_000f;
				var timeBeforeRecombining = MathF.Max(SPLIT_RECOMBINE_DELAY_SEC - timeSinceSplit, 0f);
				if (timeBeforeRecombining > SPLIT_GRAV_PULL_BEFORE_RECOMBINE_SEC)
				{
					continue;
				}

				var entityI = playerEntities[i];
				for (int j = i + 1; j < playerEntities.Count; j++)
				{
					var entityJ = playerEntities[j];
					var diff = entityI.position - entityJ.position;
					var distanceSqr = diff.SqrMagnitude;
					if (distanceSqr <= 0.0001f)
					{
						diff = new DbVector2(1f, 0f);
						distanceSqr = 1f;
					}
					var radiusSum = MassToRadius(entityI.mass) + MassToRadius(entityJ.mass);
					if (distanceSqr > radiusSum * radiusSum)
					{
						var gravityMultiplier =
							1f - timeBeforeRecombining / SPLIT_GRAV_PULL_BEFORE_RECOMBINE_SEC;
						var vec = diff.Normalized
							* ((radiusSum - MathF.Sqrt(distanceSqr))
							* gravityMultiplier
							* 0.05f
							/ count);
						var vecHalf = vec / 2f;
						circleDirections[entityI.entity_id] += vecHalf;
						circleDirections[entityJ.entity_id] -= vecHalf;
					}
				}
			}

			//Force circles apart
			for (int i = 0; i < playerEntities.Count; i++)
			{
				var entityI = playerEntities[i];
				for (int j = i + 1; j < playerEntities.Count; j++)
				{
					var entityJ = playerEntities[j];
					var diff = entityI.position - entityJ.position;
					var distanceSqr = diff.SqrMagnitude;
					if (distanceSqr <= 0.0001f)
					{
						diff = new DbVector2(1f, 0f);
						distanceSqr = 1f;
					}
					var radiusSum = MassToRadius(entityI.mass) + MassToRadius(entityJ.mass);
					var radiusSumMultiplied = radiusSum * ALLOWED_SPLIT_CIRCLE_OVERLAP_PCT;
					if (distanceSqr < radiusSumMultiplied * radiusSumMultiplied)
					{
						var vec = diff.Normalized
							* ((radiusSum - MathF.Sqrt(distanceSqr))
							* SELF_COLLISION_SPEED);
						var vecHalf = vec / 2f;
						circleDirections[entityI.entity_id] += vecHalf;
						circleDirections[entityJ.entity_id] -= vecHalf;
					}
				}
			}
		}

		//Handle player input
		foreach (var circle in ctx.Db.circle.Iter())
		{
			var checkEntity = ctx.Db.entity.entity_id.Find(circle.entity_id);
			if (checkEntity == null)
			{
				// This can happen if the circle has been eaten by another circle.
				continue;
			}
			var circleEntity = checkEntity.Value;
			var circleRadius = MassToRadius(circleEntity.mass);
			var direction = circleDirections[circle.entity_id];
			var newPos = circleEntity.position + direction * MassToMaxMoveSpeed(circleEntity.mass);
			circleEntity.position.x = Math.Clamp(newPos.x, circleRadius, worldSize - circleRadius);
			circleEntity.position.y = Math.Clamp(newPos.y, circleRadius, worldSize - circleRadius);
			ctx.Db.entity.entity_id.Update(circleEntity);
		}

		// Check collisions
		var entities = ctx.Db.entity.Iter().Select(static e => (e.entity_id, e)).ToDictionary();
		foreach (var circle in ctx.Db.circle.Iter())
		{
			//var span = new SpacetimeDB.LogStopwatch("collisions");
			var circleEntity = entities[circle.entity_id];
			foreach (var (_, otherEntity) in entities)
			{
				if (otherEntity.entity_id == circleEntity.entity_id)
				{
					continue;
				}

				if (IsOverlapping(circleEntity, otherEntity))
				{
					// Check to see if we're overlapping with another circle owned by another player
					var otherCircle = ctx.Db.circle.entity_id.Find(otherEntity.entity_id);
					if (otherCircle.HasValue)
					{
						if (otherCircle.Value.player_id != circle.player_id)
						{
							var massRatio = (float) otherEntity.mass / circleEntity.mass;
							if (massRatio < MINIMUM_SAFE_MASS_RATIO)
							{
								schedule_consume_entity(ctx, circleEntity.entity_id, otherEntity.entity_id);
							}
						}
					}
					else
					{
						schedule_consume_entity(ctx, circleEntity.entity_id, otherEntity.entity_id);
					}
				}
			}
			//span.End();
		}

		//span.End();
	}

	private static void schedule_consume_entity(ReducerContext ctx, int consumerId, int consumedId)
	{
		ctx.Db.consume_entity_timer.Insert(new ConsumeEntityTimer
		{
			scheduled_at = new ScheduleAt.Time(DateTimeOffset.Now),
			consumer_entity_id = consumerId,
			consumed_entity_id = consumedId,
		});
	}

	[Reducer]
	public static void ConsumeEntity(ReducerContext ctx, ConsumeEntityTimer request)
	{
		var consumedEntity = ctx.Db.entity.entity_id.Find(request.consumed_entity_id) ??
		                     throw new Exception("Consumed entity doesn't exist");
		var consumerEntity = ctx.Db.entity.entity_id.Find(request.consumer_entity_id) ??
		                     throw new Exception("Consumer entity doesn't exist");
        
        ctx.Db.consume_entity_event.Insert(new ConsumeEntityEvent
        {
            consumed_entity_id = consumedEntity.entity_id,
            consumer_entity_id = consumerEntity.entity_id,
        });

		consumerEntity.mass += consumedEntity.mass;
		DestroyEntity(ctx, consumedEntity.entity_id);
		ctx.Db.entity.entity_id.Update(consumerEntity);
	}

	public static void DestroyEntity(ReducerContext ctx, int entityId)
	{
		ctx.Db.food.entity_id.Delete(entityId);
		ctx.Db.circle.entity_id.Delete(entityId);
		ctx.Db.entity.entity_id.Delete(entityId);
	}

	[Reducer]
	public static void PlayerSplit(ReducerContext ctx)
	{
		var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Sender has no player");
		var circles = ctx.Db.circle.player_id.Filter(player.player_id).ToList();
		var circleCount = circles.Count;
		if (circleCount >= MAX_CIRCLES_PER_PLAYER)
		{
			return;
		}

		foreach (var t in circles)
		{
			var circle = t;
			var circleEntity = ctx.Db.entity.entity_id.Find(circle.entity_id) ??
			                   throw new Exception("Circle has no entity");
			if (circleEntity.mass >= MIN_MASS_TO_SPLIT * 2)
			{
				var halfMass = circleEntity.mass / 2;
				SpawnCircleAt(
					ctx,
					circle.player_id,
					halfMass,
					circleEntity.position + circle.direction,
					ctx.Timestamp
				);
				circleEntity.mass -= halfMass;
				circle.last_split_time = ctx.Timestamp;
				ctx.Db.circle.entity_id.Update(circle);
				ctx.Db.entity.entity_id.Update(circleEntity);
				circleCount += 1;
				if (circleCount >= MAX_CIRCLES_PER_PLAYER)
				{
					break;
				}
			}
		}

		var duration = new TimeDuration { Microseconds = (long) (SPLIT_RECOMBINE_DELAY_SEC * 1_000_000) };
		var triggerAt = ctx.Timestamp + duration;
		ctx.Db.circle_recombine_timer.Insert(new CircleRecombineTimer
		{
			scheduled_at = new ScheduleAt.Time(triggerAt),
			player_id = player.player_id,
		});

		Log.Warn("Player split!");
	}

	[Reducer]
	public static void SpawnFood(ReducerContext ctx, SpawnFoodTimer timer)
	{
		if (ctx.Db.player.Count == 0) //Are there no players yet?
		{
			return;
		}

		var worldSize = (ctx.Db.config.id.Find(0) ?? throw new Exception("Config not found")).world_size;

		var rng = ctx.Rng;
		var foodCount = ctx.Db.food.Count;
		while (foodCount < TARGET_FOOD_COUNT)
		{
			var foodMass = rng.Range(FOOD_MASS_MIN, FOOD_MASS_MAX);
			var foodRadius = MassToRadius(foodMass);
			var x = rng.Range(foodRadius, worldSize - foodRadius);
			var y = rng.Range(foodRadius, worldSize - foodRadius);
			var entity = ctx.Db.entity.Insert(new Entity
			{
				position = new DbVector2(x, y),
				mass = foodMass,
			});
			ctx.Db.food.Insert(new Food
			{
				entity_id = entity.entity_id,
			});
			foodCount++;
			Log.Info($"Spawned food! {entity.entity_id}");
		}
	}

	[Reducer]
	public static void CircleDecay(ReducerContext ctx, CircleDecayTimer timer)
	{
		foreach (var circle in ctx.Db.circle.Iter())
		{
			var circleEntity = ctx.Db.entity.entity_id.Find(circle.entity_id) ??
			                   throw new Exception("Entity not found");
			if (circleEntity.mass <= START_PLAYER_MASS)
			{
				continue;
			}
			circleEntity.mass = (int)(circleEntity.mass * 0.99f);
			ctx.Db.entity.entity_id.Update(circleEntity);
		}
	}

	public static DbVector2 CalculateCenterOfMass(IEnumerable<Entity> entities)
	{
		var array = entities.ToArray();
		var totalMass = array.Sum(static e => e.mass);
		var centerOfMass = array.Select(static e => e.position * e.mass).Aggregate(static (a, b) => a + b);
		return centerOfMass / totalMass;
	}

	[Reducer]
	public static void CircleRecombine(ReducerContext ctx, CircleRecombineTimer timer)
	{
		var circles = ctx.Db.circle.player_id.Filter(timer.player_id).ToList();
		var recombiningEntities = circles
			.Where(c => ctx.Timestamp.TimeDurationSince(c.last_split_time).Microseconds / 1_000_000f >= SPLIT_RECOMBINE_DELAY_SEC)
			.Select(c => ctx.Db.entity.entity_id.Find(c.entity_id) ?? throw new Exception())
			.ToArray();
		if (recombiningEntities.Length <= 1)
		{
			return; //No circles to recombine
		}

		var baseEntityId = recombiningEntities[0].entity_id;

		for (int i = 1; i < recombiningEntities.Length; i++)
		{
			schedule_consume_entity(ctx, baseEntityId, recombiningEntities[i].entity_id);
		}
	}

	#endregion

	#region Views

	//TODO: generate fails due to the return type?
	/*[View(Accessor = "GetPlayerMass", Public = true)]
	public static int? GetPlayerMass(ViewContext ctx)
	{
		var player = ctx.Db.player.identity.Find(ctx.Sender).Expect("player not found");

		var mass = 0;
		foreach (var circle in ctx.Db.circle.player_id.Filter(player.player_id))
		{
			if (ctx.Db.entity.entity_id.Find(circle.entity_id) is { } entity)
			{
				mass += entity.mass;
			}
		}

		return mass;
	}*/

	#endregion

	extension(Random rng)
	{
		private float Range(float min, float max) => rng.NextSingle() * (max - min) + min;
		private int Range(int min, int max) => (int)rng.NextInt64(min, max);
	}

	extension<T>(T? o) where T : struct
	{
		private T Expect(string msg) => o! ?? throw new ArgumentNullException(msg);
	}

	extension<T>(T? o) where T : class
	{
		private T Expect(string msg) => o! ?? throw new ArgumentNullException(msg);
	}
}