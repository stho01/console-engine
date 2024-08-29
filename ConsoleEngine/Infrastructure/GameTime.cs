using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ConsoleEngine.Infrastructure;

public static class GameTime
{
    //**********************************************************
    //** fields:
    //**********************************************************

    private static long? _previous;
    private static TimeSpan _delta;
    private static Stopwatch _stopwatch;
          
    //**********************************************************
    //** props:
    //**********************************************************

    /// <summary>Elapsed time since last frame.</summary>
    public static TimeSpan Delta => _delta;
    public static int Fps { get; private set; }
        
    private static readonly List<Interval> _intervals = new();
        
    //**********************************************************
    //** methods:
    //**********************************************************

    internal static void Update()
    {
        _stopwatch ??= Stopwatch.StartNew();
        _previous ??= _stopwatch.Elapsed.Ticks;
        var now = _stopwatch.Elapsed.Ticks;
        var dt = (now - _previous.Value);
        _delta = TimeSpan.FromTicks(dt);
        _previous = now;
            
        Fps = (int)(1f / _delta.TotalSeconds);
            
        _intervals.ForEach(x =>
        {
            if (!x.Pause) x.UpdateInterval();
        });
    }

    public static Interval SetInterval(int ms, Action callback)
    {
        var interval = new Interval(ms, callback); 
        _intervals.Add(interval);
        return interval;
    }

    public static void ClearInterval(Interval interval)
    {
        _intervals.Remove(interval);
    }

    public class Interval
    {
        private int _ms;
        private readonly Action _callback;
        private double _intervalElapsed = 0;

        internal Interval(int ms, Action callback)
        {
            _ms = ms;
            _callback = callback;
        }

        public int Ms
        {
            get => _ms;
            set => _ms = Math.Max(0, value);
        }

        public bool Pause { get; set; }

        public void UpdateInterval()
        {
            _intervalElapsed += GameTime.Delta.TotalMilliseconds;
            if (_intervalElapsed < _ms) 
                return;
                
            _callback.Invoke();
            _intervalElapsed = 0f;
        }
    }
}