// Lib.cs defines the tables and reducers for the server

using SpacetimeDB;

public static partial class Module 
{
  // Tables are defined thusly
  [Table(Name = "Config", Public = true)]
  public partial struct Config
  {
    // This is how you define the primary key of a table
    [PrimaryKey]
    public uint id;
    public ulong worldSize;
  }

  [Table(Name = "Entity", Public = true)]
  public partial struct Entity
  {
    // AutoInc = auto increment
    [PrimaryKey, AutoInc]
    public uint entityId;
    public DbVector2 position;
    public uint mass;

  }

  [Table(Name = "Circle", Public = true)]
  public partial struct Circle
  {
    [PrimaryKey]
    public uint entityId;
    [SpacetimeDB.Index.BTree]
    public uint playerId;
    public DbVector2 direction;
    public float speed;
    public SpacetimeDB.Timestamp lastSplitTime;
  }

  [Table(Name = "Food", Public = true)]
  public partial struct Food
  {
    [PrimaryKey]
    public uint entityId;
  }

  // Two tables with the same type for their rows
  [Table(Name = "Player", Public = true)]
  [Table(Name = "LoggedOutPlayer")]
  public partial struct Player
  {
      [PrimaryKey]
      public Identity identity;
      [Unique, AutoInc]
      public uint playerId;
      public string name;
  }

  [Table(Name = "SpawnFoodTimer", Scheduled = nameof(SpawnFood), ScheduledAt = nameof(scheduledAt))]
  public partial struct SpawnFoodTimer
{
    [PrimaryKey, AutoInc]
    public ulong scheduledId;
    public ScheduleAt scheduledAt;
}

[Table(Name = "MoveAllPlayersTimer", Scheduled = nameof(MoveAllPlayers), ScheduledAt = nameof(scheduledAt))]
public partial struct MoveAllPlayersTimer
{
    [PrimaryKey, AutoInc]
    public ulong scheduledId;
    public ScheduleAt scheduledAt;
}

const uint START_PLAYER_SPEED = 10;

public static float MassToMaxMoveSpeed(uint mass) => 2f * START_PLAYER_SPEED / (1f + MathF.Sqrt((float)mass / START_PLAYER_MASS));

const float MINIMUM_SAFE_MASS_RATIO = 0.85f;

public static bool IsOverlapping(Entity a, Entity b)
{
    var dx = a.position.x - b.position.x;
    var dy = a.position.y - b.position.y;
    var distance_sq = dx * dx + dy * dy;

    var radius_a = MassToRadius(a.mass);
    var radius_b = MassToRadius(b.mass);
    
    // If the distance between the two circle centers is less than the
    // maximum radius, then the center of the smaller circle is inside
    // the larger circle. This gives some leeway for the circles to overlap
    // before being eaten.
    var max_radius = radius_a > radius_b ? radius_a: radius_b;
    return distance_sq <= max_radius * max_radius;
}

[Reducer]
public static void MoveAllPlayers(ReducerContext ctx, MoveAllPlayersTimer timer)
{
    var world_size = (ctx.Db.Config.id.Find(0) ?? throw new Exception("Config not found")).worldSize;

    // Handle player input
    foreach (var circle in ctx.Db.Circle.Iter())
    {
        var check_entity = ctx.Db.Entity.entityId.Find(circle.entityId);
        if (check_entity == null)
        {
            // This can happen if the circle has been eaten by another circle.
            continue;
        }
        var circle_entity = check_entity.Value;
        var circle_radius = MassToRadius(circle_entity.mass);
        var direction = circle.direction * circle.speed;
        var new_pos = circle_entity.position + direction * MassToMaxMoveSpeed(circle_entity.mass);
        circle_entity.position.x = Math.Clamp(new_pos.x, circle_radius, world_size - circle_radius);
        circle_entity.position.y = Math.Clamp(new_pos.y, circle_radius, world_size - circle_radius);

        // Check collisions
        foreach (var entity in ctx.Db.Entity.Iter())
        {
            if (entity.entityId == circle_entity.entityId)
            {
                continue;
            }
            if (IsOverlapping(circle_entity, entity))
            {
                // Check to see if we're overlapping with food
                if (ctx.Db.Food.entityId.Find(entity.entityId).HasValue) {
                    ctx.Db.Entity.entityId.Delete(entity.entityId);
                    ctx.Db.Food.entityId.Delete(entity.entityId);
                    circle_entity.mass += entity.mass;
                }
                
                // Check to see if we're overlapping with another circle owned by another player
                var other_circle = ctx.Db.Circle.entityId.Find(entity.entityId);
                if (other_circle.HasValue &&
                    other_circle.Value.playerId != circle.playerId)
                {
                    var mass_ratio = (float)entity.mass / circle_entity.mass;
                    if (mass_ratio < MINIMUM_SAFE_MASS_RATIO)
                    {
                        ctx.Db.Entity.entityId.Delete(entity.entityId);
                        ctx.Db.Circle.entityId.Delete(entity.entityId);
                        circle_entity.mass += entity.mass;
                    }
                }
            }
        }
        ctx.Db.Entity.entityId.Update(circle_entity);
    }
}
  
