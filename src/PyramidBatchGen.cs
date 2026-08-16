using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

// Batch-generates Terraria worlds in a single running game process and
// keeps only the ones containing a pyramid. Reflection-only, no Harmony.
// Replaces the external-process (TerrariaServer + subprocess) approach:
// same WorldGen.CreateNewWorld entry point the game itself uses, plus an
// in-memory Main.tile[,] scan instead of re-parsing the .wld file with a
// third-party library (lihzahrd is archived and doesn't support 1.4.5).
public static class PyramidBatchGen
{
    static string AssemblyFolder;

    public static void Main(string[] args)
    {
        int target = args.Length > 0 ? int.Parse(args[0]) : 1;
        int maxAttempts = args.Length > 1 ? int.Parse(args[1]) : 50;
        string gameExe = args.Length > 2 ? args[2] : "Terraria.exe";

        AssemblyFolder = AppDomain.CurrentDomain.BaseDirectory;
        AppDomain.CurrentDomain.AssemblyResolve += ResolveHandler;

        Console.WriteLine("[BatchGen] Loading game assembly: " + gameExe);
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
                RunBatch(game, target, maxAttempts);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BatchGen] FATAL: " + ex);
            }
            Environment.Exit(0);
        });
        worker.IsBackground = true;
        worker.Start();

        game.EntryPoint.Invoke(null, new object[] { new string[0] });
    }

    static void RunBatch(Assembly game, int target, int maxAttempts)
    {
        WaitForRenderSystemReady(game);
        object playerFileData = SetupActivePlayer(game);

        var detector = new PyramidDetector(game);
        int found = 0;
        int attempted = 0;
        var totalSw = Stopwatch.StartNew();

        Console.WriteLine("[BatchGen] Target: " + target + " pyramid world(s), max attempts: " + maxAttempts);
        Console.WriteLine();

        while (found < target && attempted < maxAttempts)
        {
            attempted++;
            var roundSw = Stopwatch.StartNew();
            object worldFileData;
            try
            {
                worldFileData = GenerateOneWorld(game, attempted);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BatchGen] Attempt " + attempted + ": generation failed: " + ex.Message);
                if (ex is ThreadAbortException) Thread.ResetAbort();
                continue;
            }

            // recreate detector's cached tile array reference for the freshly generated world
            detector = new PyramidDetector(game);
            int sandstoneCount = detector.CountSandstoneBricks();
            bool hasPyramid = sandstoneCount >= 1;

            string worldPath = GetPath(worldFileData);
            roundSw.Stop();

            if (hasPyramid)
            {
                found++;
                Console.WriteLine("[BatchGen] Attempt " + attempted + ": PYRAMID FOUND (" + sandstoneCount + " bricks) - kept - " + roundSw.Elapsed.TotalSeconds + "s");
                Console.WriteLine("           " + worldPath);
            }
            else
            {
                Console.WriteLine("[BatchGen] Attempt " + attempted + ": no pyramid - deleting - " + roundSw.Elapsed.TotalSeconds + "s");
                DeleteWorldFiles(worldPath);
            }
        }

        totalSw.Stop();
        Console.WriteLine();
        Console.WriteLine("[BatchGen] ===== Summary =====");
        Console.WriteLine("[BatchGen] Found: " + found + "/" + target);
        Console.WriteLine("[BatchGen] Attempts: " + attempted);
        Console.WriteLine("[BatchGen] Total time: " + totalSw.Elapsed.TotalSeconds + "s");
        Console.WriteLine("[BatchGen] Avg per world: " + (totalSw.Elapsed.TotalSeconds / attempted) + "s");
    }

    const string ToolPlayerName = "BatchGenPlayer";

    static object SetupActivePlayer(Assembly game)
    {
        Type mainType = game.GetType("Terraria.Main");
        Type playerType = game.GetType("Terraria.Player");
        Type playerFileDataType = game.GetType("Terraria.IO.PlayerFileData");

        MethodInfo getPlayerPath = mainType.GetMethod("GetPlayerPathFromName", BindingFlags.Public | BindingFlags.Static);
        string playerPath = (string)getPlayerPath.Invoke(null, new object[] { ToolPlayerName, false });

        object playerFileData;
        if (File.Exists(playerPath))
        {
            MethodInfo loadPlayer = playerType.GetMethod("LoadPlayer", new[] { typeof(string), typeof(bool) });
            playerFileData = loadPlayer.Invoke(null, new object[] { playerPath, false });
            Console.WriteLine("[BatchGen] Reusing existing player: " + playerPath);
        }
        else
        {
            object player = Activator.CreateInstance(playerType);
            FieldInfo nameField = playerType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
            nameField.SetValue(player, ToolPlayerName);

            MethodInfo createAndSave = playerFileDataType.GetMethod("CreateAndSave", BindingFlags.Public | BindingFlags.Static);
            playerFileData = createAndSave.Invoke(null, new object[] { player });
            Console.WriteLine("[BatchGen] Created new player: " + playerPath);
        }

        FieldInfo activePlayerField = mainType.GetField("ActivePlayerFileData", BindingFlags.Public | BindingFlags.Static);
        activePlayerField.SetValue(null, playerFileData);
        return playerFileData;
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
                Console.WriteLine("[BatchGen] Game ready after " + sw.Elapsed.TotalSeconds + "s");
                Console.WriteLine();
                return;
            }
            Thread.Sleep(500);
            if (sw.Elapsed.TotalSeconds > 60) return;
        }
    }

    static object GenerateOneWorld(Assembly game, int attemptNumber)
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

        string worldName = "Poc_" + attemptNumber;

        setWorldSize.Invoke(null, new object[] { 0 }); // Small
        gameModeProp.SetValue(null, 0, null); // Normal
        worldNameField.SetValue(null, worldName);
        worldGenParamEvilField.SetValue(null, 1); // force Crimson

        int gameMode = (int)gameModeProp.GetValue(null, null);
        object metadata = createMetadata.Invoke(null, new object[] { worldName, false, gameMode });
        activeWorldFileDataField.SetValue(null, metadata);
        setSeed.Invoke(metadata, new object[] { "" });

        object controller = Activator.CreateInstance(controllerType, new object[] { null });
        FieldInfo pausedField = controllerType.GetField("Paused", BindingFlags.Public | BindingFlags.Instance);
        if (pausedField != null) pausedField.SetValue(controller, false);

        object taskObj = createNewWorld.Invoke(null, new object[] { null, controller, null });
        var task = (System.Threading.Tasks.Task)taskObj;

        int pollCount = 0;
        while (!task.IsCompleted)
        {
            Thread.Sleep(500);
            pollCount++;
            if (pollCount > 240) throw new TimeoutException("World generation exceeded 120s");
        }
        if (task.IsFaulted) throw task.Exception;

        return metadata;
    }

    static string GetPath(object fileData)
    {
        Type t = fileData.GetType();
        PropertyInfo pathProp = t.GetProperty("Path", BindingFlags.Public | BindingFlags.Instance);
        return (string)pathProp.GetValue(fileData, null);
    }

    static void DeleteWorldFiles(string worldPath)
    {
        try
        {
            if (File.Exists(worldPath)) File.Delete(worldPath);
            string twld = Path.ChangeExtension(worldPath, ".twld");
            if (File.Exists(twld)) File.Delete(twld);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[BatchGen] Failed to delete " + worldPath + ": " + ex.Message);
        }
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
