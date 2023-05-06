using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Timers;
using Timer = System.Timers.Timer;

namespace GamePad
{
    internal class RootCommand
    {
        Timer timer;
        Stopwatch stopwatch = new Stopwatch();

        int count = 0;
        public void Run()
        {
            timer = new(10);

            timer.Elapsed += Timer_Elapsed;

            timer.Start();
            stopwatch.Start();
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            count++;
            if (count == 100)
            {
                var time = stopwatch.ElapsedMilliseconds;
                Console.WriteLine($"Time:{time}");
                count = 0;
            }
        }
    }
}
