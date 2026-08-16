using System;
using System.Reflection;

// Forces the local player's life to max every poll tick. Independent of
// Journey Mode's creative-power system (which is permission/network gated
// and may be unavailable outside Journey difficulty) — this works in any
// game mode because it just overwrites the same fields the game itself
// reads every frame.
public class GodMode
{
    readonly Assembly _game;
    PropertyInfo _localPlayerProp;
    FieldInfo _statLife;
    FieldInfo _statLifeMax;

    public bool Enabled;

    public GodMode(Assembly game)
    {
        _game = game;
    }

    public void Poll()
    {
        if (!Enabled) return;

        if (_localPlayerProp == null)
        {
            var mainType = _game.GetType("Terraria.Main");
            _localPlayerProp = mainType?.GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.Static);
            if (_localPlayerProp == null) return;

            var playerType = _game.GetType("Terraria.Player");
            _statLife = playerType.GetField("statLife", BindingFlags.Public | BindingFlags.Instance);
            _statLifeMax = playerType.GetField("statLifeMax", BindingFlags.Public | BindingFlags.Instance);
        }

        object localPlayer = _localPlayerProp.GetValue(null, null);
        if (localPlayer == null) return;

        int max = (int)_statLifeMax.GetValue(localPlayer);
        _statLife.SetValue(localPlayer, max);
    }
}
