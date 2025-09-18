
public interface IAvailabilityRule
{
    public bool IsAvailable(int requestOwnerId, int sourceId, out string message);
}