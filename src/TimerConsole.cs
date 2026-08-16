using System;
using System.IO;
using System.Threading;

public static class TimerConsole
{
    public static void Main(string[] args)
    {
        string pbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pb_splits.txt");
        var splitNames = new[] { "King Slime", "Eye of Cthulhu", "Skeletron" };
        var timer = new SplitTimer(splitNames, pbPath);

        Console.WriteLine("Commands: s=start/split, u=undo, k=skip, r=reset, q=quit");

        var render = new Thread(() =>
        {
            while (true)
            {
                RenderState(timer);
                Thread.Sleep(100);
            }
        });
        render.IsBackground = true;
        render.Start();

        while (true)
        {
            var key = Console.ReadKey(true).KeyChar;
            switch (key)
            {
                case 's':
                    if (!timer.Running) timer.Start();
                    else timer.Split_();
                    break;
                case 'u':
                    timer.Undo();
                    break;
                case 'k':
                    timer.Skip();
                    break;
                case 'r':
                    timer.Reset(save: true);
                    break;
                case 'q':
                    return;
            }
        }
    }

    static void RenderState(SplitTimer timer)
    {
        Console.Clear();
        Console.WriteLine("=== Speedrun Timer ===");
        Console.WriteLine(SplitTimer.FormatSpan(timer.Elapsed));
        Console.WriteLine();
        for (int i = 0; i < timer.Splits.Count; i++)
        {
            var s = timer.Splits[i];
            string cur = s.CurrentTime.HasValue ? SplitTimer.FormatSpan(s.CurrentTime.Value) : "--:--.---";
            string pb = s.PbTime.HasValue ? SplitTimer.FormatSpan(s.PbTime.Value) : "--:--.---";
            string delta = timer.DeltaString(i);
            string marker = i == timer.CurrentSplitIndex ? ">" : " ";
            Console.WriteLine($"{marker} {s.Name,-20} {cur}  (PB {pb})  {delta}");
        }
        Console.WriteLine();
        Console.WriteLine("s=start/split  u=undo  k=skip  r=reset  q=quit");
    }
}
