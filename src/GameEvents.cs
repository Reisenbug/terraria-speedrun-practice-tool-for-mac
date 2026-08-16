using System;
using System.Collections.Generic;
using System.Reflection;

public class GameEvents
{
    // Field names verified against the game's own Terraria.NPC / Terraria.Main / Terraria.WorldGen source.
    // Independently written against those field names, not derived from any third-party mod.
    class FlagDef
    {
        public string TypeName, FieldName, EventName;
        public FlagDef(string t, string f, string e) { TypeName = t; FieldName = f; EventName = e; }
    }

    static readonly FlagDef[] BoolFlags = new[]
    {
        new FlagDef("Terraria.NPC", "downedBoss1", "Eye of Cthulhu"),
        new FlagDef("Terraria.NPC", "downedBoss2", "Eater of Worlds / Brain of Cthulhu"),
        new FlagDef("Terraria.NPC", "downedBoss3", "Skeletron"),
        new FlagDef("Terraria.NPC", "downedQueenBee", "Queen Bee"),
        new FlagDef("Terraria.NPC", "downedMechBoss1", "The Destroyer"),
        new FlagDef("Terraria.NPC", "downedMechBoss2", "The Twins"),
        new FlagDef("Terraria.NPC", "downedMechBoss3", "Skeletron Prime"),
        new FlagDef("Terraria.NPC", "downedPlantBoss", "Plantera"),
        new FlagDef("Terraria.NPC", "downedGolemBoss", "Golem"),
        new FlagDef("Terraria.NPC", "downedFishron", "Duke Fishron"),
        new FlagDef("Terraria.NPC", "downedAncientCultist", "Lunatic Cultist"),
        new FlagDef("Terraria.NPC", "downedMoonlord", "Moon Lord"),
        new FlagDef("Terraria.NPC", "downedEmpressOfLight", "Empress of Light"),
        new FlagDef("Terraria.NPC", "downedQueenSlime", "Queen Slime"),
        new FlagDef("Terraria.NPC", "downedDeerclops", "Deerclops"),
        new FlagDef("Terraria.Main", "hardMode", "Hardmode"),
    };

    readonly Assembly _game;
    readonly Dictionary<string, FieldInfo> _fields = new Dictionary<string, FieldInfo>();
    readonly Dictionary<string, bool> _lastValue = new Dictionary<string, bool>();

    public event Action<string> OnEventTriggered;

    public GameEvents(Assembly game)
    {
        _game = game;
        foreach (var flag in BoolFlags)
        {
            var type = _game.GetType(flag.TypeName);
            var field = type != null ? type.GetField(flag.FieldName, BindingFlags.Public | BindingFlags.Static) : null;
            if (field != null)
            {
                _fields[flag.EventName] = field;
                _lastValue[flag.EventName] = false;
            }
        }
    }

    public void Poll()
    {
        foreach (var kv in _fields)
        {
            string eventName = kv.Key;
            bool current = (bool)kv.Value.GetValue(null);
            if (current && !_lastValue[eventName])
                OnEventTriggered?.Invoke(eventName);
            _lastValue[eventName] = current;
        }
    }
}
