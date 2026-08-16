using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

public class Split
{
    public string Name;
    public TimeSpan? PbTime;
    public TimeSpan? CurrentTime;
}

public class SplitTimer
{
    public readonly List<Split> Splits = new List<Split>();
    readonly Stopwatch _stopwatch = new Stopwatch();
    int _currentSplit = -1;
    readonly string _pbPath;

    public bool Running => _stopwatch.IsRunning;
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public int CurrentSplitIndex => _currentSplit;

    public SplitTimer(IEnumerable<string> splitNames, string pbPath)
    {
        _pbPath = pbPath;
        foreach (var name in splitNames)
            Splits.Add(new Split { Name = name });
        LoadPb();
    }

    public void Start()
    {
        if (Running) return;
        foreach (var s in Splits) s.CurrentTime = null;
        _currentSplit = -1;
        _stopwatch.Restart();
    }

    public void Split_()
    {
        if (!Running) return;
        _currentSplit++;
        if (_currentSplit >= Splits.Count)
        {
            Stop();
            return;
        }
        Splits[_currentSplit].CurrentTime = _stopwatch.Elapsed;
        if (_currentSplit == Splits.Count - 1)
            Stop();
    }

    public void Undo()
    {
        if (_currentSplit < 0) return;
        Splits[_currentSplit].CurrentTime = null;
        _currentSplit--;
    }

    public void Skip()
    {
        if (!Running) return;
        if (_currentSplit + 1 >= Splits.Count) return;
        _currentSplit++;
        Splits[_currentSplit].CurrentTime = null;
    }

    public void Reset(bool save)
    {
        _stopwatch.Stop();
        if (save) SavePbIfBetter();
    }

    void Stop()
    {
        _stopwatch.Stop();
        SavePbIfBetter();
    }

    void SavePbIfBetter()
    {
        bool fullRun = Splits.Count > 0 && Splits[Splits.Count - 1].CurrentTime.HasValue;
        if (!fullRun) return;

        var finalTime = Splits[Splits.Count - 1].CurrentTime.Value;
        var pbFinal = Splits[Splits.Count - 1].PbTime;
        if (pbFinal.HasValue && finalTime >= pbFinal.Value) return;

        foreach (var s in Splits)
            s.PbTime = s.CurrentTime;
        SavePb();
    }

    public string DeltaString(int index)
    {
        var s = Splits[index];
        if (!s.CurrentTime.HasValue || !s.PbTime.HasValue) return "";
        var delta = s.CurrentTime.Value - s.PbTime.Value;
        string sign = delta.TotalMilliseconds <= 0 ? "-" : "+";
        return sign + FormatSpan(Abs(delta));
    }

    static TimeSpan Abs(TimeSpan t) => t < TimeSpan.Zero ? t.Negate() : t;

    public static string FormatSpan(TimeSpan t)
    {
        return string.Format("{0:00}:{1:00}.{2:000}", (int)t.TotalMinutes, t.Seconds, t.Milliseconds);
    }

    void LoadPb()
    {
        if (!File.Exists(_pbPath)) return;
        try
        {
            var lines = File.ReadAllLines(_pbPath);
            for (int i = 0; i < lines.Length && i < Splits.Count; i++)
            {
                if (long.TryParse(lines[i], out long ms))
                    Splits[i].PbTime = TimeSpan.FromMilliseconds(ms);
            }
        }
        catch { }
    }

    void SavePb()
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var s in Splits)
                sb.AppendLine(s.PbTime.HasValue ? ((long)s.PbTime.Value.TotalMilliseconds).ToString() : "");
            File.WriteAllText(_pbPath, sb.ToString());
        }
        catch { }
    }
}
