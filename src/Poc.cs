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

        string pbPath = Path.Combine(AssemblyFolder, "pb_splits_test.txt");
        var timer = new SplitTimer(new[] { "Eye of Cthulhu", "Skeletron", "Hardmode" }, pbPath);
        var events = new GameEvents(game);
        var binding = new AutoSplitBinding(timer, events);
        binding.Arm();

        var poller = new Thread(() =>
        {
            Thread.Sleep(5000);

            Type npcType = game.GetType("Terraria.NPC");
            FieldInfo boss1 = npcType.GetField("downedBoss1", BindingFlags.Public | BindingFlags.Static);
            FieldInfo boss3 = npcType.GetField("downedBoss3", BindingFlags.Public | BindingFlags.Static);
            Type mainType = game.GetType("Terraria.Main");
            FieldInfo hardMode = mainType.GetField("hardMode", BindingFlags.Public | BindingFlags.Static);

            Console.WriteLine("[Poc] Starting timer...");
            timer.Start();

            int tick = 0;
            while (true)
            {
                events.Poll();

                if (tick == 3 && boss1 != null)
                {
                    Console.WriteLine("[Poc] Simulating Eye of Cthulhu kill...");
                    boss1.SetValue(null, true);
                }
                if (tick == 6 && boss3 != null)
                {
                    Console.WriteLine("[Poc] Simulating Skeletron kill...");
                    boss3.SetValue(null, true);
                }
                if (tick == 9 && hardMode != null)
                {
                    Console.WriteLine("[Poc] Simulating Hardmode trigger...");
                    hardMode.SetValue(null, true);
                }
                if (tick == 12)
                {
                    Console.WriteLine("[Poc] Timer running=" + timer.Running + " elapsed=" + SplitTimer.FormatSpan(timer.Elapsed));
                    for (int i = 0; i < timer.Splits.Count; i++)
                    {
                        var s = timer.Splits[i];
                        Console.WriteLine("  split[" + i + "] " + s.Name + " = " + (s.CurrentTime.HasValue ? SplitTimer.FormatSpan(s.CurrentTime.Value) : "not hit"));
                    }
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
