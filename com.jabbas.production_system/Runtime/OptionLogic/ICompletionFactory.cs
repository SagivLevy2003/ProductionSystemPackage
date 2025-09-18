
/// <summary>
/// A static, instance-independent 
/// </summary>
public interface ICompletionFactory
{
    public ICompletionRuntime CreateRuntime();
}

/// <summary>
/// Executes completion logic for the production.
/// </summary>
public interface ICompletionRuntime
{
    /// <summary>
    /// Executes when production finishes for a given instance.
    /// </summary>
    public void OnCompletion(ProductionInstance instance);

}
