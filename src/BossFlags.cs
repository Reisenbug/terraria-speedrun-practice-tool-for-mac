using System;
using System.Collections.Generic;
using System.Reflection;

// Read/write access to world-progression boss flags, for jumping straight
// to a practice point (e.g. set mech bosses as downed to practice
// post-mech content without re-fighting earlier bosses every time).
public class BossFlags
{
    class FlagDef
    {
        public string TypeName, FieldName, DisplayName;
        public FlagDef(string t, string f, string d) { TypeName = t; FieldName = f; DisplayName = d; }
    }

    static readonly FlagDef[] Defs = new[]
    {
        new FlagDef("Terraria.NPC", "downedBoss1", "Eye of Cthulhu"),
        new FlagDef("Terraria.NPC", "downedBoss2", "Eater of Worlds / Brain of Cthulhu"),
        new FlagDef("Terraria.NPC", "downedBoss3", "Skeletron"),
        new FlagDef("Terraria.NPC", "downedQueenBee", "Queen Bee"),
        new FlagDef("Terraria.NPC", "downedMechBoss1", "The Destroyer"),
        new FlagDef("Terraria.NPC", "downedMechBoss2", "The Twins"),
        new FlagDef("Terraria.NPC", "downedMechBoss3", "Skeletron Prime"),
        new FlagDef("Terraria.NPC", "downedMechBossAny", "Any Mech Boss"),
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

    public IEnumerable<string> Names => _fields.Keys;

    public BossFlags(Assembly game)
    {
        _game = game;
        foreach (var def in Defs)
        {
            var type = _game.GetType(def.TypeName);
            var field = type != null ? type.GetField(def.FieldName, BindingFlags.Public | BindingFlags.Static) : null;
            if (field != null)
                _fields[def.DisplayName] = field;
        }
    }

    public bool Get(string name)
    {
        if (!_fields.TryGetValue(name, out var field)) return false;
        return (bool)field.GetValue(null);
    }

    public void Set(string name, bool value)
    {
        if (!_fields.TryGetValue(name, out var field)) return;
        field.SetValue(null, value);
    }

    public void SetAllBeforeMechBosses()
    {
        Set("Eye of Cthulhu", true);
        Set("Eater of Worlds / Brain of Cthulhu", true);
        Set("Skeletron", true);
        Set("Queen Bee", true);
        Set("Hardmode", true);
    }

    public void ClearAll()
    {
        foreach (var name in _fields.Keys)
            Set(name, false);
    }
}
