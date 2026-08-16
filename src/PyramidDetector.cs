using System;
using System.Collections.Generic;
using System.Reflection;

// Scans the in-memory Main.tile[,] array for Sandstone Brick tiles (TileID 151)
// right after WorldGen.CreateNewWorld completes, avoiding a second .wld
// parse pass. TileID 151 confirmed against this project's own 1.4.5.4
// source (TileID.cs) — not derived from any third-party tool.
public class PyramidDetector
{
    const int SandstoneBrickTileId = 151;

    readonly Assembly _game;
    Array _tileArray;
    Type _tileType;
    FieldInfo _typeField;
    MethodInfo _activeMethod;
    int _sizeX, _sizeY;

    public PyramidDetector(Assembly game)
    {
        _game = game;
        Init();
    }

    void Init()
    {
        Type mainType = _game.GetType("Terraria.Main");
        FieldInfo tileField = mainType.GetField("tile", BindingFlags.Public | BindingFlags.Static);
        _tileArray = (Array)tileField.GetValue(null);
        _sizeX = _tileArray.GetLength(0);
        _sizeY = _tileArray.GetLength(1);

        _tileType = _game.GetType("Terraria.Tile");
        _typeField = _tileType.GetField("type", BindingFlags.Public | BindingFlags.Instance);
        _activeMethod = _tileType.GetMethod("active", Type.EmptyTypes);
    }

    public int CountSandstoneBricks()
    {
        int count = 0;
        for (int x = 0; x < _sizeX; x++)
        {
            for (int y = 0; y < _sizeY; y++)
            {
                object t = _tileArray.GetValue(x, y);
                if (t == null) continue;
                bool active = (bool)_activeMethod.Invoke(t, null);
                if (!active) continue;
                ushort type = (ushort)_typeField.GetValue(t);
                if (type == SandstoneBrickTileId)
                    count++;
            }
        }
        return count;
    }

    public Dictionary<ushort, long> TypeHistogram(int sampleEveryN = 1)
    {
        var hist = new Dictionary<ushort, long>();
        int activeCount = 0;
        int inactiveCount = 0;
        for (int x = 0; x < _sizeX; x += sampleEveryN)
        {
            for (int y = 0; y < _sizeY; y += sampleEveryN)
            {
                object t = _tileArray.GetValue(x, y);
                if (t == null) continue;
                bool active = (bool)_activeMethod.Invoke(t, null);
                if (!active) { inactiveCount++; continue; }
                activeCount++;
                ushort type = (ushort)_typeField.GetValue(t);
                if (!hist.ContainsKey(type)) hist[type] = 0;
                hist[type]++;
            }
        }
        hist[ushort.MaxValue] = activeCount; // sentinel: total active
        hist[ushort.MaxValue - 1] = inactiveCount; // sentinel: total inactive
        return hist;
    }

    public int SizeX => _sizeX;
    public int SizeY => _sizeY;
}
