/* Basic command line test game made with Stratosphere's core ECS system. */

using Stratosphere.Core;
using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.BuiltInComponents;
using Stratosphere.Core.ECS.Prototypes;

new BasicGame(100, 50);

public sealed class BasicGame
{
    PrototypeManager prototypes = new PrototypeManager();
    World world;
    Random rng = new Random();

    public World InitializePrototypes() {
        var youPrototype = new EntityPrototype("player", "You", "...It's you.")
            .With(new HealthComponent() { Hp = 100 })
            .With(new PositionComponent())
            .With(new FacingComponent())
            .With(new HandComponent())
            .With(new PlayerComponent());
        var enemyPrototype = new EntityPrototype("enemy", "Unknown", "A threat of some kind. Defend yourself.")
            .With(new HealthComponent() { Hp = 80 })
            .With(new PositionComponent())
            .With(new EnemyComponent() { AttackDamage = 10 });
        var crowbar = new EntityPrototype("crowbar", "Crowbar", "A heavy iron lever that does fine as a weapon in a pinch.")
            .With(new PositionComponent())
            .With(new SwingableComponent() { SwingDamage = 20 })
            .With(new ItemComponent())
            .With(new TagsComponent() { Tags = new Dictionary<string, object>() { {"doNotRenderIfHeld", true} } });
        var rock = new EntityPrototype("rock", "Rock", "A random rock. You could probably throw it.")
            .With(new PositionComponent())
            .With(new ThrowableComponent() { ThrowDamage = 10 })
            .With(new FacingComponent())
            .With(new ItemComponent());

        prototypes.Register(youPrototype);
        prototypes.Register(enemyPrototype);
        prototypes.Register(crowbar);
        prototypes.Register(rock);
        return new World(prototypes);
    }

    public void SprinkleObjects(int playsizeX, int playsizeY)
    {
        /* frequency of crowbar should be... let's say... 10% of playsize area. */
        /* never fucking mind. */
        int area = playsizeX*playsizeY;

        for (int i = 0; i < (int)Math.Round(area*0.01); i++)
        {
            EntityUid crowbar = world.Spawn("crowbar");
            var posComp = world.Get<PositionComponent>(crowbar); /* throwing is intentional */
            posComp.X = rng.Next(0, playsizeX);
            posComp.Y = rng.Next(0, playsizeY);
        }
        /* rocks, 20%. */
        for (int i = 0; i < (int)Math.Round(area*0.02); i++)
        {
            EntityUid rock = world.Spawn("rock");
            var posComp = world.Get<PositionComponent>(rock);
            posComp.X = rng.Next(0, playsizeX);
            posComp.Y = rng.Next(0, playsizeY);
        }
    }

    public void PlaceEnemySomewhere(int playsizeX, int playsizeY)
    {
        EntityUid enemy = world.Spawn("enemy");
        var posComp = world.Get<PositionComponent>(enemy);
        posComp.X = rng.Next(0, playsizeX);
        posComp.Y = rng.Next(0, playsizeY);
    }

    public EntityUid PlacePlayer(int playsizeX, int playsizeY)
    {
        EntityUid player = world.Spawn("player");
        var posComp = world.Get<PositionComponent>(player);
        posComp.X = (int)Math.Round(playsizeX*0.5);
        posComp.Y = (int)Math.Round(playsizeY*0.5);
        return player;
    }

    public void RenderToScreen(int playsizeX, int playsizeY, string inspectName, string inspectDescription, int? theoryPosX = null, int? theoryPosY = null)
    {
        var thingsOnScreen = new Dictionary<(int,int), EntityUid>();
        /* Collect all the things we need to render. */
        foreach (var (uid, posComp) in world.Query<PositionComponent>())
        {
            if (world.TryGet<TagsComponent>(uid, out var tags) && world.TryGet<ItemComponent>(uid, out var item))
                if (tags.Tags.TryGetValue("doNotRenderIfHeld", out object? value))
                    if ((bool)value && item.BeingHeldBy != EntityUid.INVALID) continue;

            if (!thingsOnScreen.TryAdd((posComp.X, posComp.Y), uid))
            {
                Console.WriteLine($"failed to add {world.PrototypeOf(uid)} {uid} to render list");
            }
        }
        Console.Clear();

        for (int y = 0; y < playsizeY; y++)
        {
            for (int x = 0; x < playsizeX; x++)
            {
                char character = '.';

                if (theoryPosX != null && theoryPosY != null && theoryPosX == x && theoryPosY == y) character = '@';

                if (thingsOnScreen.TryGetValue((x, y), out var entity))
                {
                    /* okay but what are you. */
                    if (world.TryGet<MetadataComponent>(entity, out var meta))
                    {
                        switch (meta.EntityPrototype)
                        {
                            case "player":  character = '#'; break;
                            case "enemy":   character = '!'; break;
                            case "crowbar": character = '/'; break;
                            case "rock":    character = 'o'; break;
                        }
                    }
                }

                Console.Write(character);
            }
            Console.Write('\n');
        }

        if (!string.IsNullOrEmpty(inspectName) || !string.IsNullOrEmpty(inspectDescription))
            Console.WriteLine($"\n\n{inspectName} | {inspectDescription}");
    }

