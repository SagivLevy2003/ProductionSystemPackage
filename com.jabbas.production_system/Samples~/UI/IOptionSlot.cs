using Jabbas.ProductionSystem;

public interface IOptionSlot
{
    public void InitializeSlot(ProductionOption option, ProductionHandler handler);
    public void SetEnabled(bool isEnabled);

    //how do I tell the handler to produce this? what information do I keep?
}