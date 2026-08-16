using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

public static class Poc
{
    static string AssemblyFolder;

    public static void Main(string[] args)
    {
        AssemblyFolder = AppDomain.CurrentDomain.BaseDirectory;
        AppDomain.CurrentDomain.AssemblyResolve += ResolveHandler;

        string gameExe = args.Length > 0 ? args[0] : "Terraria.exe";
        Assembly game = Assembly.UnsafeLoadFrom(gameExe);

        foreach (var resName in game.GetManifestResourceNames())
        {
            if (resName.Contains(".dll"))
            {
                using (var s = game.GetManifestResourceStream(resName))
                {
                    var buf = new byte[s.Length];
                    s.Read(buf, 0, buf.Length);
                    Assembly.Load(buf);
                }
            }
        }

        var progType = game.GetType("Terraria.Program");
        var saveField = progType?.GetField("SavePath");
        string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "My Games", "Terraria");
        if (saveField != null) saveField.SetValue(null, savePath);

        var worker = new Thread(() =>
        {
            try
            {
                WaitForRenderSystemReady(game);
                SetupActivePlayer(game);
                LoadKnownWorldAndDetect(game);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Poc] FAILED: " + ex);
            }
            Console.WriteLine("[Poc] Done, exiting process.");
            Environment.Exit(0);
        });
        worker.IsBackground = true;
        worker.Start();

        game.EntryPoint.Invoke(null, new object[] { new string[0] });
    }

    static void SetupActivePlayer(Assembly game)
    {
        Type mainType = game.GetType("Terraria.Main");
        Type playerType = game.GetType("Terraria.Player");
        Type playerFileDataType = game.GetType("Terraria.IO.PlayerFileData");

        object player = Activator.CreateInstance(playerType);
        FieldInfo nameField = playerType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
        nameField.SetValue(player, "PocPlayer");

        MethodInfo createAndSave = playerFileDataType.GetMethod("CreateAndSave", BindingFlags.Public | BindingFlags.Static);
        object playerFileData = createAndSave.Invoke(null, new object[] { player });

        FieldInfo activePlayerField = mainType.GetField("ActivePlayerFileData", BindingFlags.Public | BindingFlags.Static);
        activePlayerField.SetValue(null, playerFileData);
    }

    static void WaitForRenderSystemReady(Assembly game)
    {
        Type mainType = game.GetType("Terraria.Main");
        FieldInfo instanceField = mainType.GetField("instance", BindingFlags.Public | BindingFlags.Static);
        FieldInfo tilesRendererField = mainType.GetField("TilesRenderer", BindingFlags.Public | BindingFlags.Instance);

        var sw = Stopwatch.StartNew();
        while (true)
        {
            object instance = instanceField.GetValue(null);
            if (instance != null && tilesRendererField.GetValue(instance) != null)
            {
                Console.WriteLine("[Poc] Game ready after " + sw.Elapsed.TotalSeconds + "s");
                return;
            }
            Thread.Sleep(500);
            if (sw.Elapsed.TotalSeconds > 60) return;
        }
    }

    static void LoadKnownWorldAndDetect(Assembly game)
    {
        string worldPath = "/Users/lhy/Library/Application Support/Terraria/Worlds/s_rand_20260816_181649_1.wld";
        Console.WriteLine("[Poc] Loading known world: " + worldPath);
        Console.WriteLine("[Poc] File exists: " + File.Exists(worldPath));

        Type mainType = game.GetType("Terraria.Main");
        Type worldFileType = game.GetType("Terraria.IO.WorldFile");
        Type worldFileDataType = game.GetType("Terraria.IO.WorldFileData");

        FieldInfo activeWorldFileDataField = mainType.GetField("ActiveWorldFileData", BindingFlags.Public | BindingFlags.Static);
        MethodInfo loadWorld = worldFileType.GetMethod("LoadWorld", Type.EmptyTypes);

        ConstructorInfo ctor = worldFileDataType.GetConstructor(new[] { typeof(string), typeof(bool) });
        object worldFileData = ctor.Invoke(new object[] { worldPath, false });
        activeWorldFileDataField.SetValue(null, worldFileData);

        Console.WriteLine("[Poc] Calling WorldFile.LoadWorld()...");
        loadWorld.Invoke(null, null);
        Console.WriteLine("[Poc] LoadWorld() returned.");

        FieldInfo maxTilesXField = mainType.GetField("maxTilesX", BindingFlags.Public | BindingFlags.Static);
        FieldInfo maxTilesYField = mainType.GetField("maxTilesY", BindingFlags.Public | BindingFlags.Static);
        Console.WriteLine("[Poc] After load: maxTilesX=" + maxTilesXField.GetValue(null) + " maxTilesY=" + maxTilesYField.GetValue(null));

        var detector = new PyramidDetector(game);
        Console.WriteLine("[Poc] tile array dims: " + detector.SizeX + " x " + detector.SizeY);
        int count = detector.CountSandstoneBricks();
        Console.WriteLine("[Poc] Sandstone Brick count in KNOWN-PYRAMID world: " + count);
    }

    static Assembly ResolveHandler(object sender, ResolveEventArgs args)
    {
        string simpleName = args.Name.Split(',')[0];
        string path = Path.Combine(AssemblyFolder, simpleName + ".dll");
        if (File.Exists(path))
        {
            try { return Assembly.UnsafeLoadFrom(path); } catch { return null; }
        }
        return null;
    }
}
