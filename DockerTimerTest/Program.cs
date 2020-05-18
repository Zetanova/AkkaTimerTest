using System;
using System.Diagnostics;
using System.Threading;

namespace DockerTimerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"IsHighResolution: {Stopwatch.IsHighResolution}");
            Console.WriteLine($"Thread Frequency: {Stopwatch.Frequency}");

            var test = new TestHashWheelLoop();

            test.Run(20000); // 10ms * 1000 = 200sec

            Console.WriteLine("end");
        }

    }

    sealed class TestHashWheelLoop
    {
        readonly Stopwatch sw = Stopwatch.StartNew();

        private const int TicksInMillisecond = 10000;

        TimeSpan Elapsed => TimeSpan.FromTicks(sw.ElapsedMilliseconds * TicksInMillisecond);

        TimeSpan ElapsedHighRes => sw.Elapsed;

        long _startTime;
        long _tick;
        long _tickDuration;

        public TestHashWheelLoop()
        {
        }

        public void Run(long endTick)
        {
            //default tickduration 10ms
            _tickDuration = TimeSpan.FromMilliseconds(10).Ticks;

            // Initialize the clock
            _startTime = ElapsedHighRes.Ticks;
            if (_startTime == 0)
            {
                // 0 means it's an uninitialized value, so bump to 1 to indicate it's started
                _startTime = 1;
            }

            do
            {
                var deadline = WaitForNextTick();
                if (deadline > 0)
                {
                    _tick++; // it will take 2^64 * 10ms for this to overflow
                }
                else
                {
                    //todo answer when is deadline < 0 
                    Console.WriteLine($"deadline: {deadline}");
                }
            } while (_tick < endTick);
        }

        long WaitForNextTick()
        {
            var deadline = _tickDuration * (_tick + 1);
            unchecked // just to avoid trouble with long-running applications
            {
                for (int i = 0; ; i++)
                {
                    long currentTime = ElapsedHighRes.Ticks - _startTime;
                    var sleepMs = ((deadline - currentTime + TimeSpan.TicksPerMillisecond - 1) / TimeSpan.TicksPerMillisecond);

                    if (sleepMs <= 0) // no need to sleep
                    {
                        if(i == 0)
                            Console.WriteLine($"sleep skip: tick[{_tick}], sleep[{sleepMs}], currentTime[{currentTime}], deadline[{deadline}]");

                        if (currentTime == long.MinValue) // wrap-around
                            return -long.MaxValue;
                        return currentTime;
                    }

                    if(i > 0)
                    {
                        Console.WriteLine($"double sleep: {i}");
                    }

#if UNSAFE_THREADING
                    try
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(sleepMs));
                    }
                    catch (ThreadInterruptedException)
                    {
                        if (_workerState == WORKER_STATE_SHUTDOWN)
                            return long.MinValue;
                    }
#else
                    //Console.WriteLine($"sleep: tick[{_tick}], sleep[{sleepMs}]");
                    Thread.Sleep(TimeSpan.FromMilliseconds(sleepMs));
#endif
                }
            }
        }
    }
}
