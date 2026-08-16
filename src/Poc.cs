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

        var events = new GameEvents(game);
        events.OnEventTriggered += (name) =>
        {
            Console.WriteLine("[EVENT] *** " + name + " triggered! ***");
        };

        var poller = new Thread(() =>
        {
            // give the game a moment to fully init before we start hammering reflection
            Thread.Sleep(5000);

            Type npcType = game.GetType("Terraria.NPC");
            FieldInfo testFlag = npcType.GetField("downedBoss1", BindingFlags.Public | BindingFlags.Static);

            int tick = 0;
            while (true)
            {
                events.Poll();

                // after 15s, flip a flag ourselves to prove OnEventTriggered fires end-to-end
                if (tick == 5 && testFlag != null)
                {
                    Console.WriteLine("[Poc] Forcing NPC.downedBoss1 = true to test event pipeline...");
                    testFlag.SetValue(null, true);
                }
                tick++;
                Thread.Sleep(1000);
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
