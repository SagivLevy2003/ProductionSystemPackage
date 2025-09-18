using System;

namespace Jabbas.ProductionSystem
{
    [Serializable]
    public struct MirroredProductionInstance
    {
        //Data
        public int OptionId;

        //Identification
        public int InstanceId;
        public int RequestOwnerId;
        public int SourceId;

        //State
        public float Progress;
        public bool IsCompleted { get; private set; }
        public bool IsPaused;

        public MirroredProductionInstance(int optionId, int instanceId, int requestOwnerId, int sourceId, float progress, bool completed, bool paused)
        {
            OptionId = optionId;
            InstanceId = instanceId;
            RequestOwnerId = requestOwnerId;
            SourceId = sourceId;
            Progress = progress;
            IsCompleted = completed;
            IsPaused = paused;
        }
    }
}
