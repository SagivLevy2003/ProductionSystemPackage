public interface IProgressFactory
{
    public IProgressionRuntime CreateRuntime();
}

public interface IProgressionRuntime
{
    public float GetNextAbsoluteProgress(ProductionInstance instance, int _requestOwnerId, int sourceId);
}