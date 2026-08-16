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

        var progType = game.GetType("Terraria.Program");
        var saveField = progType?.GetField("SavePath");
        if (saveField != null)
        {
            string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "My Games", "Terraria");
            saveField.SetValue(null, savePath);
            Console.WriteLine("[Poc] SavePath set to: " + savePath);
        }

        var godMode = new GodMode(game);

        var poller = new Thread(() =>
        {
            Thread.Sleep(5000);
            Console.WriteLine("[Poc] Enabling God Mode reflection loop (press ctrl+C to stop)...");
            godMode.Enabled = true;

            Type mainType = game.GetType("Terraria.Main");
            PropertyInfo localPlayerProp = mainType.GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.Static);
            Type playerType = game.GetType("Terraria.Player");
            FieldInfo statLifeField = playerType.GetField("statLife", BindingFlags.Public | BindingFlags.Instance);

            int tick = 0;
            while (true)
            {
                godMode.Poll();

                if (tick % 5 == 0)
                {
                    try
                    {
                        object lp = localPlayerProp.GetValue(null, null);
                        int life = lp != null ? (int)statLifeField.GetValue(lp) : -1;
                        Console.WriteLine("[Poc] tick=" + tick + " LocalPlayer.statLife=" + life);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[Poc] read error: " + ex.Message);
                    }
                }

                tick++;
                Thread.Sleep(200);
            }
        });
        poller.IsBackground = true;
        poller.Start();

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
