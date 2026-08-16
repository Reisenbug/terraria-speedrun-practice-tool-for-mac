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

        var poller = new Thread(() =>
        {
            Console.WriteLine("[Poc] Waiting for FNA/game assemblies to finish loading...");
            Thread.Sleep(5000);
            var spawner = new Spawner(game);

            Type mainType = game.GetType("Terraria.Main");
            Check("Terraria.Main", mainType);
            PropertyInfo localPlayerProp = mainType.GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.Static);
            Check("Main.LocalPlayer", localPlayerProp);
            Type entityType = game.GetType("Terraria.Entity");
            Check("Terraria.Entity", entityType);
            FieldInfo posField = entityType.GetField("position", BindingFlags.Public | BindingFlags.Instance);
            Check("Entity.position", posField);
            Type vec2Type = FindVector2Type();
            Check("Microsoft.Xna.Framework.Vector2", vec2Type);
            FieldInfo xField = vec2Type.GetField("X");
            Check("Vector2.X", xField);
            FieldInfo yField = vec2Type.GetField("Y");
            Check("Vector2.Y", yField);

            FieldInfo npcArrayField = mainType.GetField("npc", BindingFlags.Public | BindingFlags.Static);
            Check("Main.npc", npcArrayField);
            Type npcType = game.GetType("Terraria.NPC");
            Check("Terraria.NPC", npcType);
            FieldInfo npcActive = npcType.GetField("active", BindingFlags.Public | BindingFlags.Instance);
            Check("NPC.active", npcActive);
            FieldInfo npcLife = npcType.GetField("life", BindingFlags.Public | BindingFlags.Instance);
            Check("NPC.life", npcLife);
            FieldInfo npcTypeField = npcType.GetField("type", BindingFlags.Public | BindingFlags.Instance);
            Check("NPC.type", npcTypeField);

            FieldInfo itemArrayField = mainType.GetField("item", BindingFlags.Public | BindingFlags.Static);
            Check("Main.item", itemArrayField);
            Type worldItemType = game.GetType("Terraria.WorldItem");
            Check("Terraria.WorldItem", worldItemType);
            PropertyInfo wiActive = worldItemType.GetProperty("active", BindingFlags.Public | BindingFlags.Instance);
            Check("WorldItem.active", wiActive);
            PropertyInfo wiType = worldItemType.GetProperty("type", BindingFlags.Public | BindingFlags.Instance);
            Check("WorldItem.type", wiType);

            bool spawned = false;
            while (true)
            {
                Thread.Sleep(3000);
                try
                {
                    object lp = localPlayerProp.GetValue(null, null);
                    object pos = posField.GetValue(lp);
                    float px = (float)xField.GetValue(pos);
                    float py = (float)yField.GetValue(pos);
                    Console.WriteLine("[Poc] Player position: (" + px + ", " + py + ")");

                    bool inWorld = px != 0 || py != 0;
                    if (!spawned && inWorld)
                    {
                        int itemIdx = spawner.SpawnItem(24, 1);
                        int npcIdx = spawner.SpawnNpc(1);
                        Console.WriteLine("[Poc] SpawnItem index=" + itemIdx + " SpawnNpc index=" + npcIdx);
                        spawned = true;

                        object itemArr = itemArrayField.GetValue(null);
                        object worldItem = ((Array)itemArr).GetValue(itemIdx);
                        Console.WriteLine("[Poc] item[" + itemIdx + "].active=" + wiActive.GetValue(worldItem, null) + " .type=" + wiType.GetValue(worldItem, null));

                        object npcArr = npcArrayField.GetValue(null);
                        object npcObj = ((Array)npcArr).GetValue(npcIdx);
                        Console.WriteLine("[Poc] npc[" + npcIdx + "].active=" + npcActive.GetValue(npcObj) + " .life=" + npcLife.GetValue(npcObj) + " .type=" + npcTypeField.GetValue(npcObj));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Poc] attempt failed: " + ex.Message);
                }
            }
        });
        poller.IsBackground = true;
        poller.Start();

        Console.WriteLine("[Poc] Invoking game entry point...");
        game.EntryPoint.Invoke(null, new object[] { new string[0] });
    }

    static Type FindVector2Type()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("Microsoft.Xna.Framework.Vector2");
            if (t != null) return t;
        }
        return null;
    }

    static void Check(string label, object obj)
    {
        Console.WriteLine("[Poc] " + label + " -> " + (obj == null ? "NULL !!!" : "ok"));
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
