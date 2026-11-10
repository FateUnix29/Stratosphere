using System.Diagnostics;
using System.Numerics;
using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Components;
using Stratosphere.Core.ECS.Prototypes;
using Stratosphere.Core.Logging;
using Stratosphere.Core.Maths;
using Stratosphere.Images;
using Stratosphere.Rendering;
using Stratosphere.Rendering.Components;
using Stratosphere.Rendering.OpenGL;
using Stratosphere.Rendering.Systems;
using Stratosphere.Rendering.Text;

const float Zoom = 12f;
const float ArtPixelsPerUnit = 32f;
string assets = Path.Combine(AppContext.BaseDirectory, "Assets");

using var logs = new LogManager { DefaultLevel = LogLevel.Debug };
logs.AddSink(new ConsoleSink());
logs.HookUnhandledExceptions();
var log = logs.GetSawmill("demo");

/* bullshit some font stuff in. */
string fontPath;
try { fontPath = args.Length > 0 ? args[0] : SystemFonts.Require(SystemFonts.CommonSans); }
catch (FileNotFoundException e) { log.Error(e.Message); return 1; }
log.Info($"Using font {fontPath}");
using var ttf = TtfFont.FromFile(fontPath);

GLBackend? backend = null;
var device = new RenderThread(
    () => backend = new GLBackend(new GLBackendSettings { Title = "Stratosphere Rendering Demo" }, logs.GetSawmill("gl")),
    log: logs.GetSawmill("render")
);

var textures = new TextureLibrary(device);
textures.Load("resomi", Path.Combine(assets, "Resomi.png"));
textures.Load("tandenian", Path.Combine(assets, "Tandenian_x4.png"));
//textures.Load("title", Path.Combine(assets, "title.png"));
textures.Load("heart", Path.Combine(assets, "yellow-heart.png"));
//textures.Load("bottom", Path.Combine(assets, "bottom.png"));

if (OperatingSystem.IsWindows()) log.Fatal("microsoft detected. you're fucked");
var fonts = new FontLibrary();
fonts.Add("small", BakedFont.Bake(textures, "font-small", ttf, 12f));
fonts.Add("mid", BakedFont.Bake(textures, "font-mid", ttf, 16f));
fonts.Add("large", BakedFont.Bake(textures, "font-large", ttf, 48f));

var prototypes = new PrototypeManager();
prototypes.ComponentFactory.RegisterAssembly(typeof(SpriteComponent).Assembly);
prototypes.LoadDirectory(assets);
prototypes.Resolve();

var world = new World(prototypes, logs);
world.AddSystemsFromAssembly(typeof(RenderSystem).Assembly);
world.InitializeSystems();

var renderer = world.GetSystem<RenderSystem>();
renderer.Device = device;
renderer.Textures = textures;
renderer.Fonts = fonts;

var resomi = world.Spawn("resomi");
var tandenian = world.Spawn("tandenian");
var title = world.Spawn(/*"title"*/ "empty");
var heart = world.Spawn("heart");
var bottom = world.Spawn(/*"bottom"*/ "empty");
world.Get<TransformComponent>(resomi).LocalPosition = new Vector3d(-0.6, 0.1, 0);
world.Get<TransformComponent>(tandenian).LocalPosition = new Vector3d(0.6, 0, 0);
world.Get<TransformComponent>(title).LocalPosition = new Vector3d(0, 0.5, 0);
world.Get<TransformComponent>(heart).LocalPosition = new Vector3d(0, 0, 0);
world.Get<TransformComponent>(bottom).LocalPosition = new Vector3d(0, -0.5, 0);

var titleComponent = world.Add<TextComponent>(title);
var bottomComponent = world.Add<TextComponent>(bottom);
titleComponent.Text = "Stratosphere"; titleComponent.Font = "large"; titleComponent.PixelsPerUnit = 48f; titleComponent.Color = Color.White; titleComponent.Scale = new Vector2(0.25f, 0.25f);
bottomComponent.Text = "Heavily inspired by RobustToolbox from Space Station 14"; bottomComponent.Font = "small"; bottomComponent.PixelsPerUnit = 48f; bottomComponent.Color = Color.White; bottomComponent.Scale = new Vector2(0.25f, 0.25f);

var eye = world.Spawn("eye");
world.Get<TransformComponent>(eye).LocalPosition = new Vector3d(0, 0, 10);
var camera = world.Get<CameraComponent>(eye);
camera.ClearColor = Color.Black; /* so you can change it from black */

log.Info($"Showing at {Zoom} zoom! Close the window to quit.");

var clock = Stopwatch.StartNew();
double last = clock.Elapsed.TotalSeconds;

while (!backend!.CloseRequested)
{
    var screen = device.GetTargetSize(RenderTargetHandle.Screen);
    camera.OrthographicSize = screen.Height / (ArtPixelsPerUnit*Zoom);

    double now = clock.Elapsed.TotalSeconds;
    world.Tick((float)(now - last));
    last = now;

    renderer.Render();
}

log.Info("Bye!");
return 0;
