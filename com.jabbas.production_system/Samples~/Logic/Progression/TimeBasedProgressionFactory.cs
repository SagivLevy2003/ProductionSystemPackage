using System;
using UnityEngine;


[Serializable]
public class TimeBasedProgressionFactory : IProgressFactory
{
    [SerializeField, Min(0.01f)] private float duration = 5f;

    public IProgressionRuntime CreateRuntime()
    {
        return new TimeBasedProgressionRuntime(duration);
    }
}


[Serializable]
public sealed class TimeBasedProgressionRuntime : IProgressionRuntime, IPausableProgression
{
    private readonly float _duration;
    private bool _started;

    // Absolute-time model: progress = p0 + (elapsed - paused) / duration
    private float _t0;          // start time (Time.time)
    private float _p0;          // progress at start
    private float _pausedTotal; // total paused seconds so far
    private bool _isPaused;
    private float _pauseStart;  // time when current pause began

    public TimeBasedProgressionRuntime(float duration)
    {
        _duration = Mathf.Max(0.001f, duration);
    }

    public float GetNextAbsoluteProgress(ProductionInstance inst, int ownerId, int sourceId)
    {
        float now = Time.time;

        if (!_started)
        {
            _started = true;
            _t0 = now;
            _p0 = inst.Progress; // in case you start mid-way
        }

        // If currently paused, don't advance elapsed; account for it on resume
        float paused = _pausedTotal + (_isPaused ? (now - _pauseStart) : 0f);
        float elapsed = Mathf.Max(0f, now - _t0 - paused);

        float abs = _p0 + (elapsed / _duration);
        return Mathf.Clamp01(abs);
    }

    public void OnPaused()
    {
        if (_isPaused) return;
        _isPaused = true;
        _pauseStart = Time.time;
    }

    public void OnResumed()
    {
        if (!_isPaused) return;
        _isPaused = false;
        _pausedTotal += Time.time - _pauseStart; // shift timeline so no jump
    }
}
