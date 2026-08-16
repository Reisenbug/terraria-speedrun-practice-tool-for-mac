using System;
using System.Diagnostics;

public class SimpleStopwatch
{
    readonly Stopwatch _sw = new Stopwatch();

    public bool Running => _sw.IsRunning;
    public TimeSpan Elapsed => _sw.Elapsed;

    public void Start()
    {
        if (!Running) _sw.Start();
    }

    public void Stop()
    {
        if (Running) _sw.Stop();
    }

    public void Reset()
    {
        _sw.Reset();
    }

    public void Toggle()
    {
        if (Running) Stop();
        else Start();
    }

    public string ElapsedString()
    {
        return SplitTimer.FormatSpan(Elapsed);
    }
}
