using Jabbas.ProductionSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the production queue UI. Subscribes to InstanceHub for per-instance updates.
/// </summary>
public sealed class UI_ProductionSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Slider _progress;     // 0..1
    [SerializeField] private GameObject _pausedIcon;
    [SerializeField] private GameObject _completedIcon;
    [SerializeField] private Button _cancelButton; // optional

    // smoothing
    [SerializeField] private float _lerpSpeed = 6f; // ~0.15s easing

    private int _instanceId;
    private float _displayProgress;
    private float _targetProgress;

    private System.Action<int> _onCancel; // provided by view
    private ProductionInstanceChangeHub _hub;
    private System.Action<MirroredProductionInstance, MirroredProductionInstance> _cb;

    private bool _bound;

    /// <summary>
    /// Set initial data and subscribe to hub for Set updates.
    /// </summary>
    public void Bind(MirroredProductionInstance row,
                     string displayName,
                     ProductionInstanceChangeHub hub,
                     System.Action<int> onCancel)
    {
        _instanceId = row.InstanceId;
        _hub = hub;
        _onCancel = onCancel;

        if (_nameText) _nameText.text = displayName;

        // initial state
        _displayProgress = _targetProgress = Mathf.Clamp01(row.Progress);
        ApplyStaticFlags(row);

        // cancel button
        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(() => _onCancel?.Invoke(_instanceId));
            _cancelButton.interactable = !row.IsCompleted;
        }

        // subscribe
        _cb ??= OnSet;
        _hub?.Subscribe(_instanceId, _cb);
        _bound = true;

        // paint once
        if (_progress) _progress.value = _displayProgress;
    }

    public void Unbind(ProductionInstanceChangeHub hub)
    {
        if (!_bound) return;
        hub?.Unsubscribe(_instanceId, _cb);
        _bound = false;
    }

    private void Update()
    {
        // Smooth progress for nice UI feel
        if (_progress == null) return;
        if (Mathf.Approximately(_displayProgress, _targetProgress)) return;

        _displayProgress = Mathf.MoveTowards(_displayProgress, _targetProgress, Time.deltaTime * _lerpSpeed);
        _progress.value = _displayProgress;
    }

    private void OnSet(MirroredProductionInstance oldRow, MirroredProductionInstance newRow)
    {
        _targetProgress = Mathf.Clamp01(newRow.Progress);
        ApplyStaticFlags(newRow);

        if (_cancelButton) _cancelButton.interactable = !newRow.IsCompleted;
    }

    private void ApplyStaticFlags(in MirroredProductionInstance row)
    {
        if (_pausedIcon) _pausedIcon.SetActive(row.IsPaused && !row.IsCompleted);
        if (_completedIcon) _completedIcon.SetActive(row.IsCompleted);
    }
}
