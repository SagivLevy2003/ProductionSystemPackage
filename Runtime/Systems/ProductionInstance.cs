using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ProductionInstance
{
    //Static Data
    [field: SerializeField] public int Id { get; private set; }
    [field: SerializeField] public int RequestOwnerId { get; private set; }
    [field: SerializeField] public int SourceId { get; private set; }
    [field: SerializeField] public int OptionId { get; private set; }

    private readonly List<ICompletionRuntime> _completionLogic = new();
    private readonly IProgressionRuntime _progressionLogic;



    //Dynamic Data
    [SerializeField] private float _progress = 0;


    #region Public API
    public float Progress // 0..1
    {
        get => _progress;
        private set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, _progress))
                return;

            float prev = _progress;
            _progress = clamped;
            float curr = _progress;

            // Progress event
            OnProgress?.Invoke(this, prev, curr);

            // Throttled event (and ensure a final ping on completion)
            if ((curr - _lastProgressThrottled) >= _throttleMargin || curr >= 1f)
            {
                OnProgressThrottled?.Invoke(this, _lastProgressThrottled, curr);
                _lastProgressThrottled = curr;
            }

            // Completion event (once)
            if (curr >= 1f)
            {
                OnCompleted?.Invoke(this);
            }
        }
    }

    public bool IsCompleted => Progress >= 1;

    public bool IsPaused { get; private set; } = false;
    #endregion

    //Throttle logic
    private float _lastProgressThrottled = 0;
    private readonly float _throttleMargin = 0.05f;

    #region Events
    /// <summary>
    /// Called when a production is completed. <br/>
    /// Gets called before the handler's <see cref="ProductionHandler.ServerProductionCompleted"/> event. 
    /// </summary>
    public event Action<ProductionInstance> OnCompleted;

    /// <summary>
    /// Instance, last progress, current progress
    /// </summary>
    public event Action<ProductionInstance, float, float> OnProgress;

    /// <summary>
    /// Instance, last progress, current progress
    /// </summary>
    public event Action<ProductionInstance, float, float> OnProgressThrottled;
    #endregion

    private bool _completedInvoked = false;

    public ProductionInstance(ProductionOption option, ProductionRequestData data, int instanceId)
    {
        if (option == null)
        {
            ProductionDebugLogger.LogMessage(this, "Attempted to construct an instance with a null production option.");
            throw new ArgumentNullException(nameof(option));
        }

        RequestOwnerId = data.RequestOwnerId;
        SourceId = data.SourceId;
        Id = instanceId;
        OptionId = option.Id;

        //Assign completion logic
        foreach (var factory in option.CompletionFactories)
        {
            if (factory == null)
            {
                ProductionDebugLogger.LogMessage(this, $"Null completion factory assigned option: " +
                                                       $"<color=cyan>{option.DisplayName}</color>", true);
                continue;
            }

            ICompletionRuntime runtime = factory.CreateRuntime();

            if (runtime == null)
            {
                ProductionDebugLogger.LogMessage(this, $"Null completion logic assigned in instance with option: " +
                                                       $"<color=cyan>{option.DisplayName}</color>", true);
                continue;
            }

            _completionLogic.Add(runtime);
        }

        OnCompleted += (_) => OnProductionCompleted();
        //


        //Assign progression logic
        _progressionLogic = option.ProgressionFactory.CreateRuntime();

        if (_progressionLogic == null) ProductionDebugLogger.LogMessage(this, $"Null progression logic assigned in instance with option: " +
            $"<color=cyan>{option.DisplayName}</color>", true);
        //
    }


    [Server]
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        if (_progressionLogic is IPausableProgression p) p.OnPaused();
    }


    [Server]
    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        if (_progressionLogic is IPausableProgression p) p.OnResumed();
    }


    /// <summary>
    /// Ticks the instance to progress it.
    /// </summary>
    /// <returns>Whether the production has completed this tick</returns>
    [Server]
    public bool Tick()
    {
        if (IsPaused || IsCompleted || _progressionLogic == null) return false;

        Progress = _progressionLogic.GetNextAbsoluteProgress(this, RequestOwnerId, SourceId);

        return IsCompleted;
    }

    [Server]
    public void AddProgress(float delta)
    {
        if (delta <= 0f || IsPaused || IsCompleted) return;
        Progress = _progress + delta;
    }

    private void OnProductionCompleted()
    {
        if (_completedInvoked)
        {
            ProductionDebugLogger.LogMessage(this, $"Attempted to fire completion events twice for production instance.", true);
            return;
        }

        _completedInvoked = true;


        foreach (var step in _completionLogic)
        {
            if (step == null) continue;
            try
            {
                step.OnCompletion(this);
            }
            catch (Exception ex)
            {
                ProductionDebugLogger.LogMessage(this, $"Completion step threw: {ex}", true);
                // continue to next step
            }
        }
    }
}
