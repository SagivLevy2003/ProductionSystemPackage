using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ProductionSystem/ProductionOptions/Base")]
public class ProductionOption : ScriptableObject
{
    [Header("Identificative information")]
    [Min(0)] public int Id; //An ID belonging to the option for lookups
    public string DisplayName = "Unnamed"; //A display name for both debugging and actual displaying

    [field: Space]
    [field: Space]
    [field: Header("Production Rules")]
    [SerializeReference, SerializeReferenceDropdown] public IProgressFactory ProgressionFactory;
    [Space]
    [SerializeReference, SerializeReferenceDropdown] public ICompletionFactory[] CompletionFactories;
    [field: Space]
    [field: SerializeField] public AvailabilityConfig AvailabilityConfig { get; private set; }
}