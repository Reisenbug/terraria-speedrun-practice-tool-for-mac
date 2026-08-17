using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

// Generates N worlds and KEEPS ALL OF THEM regardless of detection result,
// so the user can manually verify each one in-game against the detector's verdict.
public static class Poc
{
    static string AssemblyFolder;

    public static void Main(string[] args)
    {
        AssemblyFolder = AppDomain.CurrentDomain.BaseDirectory;
        AppDomain.CurrentDomain.AssemblyResolve += ResolveHandler;

        string gameExe = args.Length > 0 ? args[0] : "Terraria.exe";
        int count = args.Length > 1 ? int.Parse(args[1]) : 5;
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
                GenerateKeepAll(game, count);
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

        MethodInfo getPlayerPath = mainType.GetMethod("GetPlayerPathFromName", BindingFlags.Public | BindingFlags.Static);
        string playerPath = (string)getPlayerPath.Invoke(null, new object[] { "BatchGenPlayer", false });

        object playerFileData;
        if (File.Exists(playerPath))
        {
            MethodInfo loadPlayer = playerType.GetMethod("LoadPlayer", new[] { typeof(string), typeof(bool) });
            playerFileData = loadPlayer.Invoke(null, new object[] { playerPath, false });
        }
        else
        {
            object player = Activator.CreateInstance(playerType);
            FieldInfo nameField = playerType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
            nameField.SetValue(player, "BatchGenPlayer");

            MethodInfo createAndSave = playerFileDataType.GetMethod("CreateAndSave", BindingFlags.Public | BindingFlags.Static);
            playerFileData = createAndSave.Invoke(null, new object[] { player });
        }

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
                Thread.Sleep(3000); // extra margin: Main.Map/TileEntity/Wiring subsystems can lag TilesRenderer readiness
                return;
            }
            Thread.Sleep(500);
            if (sw.Elapsed.TotalSeconds > 60) return;
        }
    }

    static void GenerateKeepAll(Assembly game, int count)
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
        FieldInfo worldGenParamEvilField = worldGenType.GetField("WorldGenParam_Evil", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo pathProp = worldFileDataType.GetProperty("Path", BindingFlags.Public | BindingFlags.Instance);

        Console.WriteLine("[Poc] Generating " + count + " worlds, KEEPING ALL for manual verification.");
        Console.WriteLine();

        int i = 1;
        int retries = 0;
        while (i <= count)
        {
            string worldName = "PocVerify_" + i;

            setWorldSize.Invoke(null, new object[] { 0 }); // Small
            gameModeProp.SetValue(null, 0, null); // Normal
            worldNameField.SetValue(null, worldName);
            worldGenParamEvilField.SetValue(null, 1); // force Crimson

            int gameMode = (int)gameModeProp.GetValue(null, null);
            object metadata = createMetadata.Invoke(null, new object[] { worldName, false, gameMode });
            activeWorldFileDataField.SetValue(null, metadata);
            setSeed.Invoke(metadata, new object[] { "" }); // random seed

            object controller = Activator.CreateInstance(controllerType, new object[] { null });
            FieldInfo pausedField = controllerType.GetField("Paused", BindingFlags.Public | BindingFlags.Instance);
            if (pausedField != null) pausedField.SetValue(controller, false);

            var roundSw = Stopwatch.StartNew();
            object taskObj;
            try
            {
                taskObj = createNewWorld.Invoke(null, new object[] { null, controller, null });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Poc] World " + i + ": generation call failed: " + ex.Message + " - retrying");
                retries++;
                if (retries > 5) throw;
                Thread.Sleep(2000);
                continue;
            }
            var task = (System.Threading.Tasks.Task)taskObj;

            int pollCount = 0;
            while (!task.IsCompleted)
            {
                Thread.Sleep(500);
                pollCount++;
                if (pollCount > 240) throw new TimeoutException("World generation exceeded 120s");
            }
            if (task.IsFaulted)
            {
                Console.WriteLine("[Poc] World " + i + ": generation faulted: " + task.Exception.InnerException?.Message + " - retrying");
                retries++;
                if (retries > 5) throw task.Exception;
                Thread.Sleep(2000);
                continue;
            }

            var detector = new PyramidDetector(game);
            int count2 = detector.CountSandstoneBricks();
            string worldPath = (string)pathProp.GetValue(metadata, null);
            roundSw.Stop();

            Console.WriteLine("[Poc] World " + i + ": \"" + worldName + "\" - Sandstone bricks: " + count2 +
                " - " + (count2 >= 1 ? "DETECTOR SAYS: PYRAMID" : "DETECTOR SAYS: NO PYRAMID") +
                " - " + roundSw.Elapsed.TotalSeconds + "s");
            Console.WriteLine("         " + worldPath);

            i++;
        }

        Console.WriteLine();
        Console.WriteLine("[Poc] All " + count + " worlds kept on disk. Please open each in-game to verify manually.");
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