    public BasicGame(int playsizeX, int playsizeY)
    {
        world = InitializePrototypes();
        SprinkleObjects(playsizeX, playsizeY);
        EntityUid player = PlacePlayer(playsizeX, playsizeY);
        bool shouldContinue = true;
        GameTick lastDamageTick = GameTick.Zero;
        RenderToScreen(playsizeX, playsizeY, string.Empty, string.Empty);
        while (shouldContinue)
        {
            List<EntityUid> needsToBeCleanedUp = new();

            int xDelta = 0;
            int yDelta = 0;
            bool useItem = false;
            bool dropItem = false;
            switch (Console.ReadKey(true).Key) /* blocking */
            {
                case ConsoleKey.W: yDelta = -1; break;
                case ConsoleKey.A: xDelta = -1; break;
                case ConsoleKey.S: yDelta = 1; break;
                case ConsoleKey.D: xDelta = 1; break;
                case ConsoleKey.E: useItem = true; break;
                case ConsoleKey.Q: dropItem = true; break;
            }

            PositionComponent pos = world.Get<PositionComponent>(player);
            HandComponent hand = world.Get<HandComponent>(player);
            FacingComponent facing = world.Get<FacingComponent>(player);

            /* Now apply player movement. */
            if (Math.Abs(xDelta) > 0 || Math.Abs(yDelta) > 0)
            {
                facing.FacingX = xDelta;
                facing.FacingY = yDelta;
            }

            int playerFutureX = pos.X + xDelta;
            int playerFutureY = pos.Y + yDelta;
            bool canMove = true;

            string inspectName = string.Empty;
            string inspectDescription = string.Empty;

            if (playerFutureX >= 0 && playerFutureX < playsizeX &&
                playerFutureY >= 0 && playerFutureY < playsizeY)
            {
                foreach (var (ent, entPos) in world.Query<PositionComponent>())
                    if (entPos.X == playerFutureX && entPos.Y == playerFutureY)
                        if (world.TryGet<ItemComponent>(ent, out var heldItemComp)
                            && heldItemComp.BeingHeldBy != EntityUid.INVALID
                            && hand.Item == ent)
                        { }

                        /* It's weird, but it's a little trick to ensure no nullable bullshit happens. */
                        else {
                            canMove = false;
                            if (world.TryGet<MetadataComponent>(ent, out var metadataComp))
                            {
                                inspectName = metadataComp.EntityName;
                                inspectDescription = metadataComp.EntityDescription;
                            }
                        }
            }
            else { canMove = false; }

            if (canMove)
            {
                pos.X = playerFutureX;
                pos.Y = playerFutureY;
            }

            int theoreticalPositionX = pos.X + facing.FacingX;
            int theoreticalPositionY = pos.Y + facing.FacingY;

            if (hand.Item != EntityUid.INVALID)
                if (theoreticalPositionX >= 0 && theoreticalPositionX < playsizeX &&
                    theoreticalPositionY >= 0 && theoreticalPositionY < playsizeY)
                {
                    if (world.TryGet<PositionComponent>(hand.Item, out var itemPos))
                    {
                        itemPos.X = theoreticalPositionX;
                        itemPos.Y = theoreticalPositionY;
                    }
                }

            /* 50% chance to spawn an enemy every 10 ticks. */
            if (world.CurTick.Value % 15 == 0 && rng.Next(0, 2) == 0)
                PlaceEnemySomewhere(playsizeX, playsizeY);

            if (useItem && hand.Item != EntityUid.INVALID)
            {
                if (world.TryGet<SwingableComponent>(hand.Item, out var swingComp))
                {
                    /* Locate the cell in front of the player. */
                    if (theoreticalPositionX > 0 && theoreticalPositionX < playsizeX &&
                        theoreticalPositionY > 0 && theoreticalPositionY < playsizeY)
                    {
                        /* Iterate through all of the enemies. If they're on this cell, decrease health. */
                        /* TODO: May be better to make a Query based off of prototype? */
                        foreach (var (enemy, _, enemyPos) in world.Query<EnemyComponent, PositionComponent>())
                            if (enemyPos.X == theoreticalPositionX && enemyPos.Y == theoreticalPositionY)
                                if (world.TryGet<HealthComponent>(enemy, out var healthComp))
                                    healthComp.Hp -= swingComp.SwingDamage; /* All of that just to decrease health. */
                    }
                }
                else if (world.TryGet<ThrowableComponent>(hand.Item, out var throwComp))
                {
                    /* Really, we don't need to know about the player's theoretical cell.
                       We just need to set positions and toss. */

                    if (world.TryGet<FacingComponent>(hand.Item, out var facingComp)
                     && world.TryGet<ItemComponent>(hand.Item, out var itemComp))
                    {
                        facingComp.FacingX = facing.FacingX;
                        facingComp.FacingY = facing.FacingY;
                        throwComp.RemainingThrowTime = 7; /* Will continue on for n more ticks. */
                        hand.Item = EntityUid.INVALID;
                        itemComp.BeingHeldBy = EntityUid.INVALID;
                        throwComp.HasBeenThrown = true;
                    }
                }
            }
            else if (useItem && hand.Item == EntityUid.INVALID)
            {
                if (theoreticalPositionX >= 0 && theoreticalPositionX < playsizeX &&
                    theoreticalPositionY >= 0 && theoreticalPositionY < playsizeY)
                {
                    foreach (var (itemUid, itemComp, posComp) in world.Query<ItemComponent, PositionComponent>())
                        if (posComp.X == theoreticalPositionX && posComp.Y == theoreticalPositionY)
                        {
                            hand.Item = itemUid;
                            itemComp.BeingHeldBy = player;
                        }
                }
            }
            else if (dropItem && hand.Item != EntityUid.INVALID)
            {
                if (theoreticalPositionX >= 0 && theoreticalPositionX < playsizeX &&
                    theoreticalPositionY >= 0 && theoreticalPositionY < playsizeY)
                {
                    if (world.TryGet<PositionComponent>(hand.Item, out var posComp)
                     && world.TryGet<ItemComponent>(hand.Item, out var itemComp))
                    {
                        posComp.X = theoreticalPositionX;
                        posComp.Y = theoreticalPositionY;
                        itemComp.BeingHeldBy = EntityUid.INVALID;
                        hand.Item = EntityUid.INVALID;
                    }
                }
            }


            /* Throwables movement and collision. */
            foreach (var (throwUid, throwComp, throwFaceComp) in world.Query<ThrowableComponent, FacingComponent>())
            {
                if (world.TryGet<PositionComponent>(throwUid, out var throwPos) && throwComp.HasBeenThrown)
                {
                    /* Calculate theoretical position. */
                    int throwTheoryPositionX = throwPos.X + throwFaceComp.FacingX;
                    int throwTheoryPositionY = throwPos.Y + throwFaceComp.FacingY;
                    if (throwTheoryPositionX >= 0 && throwTheoryPositionX < playsizeX &&
                        throwTheoryPositionY >= 0 && throwTheoryPositionY < playsizeY &&
                        throwComp.RemainingThrowTime > 0)
                    {
                        bool hit = false;
                        foreach (var (ent, entPos) in world.Query<PositionComponent>())
                        {
                            if (entPos.X == throwTheoryPositionX && entPos.Y == throwTheoryPositionY)
                            {
                                if (world.TryGet<HealthComponent>(ent, out var healthComp) && world.Has<EnemyComponent>(ent))
                                    healthComp.Hp -= throwComp.ThrowDamage;
                                needsToBeCleanedUp.Add(throwUid);
                                hit = true;
                            }
                        }

                        if (hit) continue;

                        throwPos.X = throwTheoryPositionX;
                        throwPos.Y = throwTheoryPositionY;
                        throwComp.RemainingThrowTime -= 1;
                    }
                    else
                        /* This one has reached its end. */
                        needsToBeCleanedUp.Add(throwUid);
                }
            }

            if ((world.CurTick - lastDamageTick.Value).Value > 3)
                /* Begin healing. Ten every tick. */
                if (world.TryGet<HealthComponent>(player, out var playerHealth))
                    playerHealth.Hp = Math.Max(playerHealth.Hp + 10, 100);

            /* Now enemy movement. */
            foreach (var (enemy, enemyComp, enemyPos) in world.Query<EnemyComponent, PositionComponent>())
            {
                /* Find difference between enemy position and player position. */
                int diffX = pos.X - enemyPos.X;
                int diffY = pos.Y - enemyPos.Y;

                /* Normalize. */
                int absoluteX = Math.Abs(diffX);
                int absoluteY = Math.Abs(diffY);
                int normalizedDiffX = diffX / (absoluteX > 0 ? absoluteX : 1);
                int normalizedDiffY = diffY / (absoluteY > 0 ? absoluteY : 1);
                int enemyWantsX = enemyPos.X + normalizedDiffX;
                int enemyWantsY = enemyPos.Y + normalizedDiffY;

                bool canMoveEnemyX = true;
                bool canMoveEnemyY = true;
                foreach (var (ent, entPos) in world.Query<PositionComponent>())
                {
                    if (entPos.X == enemyWantsX && entPos.Y == enemyWantsY)
                    {
                        if (world.TryGet<HealthComponent>(ent, out var playerHealthComp) && world.Has<PlayerComponent>(ent))
                        {
                            playerHealthComp.Hp -= enemyComp.AttackDamage;
                            lastDamageTick = world.CurTick;
                        }
                    }

                    /* Determine in which way we can move, and which ways we are blocked, as opposed to a binary movement flag. */
                    
                    /* Horizontal. */ if (entPos.X == enemyWantsX && entPos.Y == enemyPos.Y) canMoveEnemyX = false;
                    /* Vertical. */ if (entPos.Y == enemyWantsY && entPos.X == enemyPos.X) canMoveEnemyY = false;
                }

                if (canMoveEnemyX && enemyWantsX > 0 && enemyWantsX < playsizeX && world.CurTick.Value % 2 == 0) enemyPos.X = enemyWantsX;
                if (canMoveEnemyY && enemyWantsY > 0 && enemyWantsY < playsizeY && world.CurTick.Value % 2 == 0) enemyPos.Y = enemyWantsY;

                /* dont mind me being clever with what i have */
                //inspectDescription = $"{absoluteX} {absoluteY} {enemyWantsX} {enemyWantsY} {canMoveEnemyX} {canMoveEnemyY}";
            }

            /* If anyone is below 0 health... */
            foreach (var (ent, healthComp) in world.Query<HealthComponent>())
                if (healthComp.Hp <= 0)
                    needsToBeCleanedUp.Add(ent);

            /* And, finally... */
            foreach (EntityUid needsCleaning in needsToBeCleanedUp)
            {
                if (world.Has<PlayerComponent>(needsCleaning)) shouldContinue = false;
                world.Destroy(needsCleaning);
            }

            RenderToScreen(playsizeX, playsizeY, inspectName, inspectDescription, theoreticalPositionX, theoreticalPositionY);
            world.Tick();
        }

        Console.WriteLine("Thank you! Hope you had fun.");
    }
}

public sealed class HealthComponent : Component { [DataField("hp")] public float Hp; }
public sealed class PositionComponent : Component { [DataField("x")] public int X; [DataField("y")] public int Y; }
public sealed class SwingableComponent : Component { [DataField("swingDamage")] public float SwingDamage; }
public sealed class ThrowableComponent : Component { [DataField("throwDamage")] public float ThrowDamage; public int RemainingThrowTime; public bool HasBeenThrown = false; }
public sealed class FacingComponent : Component { public int FacingX = 0; public int FacingY = -1; }
public sealed class HandComponent : Component { public EntityUid Item = EntityUid.INVALID; }
public sealed class ItemComponent : Component { public EntityUid BeingHeldBy = EntityUid.INVALID; }
public sealed class TagsComponent : Component { [DataField("tags")] public required Dictionary<string, object> Tags; }
public sealed class EnemyComponent : Component { [DataField("attackDamage")] public float AttackDamage; }
public sealed class PlayerComponent : Component { }
