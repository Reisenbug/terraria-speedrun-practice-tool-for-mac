using System;
using System.IO;
using System.Threading;

public static class TimerTest
{
    public static void Main(string[] args)
    {
        string pbPath = Path.GetTempFileName();
        File.Delete(pbPath);

        var t = new SplitTimer(new[] { "A", "B", "C" }, pbPath);
        Assert(!t.Running, "not running initially");

        t.Start();
        Assert(t.Running, "running after start");
        Thread.Sleep(50);
        t.Split_();
        Assert(t.Splits[0].CurrentTime.HasValue, "split0 recorded");
        Thread.Sleep(50);
        t.Split_();
        Thread.Sleep(50);
        t.Split_(); // last split -> auto stop
        Assert(!t.Running, "stopped after last split");
        Assert(t.Splits[2].PbTime.HasValue, "PB saved after first full run");
        var firstPb = t.Splits[2].PbTime.Value;

        // second run, slower on purpose -> PB should NOT update
        t.Start();
        Thread.Sleep(200);
        t.Split_();
        Thread.Sleep(10);
        t.Split_();
        Thread.Sleep(10);
        t.Split_();
        Assert(t.Splits[2].PbTime.Value == firstPb, "slower run does not overwrite PB");

        // undo/skip sanity
        var t2 = new SplitTimer(new[] { "X", "Y" }, pbPath + "2");
        t2.Start();
        t2.Split_();
        Assert(t2.CurrentSplitIndex == 0, "index after 1 split");
        t2.Undo();
        Assert(t2.CurrentSplitIndex == -1, "undo resets index");
        Assert(!t2.Splits[0].CurrentTime.HasValue, "undo clears split time");
        t2.Skip();
        Assert(t2.CurrentSplitIndex == 0, "skip advances index");
        Assert(!t2.Splits[0].CurrentTime.HasValue, "skip leaves no time recorded");

        File.Delete(pbPath);
        Console.WriteLine("ALL TESTS PASSED");
    }

    static void Assert(bool cond, string msg)
    {
        if (!cond)
        {
            Console.WriteLine("FAIL: " + msg);
            Environment.Exit(1);
        }
        Console.WriteLine("ok: " + msg);
    }
}
