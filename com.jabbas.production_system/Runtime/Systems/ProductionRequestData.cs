public struct ProductionRequestData
{
    /// <summary>
    /// The ID of the player associated as the caller of the request.
    /// </summary>
    public int RequestOwnerId;

    /// <summary>
    /// The ID of the source of the call. Can be used in validation and creation rules for world-context.
    /// </summary>
    public int SourceId;
}
