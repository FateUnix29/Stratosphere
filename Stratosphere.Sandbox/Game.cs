using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Prototypes;
using Stratosphere.Core.Logging;

namespace Stratosphere.Sandbox;

public sealed class Game
{
    public World World { get; }
    public GameStateSystem State { get; }
    public ConsoleRenderSystem Renderer { get; }

    private readonly CellIndexSystem _cells;

    private Game(World world)
    {
        World = world;
        State = world.GetSystem<GameStateSystem>();
        Renderer = world.GetSystem<ConsoleRenderSystem>();
        _cells = world.GetSystem<CellIndexSystem>();
    }

    public static Game Create(int sizeX, int sizeY, string prototypeDirectory, int? seed = null)
    {
        var prototypes = new PrototypeManager();
        prototypes.ComponentFactory.RegisterAssembly(typeof(Game).Assembly);
        prototypes.LoadDirectory(prototypeDirectory);
        prototypes.Resolve();

        LogManager logs = new() { DefaultLevel = LogLevel.Debug };
        FileSink fileSink = new("stratosandbox.log");
        logs.AddSink(fileSink);

        var world = new World(prototypes, logs);
        world.AddSystemsFromAssembly(typeof(Game).Assembly);
        world.InitializeSystems();
        world.GetSystem<GameStateSystem>().Configure(sizeX, sizeY, seed);

        var game = new Game(world);
        game.Populate();
        return game;
    }

    private void Populate()
    {
        int area = State.SizeX * State.SizeY;

        var player = World.Spawn("player");
        var position = World.Get<PositionComponent>(player);
        position.X = State.SizeX / 2; /* implicit cast to int */
        position.Y = State.SizeY / 2;
        State.Player = player;
        _cells.Invalidate();

        for (int i = 0; i < (int) Math.Round(area * 0.01); i++) PlaceSomewhereEmpty("crowbar");
        for (int i = 0; i < (int) Math.Round(area * 0.02); i++) PlaceSomewhereEmpty("rock");
    }

    private void PlaceSomewhereEmpty(string prototypeId)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int x = State.Rng.Next(State.SizeX), y = State.Rng.Next(State.SizeY);
            if (_cells.SolidAt(x, y).IsValid()) continue;

            var entity = World.Spawn(prototypeId);
            var position = World.Get<PositionComponent>(entity);
            position.X = x;
            position.Y = y;
            _cells.Invalidate();
            return;
        }
    }

    public void HandleKey(ConsoleKey key)
    {
        State.ClearInspect();

        switch (key)
        {
            case ConsoleKey.W: Move(0, -1); break;
            case ConsoleKey.A: Move(-1, 0); break;
            case ConsoleKey.S: Move(0, 1); break;
            case ConsoleKey.D: Move(1, 0); break;
            case ConsoleKey.E:
                var use = new UseEvent();
                World.RaiseLocalEvent(State.Player, ref use);
                break;
            case ConsoleKey.Q:
                var drop = new DropEvent();
                World.RaiseLocalEvent(State.Player, ref drop);
                break;
            case ConsoleKey.Escape:
                State.Quit();
                return;
            default:
                return;
        }

        World.Tick();
    }

    private void Move(int dx, int dy)
    {
        var attempt = new MoveAttemptEvent { Dx = dx, Dy = dy };
        World.RaiseLocalEvent(State.Player, ref attempt);

        if (attempt.Blocker.IsValid())
            State.Inspect(attempt.Blocker);
    }

    public void Run()
    {
        Draw();
        while (State.Running)
        {
            HandleKey(Console.ReadKey(true).Key);
            Draw();
        }

        Console.WriteLine("Thank you! Hope you had fun.");
    }

    private void Draw()
    {
        Console.Clear();
        Console.Write(Renderer.Render());
    }
}
