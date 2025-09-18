using FishNet;
using Jabbas.ProductionSystem;
using System.Collections.Generic;
using UnityEngine;

public class UI_ProductionOptionSlotCreator : MonoBehaviour
{
    [SerializeField] private ProductionOptionStateManager _stateDatabase;
    [SerializeField] private ProductionHandler _handler;
    [SerializeField] private GameObject _optionSlot;
    [SerializeField] private Transform _container;

    private readonly Dictionary<int /*OptionId*/, OptionUIElement /*Slot*/> _activeSlots = new();

    private class OptionUIElement
    {
        public GameObject GameObject;
        public IOptionSlot Slot;
    }

    private void Awake()
    {
        if (!_optionSlot || !_container || !_handler)
        {
            ProductionDebugLogger.LogMessage(this, "Disabling option displayer: missing references.", true);
            enabled = false;
            return;
        }

        if (_stateDatabase && InstanceFinder.ClientManager.Started) RebuildUI();
    }

    public void ReassignUI(ProductionOptionStateManager stateDatabase)
    {
        if (_stateDatabase) _stateDatabase.OnClientOptionToggled -= OptionToggled;

        if (!stateDatabase)
        {
            ProductionDebugLogger.LogMessage(this, "Attempted to assign null database to option displayer.", true);
            return;
        }

        _stateDatabase = stateDatabase;
        _stateDatabase.OnClientOptionToggled += OptionToggled;
        RebuildUI();
    }

    public void RebuildUI()
    {
        ClearSlots();

        foreach (var state in _stateDatabase.States) //Creates all the intial slots
        {
            if (!_stateDatabase.TryGetOptionById(state.Key, out ProductionOption option)) continue;

            CreateSlot(option);
        }
    }

    private void OnEnable()
    {
        if (_stateDatabase) _stateDatabase.OnClientOptionToggled += OptionToggled;
    }

    private void OnDisable()
    {
        if (_stateDatabase) _stateDatabase.OnClientOptionToggled -= OptionToggled;
    }


    private void OptionToggled(int optionId, bool isEnabled)
    {
        if (!_activeSlots.TryGetValue(optionId, out var ui))
        {
            ProductionDebugLogger.LogMessage(this, $"Attempted to toggle option on UI for missing ID: <color=cyan>{optionId}</color>", true);
            return;
        }

        ui.Slot.SetEnabled(isEnabled);
    }

    private void CreateSlot(ProductionOption option)
    {
        if (_activeSlots.ContainsKey(option.Id))
        {
            ProductionDebugLogger.LogMessage(this, $"Duplicate options detected for ID: <color=cyan>{option.Id}</color>", true);
            return;
        }

        GameObject slotObject = Instantiate(_optionSlot, _container);

        if (!slotObject.TryGetComponent<IOptionSlot>(out var slotOptionLogic))
        {
            ProductionDebugLogger.LogMessage(this, "Assigned OptionSlot does not have an IOptionSlot script.", true);
            Destroy(slotObject);
            return;
        }

        slotOptionLogic.InitializeSlot(option, _handler);
        slotOptionLogic.SetEnabled(_stateDatabase.IsOptionEnabled(option.Id));

        _activeSlots.Add(option.Id, new() { GameObject = slotObject, Slot = slotOptionLogic });
    }

    //Leaving it here in case it would be required later on, currently outside of scope
    //private void RemoveSlot(ProductionOption option) 
    //{
    //    if (!_activeSlots.ContainsKey(option.Id)) return;

    //    GameObject slot = _activeSlots.FirstOrDefault(pair => pair.Key == option.Id).Value;
    //    _activeSlots.Remove(option.Id);

    //    if (slot == null) return;

    //    Destroy(slot);
    //}

    private void ClearSlots()
    {
        foreach (var slot in _activeSlots)
            if (slot.Value?.GameObject) Destroy(slot.Value.GameObject);

        _activeSlots.Clear();
    }
}
