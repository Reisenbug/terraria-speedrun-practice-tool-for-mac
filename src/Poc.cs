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
        Console.WriteLine("[Poc] Loading game assembly: " + gameExe);
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
        if (saveField != null)
        {
            saveField.SetValue(null, savePath);
        }

        var worker = new Thread(() =>
        {
            try
            {
                WaitForRenderSystemReady(game);
                SetupActivePlayer(game);
                for (int i = 1; i <= 3; i++)
                {
                    Console.WriteLine("[Poc] ===== Generation round " + i + " =====");
                    try
                    {
                        RunHeadlessWorldGen(game, savePath);
                    }
                    catch (Exception roundEx)
                    {
                        Console.WriteLine("[Poc] Round " + i + " FAILED: " + roundEx);
                        if (roundEx is ThreadAbortException)
                        {
                            Thread.ResetAbort();
                        }
                    }
                }
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

        Console.WriteLine("[Poc] Invoking game entry point...");
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

        Console.WriteLine("[Poc] ActivePlayerFileData set up: " + (playerFileData != null));
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
            if (instance != null)
            {
                object renderer = tilesRendererField.GetValue(instance);
                if (renderer != null)
                {
                    Console.WriteLine("[Poc] TilesRenderer ready after " + sw.Elapsed.TotalSeconds + "s");
                    return;
                }
            }
            Thread.Sleep(500);
            if (sw.Elapsed.TotalSeconds > 60)
            {
                Console.WriteLine("[Poc] Gave up waiting for TilesRenderer after 60s");
                return;
            }
        }
    }

    static void RunHeadlessWorldGen(Assembly game, string savePath)
    {
        Type mainType = game.GetType("Terraria.Main");
        Type worldGenType = game.GetType("Terraria.WorldGen");
        Type worldFileType = game.GetType("Terraria.IO.WorldFile");
        Type worldFileDataType = game.GetType("Terraria.IO.WorldFileData");
        Type worldGeneratorType = game.GetType("Terraria.WorldBuilding.WorldGenerator");
        Type controllerType = worldGeneratorType.GetNestedType("Controller", BindingFlags.Public | BindingFlags.NonPublic);

        MethodInfo setWorldSize = worldGenType.GetMethod("SetWorldSize", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo gameModeProp = mainType.GetProperty("GameMode", BindingFlags.Public | BindingFlags.Static);
        FieldInfo worldNameField = mainType.GetField("worldName", BindingFlags.Public | BindingFlags.Static);
        FieldInfo activeWorldFileDataField = mainType.GetField("ActiveWorldFileData", BindingFlags.Public | BindingFlags.Static);
        MethodInfo createMetadata = worldFileType.GetMethod("CreateMetadata", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setSeed = worldFileDataType.GetMethod("SetSeed", new[] { typeof(string) });
        MethodInfo createNewWorld = worldGenType.GetMethod("CreateNewWorld", BindingFlags.Public | BindingFlags.Static);

        string worldName = "PocGenTest_" + DateTime.Now.Ticks;

        setWorldSize.Invoke(null, new object[] { 0 }); // 0 = Small
        gameModeProp.SetValue(null, 0, null); // Normal
        worldNameField.SetValue(null, worldName);

        int gameMode = (int)gameModeProp.GetValue(null, null);
        object metadata = createMetadata.Invoke(null, new object[] { worldName, false, gameMode });
        activeWorldFileDataField.SetValue(null, metadata);
        setSeed.Invoke(metadata, new object[] { "" }); // random seed

        object controller = Activator.CreateInstance(controllerType, new object[] { null });
        FieldInfo pausedField = controllerType.GetField("Paused", BindingFlags.Public | BindingFlags.Instance);
        if (pausedField != null) pausedField.SetValue(controller, false);

        Console.WriteLine("[Poc] Starting world generation: " + worldName);
        var sw = Stopwatch.StartNew();

        object taskObj = createNewWorld.Invoke(null, new object[] { null, controller, null });
        var task = (System.Threading.Tasks.Task)taskObj;

        int pollCount = 0;
        while (!task.IsCompleted)
        {
            Thread.Sleep(1000);
            pollCount++;
            if (pollCount > 120)
            {
                Console.WriteLine("[Poc] Giving up after 120s poll.");
                break;
            }
        }
        if (task.IsFaulted)
        {
            Console.WriteLine("[Poc] Task faulted: " + task.Exception);
        }

        sw.Stop();
        Console.WriteLine("[Poc] World generation finished in " + sw.Elapsed.TotalSeconds + "s");

        var detectSw = Stopwatch.StartNew();
        var detector = new PyramidDetector(game);
        int sandstoneCount = detector.CountSandstoneBricks();
        detectSw.Stop();
        Console.WriteLine("[Poc] In-memory pyramid scan: " + sandstoneCount + " sandstone bricks, took " + detectSw.Elapsed.TotalSeconds + "s. Pyramid found: " + (sandstoneCount >= 1));
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
