using System;
using System.Reflection;

// Spawns items/NPCs at the local player's position via the game's own
// Item.NewItem / NPC.NewNPC static factory methods, tagged with the
// game's own EntitySource_DebugCommand source type.
public class Spawner
{
    readonly Assembly _game;
    Type _vector2Type;
    Type _entitySourceDebugType;
    MethodInfo _newItem;
    MethodInfo _newNpc;
    PropertyInfo _localPlayerProp;
    FieldInfo _positionField;

    public Spawner(Assembly game)
    {
        _game = game;
        Init();
    }

    void Init()
    {
        // Vector2 lives in FNA.dll, not Terraria.exe itself, and is only
        // loaded into the AppDomain once the game's own startup sequence
        // has pulled it in — so this must run after that point, not
        // immediately when the host process starts.
        _vector2Type = FindTypeInAnyLoadedAssembly("Microsoft.Xna.Framework.Vector2");
        _entitySourceDebugType = _game.GetType("Terraria.DataStructures.EntitySource_DebugCommand");
        Type entitySourceType = _game.GetType("Terraria.DataStructures.IEntitySource");

        Type itemType = _game.GetType("Terraria.Item");
        Type npcType = _game.GetType("Terraria.NPC");

        // int NewItem(IEntitySource, Vector2 pos, Vector2 randomBox, int Type, int Stack, bool noBroadcast, int prefixGiven, bool noGrabDelay)
        _newItem = itemType.GetMethod("NewItem", new[] { entitySourceType, _vector2Type, _vector2Type, typeof(int), typeof(int), typeof(bool), typeof(int), typeof(bool) });

        // int NewNPC(IEntitySource, int X, int Y, int Type, int Start, float ai0..ai3, int Target)
        _newNpc = npcType.GetMethod("NewNPC", new[] { entitySourceType, typeof(int), typeof(int), typeof(int), typeof(int), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int) });

        Type mainType = _game.GetType("Terraria.Main");
        _localPlayerProp = mainType.GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.Static);

        Type entityType = _game.GetType("Terraria.Entity");
        _positionField = entityType.GetField("position", BindingFlags.Public | BindingFlags.Instance);
    }

    static Type FindTypeInAnyLoadedAssembly(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    object PlayerPosition()
    {
        object localPlayer = _localPlayerProp.GetValue(null, null);
        return _positionField.GetValue(localPlayer);
    }

    void PositionXY(object vec2, out float x, out float y)
    {
        var xField = _vector2Type.GetField("X");
        var yField = _vector2Type.GetField("Y");
        x = (float)xField.GetValue(vec2);
        y = (float)yField.GetValue(vec2);
    }

    object MakeVector2(float x, float y)
    {
        return Activator.CreateInstance(_vector2Type, x, y);
    }

    object MakeDebugSource()
    {
        return Activator.CreateInstance(_entitySourceDebugType);
    }

    public int SpawnItem(int itemType, int stack = 1)
    {
        object pos = PlayerPosition();
        object zero = MakeVector2(0, 0);
        object source = MakeDebugSource();
        object result = _newItem.Invoke(null, new object[] { source, pos, zero, itemType, stack, false, 0, false });
        return (int)result;
    }

    public int SpawnNpc(int npcType)
    {
        object pos = PlayerPosition();
        float x, y;
        PositionXY(pos, out x, out y);
        object source = MakeDebugSource();
        object result = _newNpc.Invoke(null, new object[] { source, (int)x, (int)y, npcType, 0, 0f, 0f, 0f, 0f, 255 });
        return (int)result;
    }
}
