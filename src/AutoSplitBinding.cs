using System;
using System.Collections.Generic;

// Binds GameEvents (boss/hardmode flags read via reflection) to SplitTimer.
// Each split name in the timer is matched, in order, against the next
// recognized game event. When the game event fires, the timer advances
// one split automatically.
public class AutoSplitBinding
{
    readonly SplitTimer _timer;
    readonly GameEvents _events;
    bool _armed;

    public AutoSplitBinding(SplitTimer timer, GameEvents events)
    {
        _timer = timer;
        _events = events;
        _events.OnEventTriggered += OnGameEvent;
    }

    public void Arm()
    {
        _armed = true;
    }

    public void Disarm()
    {
        _armed = false;
    }

    void OnGameEvent(string eventName)
    {
        if (!_armed) return;
        if (!_timer.Running) return;

        int nextIndex = _timer.CurrentSplitIndex + 1;
        if (nextIndex >= _timer.Splits.Count) return;

        string expectedName = _timer.Splits[nextIndex].Name;
        if (string.Equals(expectedName, eventName, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[AutoSplit] " + eventName + " -> split " + nextIndex);
            _timer.Split_();
        }
    }
}