  // Reducers with ReducerKind.Init are only called
  // when a database is created
  [Reducer(ReducerKind.Init)]
  public static void Init(ReducerContext ctx)
  {
    Log.Info($"Initializing...");
    ctx.Db.Config.Insert(new Config { worldSize = 1000 });
    ctx.Db.SpawnFoodTimer.Insert(new SpawnFoodTimer 
    { 
      // ScheduleAt.Interval repeats this callback every 500ms
      // ScheduleAt.Time() can be used to call once
      scheduledAt = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(500))
    });
    ctx.Db.MoveAllPlayersTimer.Insert(new MoveAllPlayersTimer
    {
        scheduledAt = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(50))
    });
  }

[Reducer(ReducerKind.ClientConnected)]
public static void Connect(ReducerContext ctx)
{
    var player = ctx.Db.LoggedOutPlayer.identity.Find(ctx.Sender);
    if (player != null)
    {
        ctx.Db.Player.Insert(player.Value);
        ctx.Db.LoggedOutPlayer.identity.Delete(player.Value.identity);
    }
    else
    {
        ctx.Db.Player.Insert(new Player
        {
            identity = ctx.Sender,
            name = "",
        });
    }
}

[Reducer(ReducerKind.ClientDisconnected)]
public static void Disconnect(ReducerContext ctx)
{
    var player = ctx.Db.Player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
    // Remove any circles from the arena
    foreach (var circle in ctx.Db.Circle.playerId.Filter(player.playerId))
    {
        var entity = ctx.Db.Entity.entityId.Find(circle.entityId) ?? throw new Exception("Could not find circle");
        ctx.Db.Entity.entityId.Delete(entity.entityId);
        ctx.Db.Circle.entityId.Delete(entity.entityId);
    }
    ctx.Db.LoggedOutPlayer.Insert(player);
    ctx.Db.Player.identity.Delete(player.identity);
}

const uint FOOD_MASS_MIN = 2;
const uint FOOD_MASS_MAX = 4;
const uint TARGET_FOOD_COUNT = 600;

public static float MassToRadius(uint mass) => MathF.Sqrt(mass);

[Reducer]
public static void SpawnFood(ReducerContext ctx, SpawnFoodTimer _timer)
{
    if (ctx.Db.Player.Count == 0) //Are there no players yet?
    {
        return;
    }

    var world_size = (ctx.Db.Config.id.Find(0) ?? throw new Exception("Config not found")).worldSize;
    var rng = ctx.Rng;
    var food_count = ctx.Db.Food.Count;
    while (food_count < TARGET_FOOD_COUNT)
    {
        var food_mass = rng.Range(FOOD_MASS_MIN, FOOD_MASS_MAX);
        var food_radius = MassToRadius(food_mass);
        var x = rng.Range(food_radius, world_size - food_radius);
        var y = rng.Range(food_radius, world_size - food_radius);
        var entity = ctx.Db.Entity.Insert(new Entity()
        {
            position = new DbVector2(x, y),
            mass = food_mass,
        });
        ctx.Db.Food.Insert(new Food
        {
            entityId = entity.entityId,
        });
        food_count++;
        Log.Info($"Spawned food! {entity.entityId}");
    }
}

public static float Range(this Random rng, float min, float max) => rng.NextSingle() * (max - min) + min;

public static uint Range(this Random rng, uint min, uint max) => (uint)rng.NextInt64(min, max);
const uint START_PLAYER_MASS = 15;

[Reducer]
public static void EnterGame(ReducerContext ctx, string name)
{
    Log.Info($"Creating player with name {name}");
    var player = ctx.Db.Player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
    player.name = name;
    ctx.Db.Player.identity.Update(player);
    SpawnPlayerInitialCircle(ctx, player.playerId);
}

[Reducer]
public static void UpdatePlayerInput(ReducerContext ctx, DbVector2 direction)
{
    var player = ctx.Db.Player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");				
    foreach (var c in ctx.Db.Circle.playerId.Filter(player.playerId))
    {
        var circle = c;
        circle.direction = direction.Normalized;
        circle.speed = Math.Clamp(direction.Magnitude, 0f, 1f);
        ctx.Db.Circle.entityId.Update(circle);
    }
}

public static Entity SpawnPlayerInitialCircle(ReducerContext ctx, uint playerId)
{
    var rng = ctx.Rng;
    var world_size = (ctx.Db.Config.id.Find(0) ?? throw new Exception("Config not found")).worldSize;
    var player_start_radius = MassToRadius(START_PLAYER_MASS);
    var x = rng.Range(player_start_radius, world_size - player_start_radius);
    var y = rng.Range(player_start_radius, world_size - player_start_radius);
    return SpawnCircleAt(
        ctx,
        playerId,
        START_PLAYER_MASS,
        new DbVector2(x, y),
        ctx.Timestamp
    );
}

public static Entity SpawnCircleAt(ReducerContext ctx, uint playerId, uint mass, DbVector2 position, SpacetimeDB.Timestamp timestamp)
{
    var entity = ctx.Db.Entity.Insert(new Entity
    {
        position = position,
        mass = mass,
    });

    ctx.Db.Circle.Insert(new Circle
    {
        entityId = entity.entityId,
        playerId = playerId,
        direction = new DbVector2(0, 1),
        speed = 0f,
        lastSplitTime = timestamp,
    });
    return entity;
}

}
