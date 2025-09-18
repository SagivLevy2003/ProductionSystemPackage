using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "ProductionSystem/Databases/Basic Database")]
public class ProductionDatabase : ScriptableObject
{
    [SerializeField] private List<ProductionOption> _database = new();
    public IReadOnlyList<ProductionOption> Options => _database;

    public bool Contains(int optionId)
    {
        return _database.Any(opt => opt.Id == optionId);
    }

    public bool TryGetOptionById(int optionId, out ProductionOption option)
    {
        option = _database.FirstOrDefault(opt => opt.Id == optionId);
        return option != null;
    }
}

