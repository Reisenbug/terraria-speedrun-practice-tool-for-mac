using System;
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

        Console.WriteLine("[Poc] Loading embedded game dependencies...");
        foreach (var resName in game.GetManifestResourceNames())
        {
            if (resName.Contains(".dll"))
            {
                using (var s = game.GetManifestResourceStream(resName))
                {
                    var buf = new byte[s.Length];
                    s.Read(buf, 0, buf.Length);
                    Assembly.Load(buf);
                    Console.WriteLine("[Poc]   loaded resource dep: " + resName);
                }
            }
        }

        // Redirect save path like the real injector does, so it doesn't blow up on ~/My Games
        var progType = game.GetType("Terraria.Program");
        var saveField = progType?.GetField("SavePath");
        if (saveField != null)
        {
            string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "My Games", "Terraria");
            saveField.SetValue(null, savePath);
            Console.WriteLine("[Poc] SavePath set to: " + savePath);
        }

        // Background thread: reflectively poke Main.time every 3s, no Harmony, no hooking.
        var poker = new Thread(() =>
        {
            Type mainType = null;
            FieldInfo timeField = null;
            double t = 4500.0; // ~morning
            while (true)
            {
                try
                {
                    if (mainType == null)
                    {
                        mainType = game.GetType("Terraria.Main");
                        if (mainType != null)
                            timeField = mainType.GetField("time", BindingFlags.Public | BindingFlags.Static);
                    }
                    if (timeField != null)
                    {
                        timeField.SetValue(null, t);
                        Console.WriteLine("[Poc] Forced Main.time = " + t);
                    }
                    else
                    {
                        Console.WriteLine("[Poc] Main.time field not found yet...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Poc] poke error: " + ex.Message);
                }
                Thread.Sleep(3000);
            }
        });
        poker.IsBackground = true;
        poker.Start();

        Console.WriteLine("[Poc] Invoking game entry point...");
        game.EntryPoint.Invoke(null, new object[] { new string[0] });
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
