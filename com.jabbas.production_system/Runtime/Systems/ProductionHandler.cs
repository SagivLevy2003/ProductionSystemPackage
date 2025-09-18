using FishNet;
using FishNet.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;


namespace Jabbas.ProductionSystem
{
    /// <summary>
    /// Combines all the different production mechanisms to create a full production system.
    /// </summary>
    [RequireComponent(typeof(ProductionClientMirror))]
    [RequireComponent(typeof(ProductionOptionStateManager))]
    public sealed class ProductionHandler : NetworkBehaviour //Manages the production chain of an object
    {
        [Header("Sub-Systems")]
        [field: SerializeField] public ProductionOptionStateManager OptionStateManager { get; private set; }
        [field: SerializeField] public IdQueue<ProductionInstance> Queue { get; private set; } = new();

        //Id Generator 
        private static int _nextInstanceId = 0;
        public static int NextInstanceId() => Interlocked.Increment(ref _nextInstanceId);

        [Space]
        [Header("Properties")]
        //Properties
        [Min(0.05f)][SerializeField] private float _tickInterval = 0.1f;

        [Tooltip("Note: 0 means infinite capacity.")]
        [Min(0)]
        [SerializeField] private int _maxConcurrentProduction = 1;

        #region Events
        /// <summary>
        /// Called on the server when a production is added.
        /// </summary>
        public event Action<ProductionInstance> ServerProductionAdded;

        /// <summary>
        /// Called on the server when a production is canceled.
        /// </summary>
        public event Action<ProductionInstance> ServerProductionCanceled;

        /// <summary>
        /// Called on the server when a production is completed. <br/>
        /// Gets called after the instance's <see cref="ProductionInstance.OnCompleted"/>. event. 
        /// </summary>
        public event Action<ProductionInstance> ServerProductionCompleted;
        #endregion

        //Debug
        [SerializeField] private bool _debugLog = false;

        //Cached for performance
        private WaitForSeconds _waitCoroutine;
        private readonly List<ProductionInstance> _completedBuffer = new(5);

        public override void OnStartServer()
        {
            base.OnStartServer();
            OptionStateManager = GetComponent<ProductionOptionStateManager>();
            _waitCoroutine = new(_tickInterval);
            StartCoroutine(TickProcessor());
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            StopAllCoroutines();
        }


        /// <summary>
        /// Ticks all the instances in a pre-defined interval.
        /// </summary>
        [Server]
        IEnumerator TickProcessor()
        {
            while (true)
            {
                if (Queue.Count > 0)
                {
                    List<ProductionInstance> completedInstances = TickAllInstances();

                    foreach (var instance in completedInstances)
                    {
                        HandleProductionCompletion(instance);
                    }
                }

                yield return _waitCoroutine;
            }
        }


        /// <summary>
        /// Ticks all the instances.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns>A list of all instances completed this tick.</returns>
        [Server]
        public List<ProductionInstance> TickAllInstances()
        {
            _completedBuffer.Clear();

            int concurrentInstancesToTick =
                (_maxConcurrentProduction == 0)
                    ? Queue.Count //Set to queue count if there isn't a ticking limit
                    : Mathf.Min(_maxConcurrentProduction, Queue.Count); //Clamp the max concurrent production to the queue's size


            for (int i = 0; i < concurrentInstancesToTick; i++)
            {
                ProductionInstance instance = Queue.Entries[i].Value;

                if (instance.Tick())
                    _completedBuffer.Add(instance);
            }

            return _completedBuffer;
        }


        #region Public API
        [Client]
        public void RequestProductionOnClient(ProductionOption option)
        {
            /*Flow:
             * Check if option is null
             * Create a parameter request container and fill it with the relevant data
             * Check if the request is available
             * Send request to the server for validation and execution
             */

            if (_debugLog) ProductionDebugLogger.LogMessage(this, $"Client requesting production option: <color=cyan>{(option ? option.DisplayName : "null")}</color>");

            if (!option)
            {
                ProductionDebugLogger.LogMessage(this, "A null option was send to the production handler.", true);
                return;
            }

            if (!OptionStateManager.States.ContainsKey(option.Id)) //Checks if the option exists 
            {
                ProductionDebugLogger.LogMessage(this, $"Option does not exist for Id: <color=cyan>{option.Id}</color>, Name: <color=cyan>{option.DisplayName}</color>", true);
                return;
            }

            if (!OptionStateManager.States[option.Id]) //Checks if the option is disabled
            {
                ProductionDebugLogger.LogMessage(this, $"A disabled option was sent to the production handler. " +
                    $"Name: <color=cyan>{option.DisplayName}</color>", true);
                return;
            }

            ProductionRequestData requestData = new() //Create a request data container
            {
                SourceId = ObjectId, //Set the source to be the handler's network object ID
                RequestOwnerId = InstanceFinder.ClientManager.Connection.ClientId,
            };

            Server_RequestProduction(option.Id, requestData);
        }


        [ServerRpc(RequireOwnership = false)]
        private void Server_RequestProduction(int optionId, ProductionRequestData requestData)
        {
            /*Flow:
             * Ensure there's free space in the production queue
             * Parse the option id into a concrete option
             * Check with the validation rule if the request is valid
             * Build an instance from the parameters and option
             * Enqueue the built instance
             * Fire event on client and server
             */

            if (_debugLog) ProductionDebugLogger.LogMessage(this, $"Production request sent to server with option: <color=cyan>{optionId}</color>");

            if (Queue.IsFull) return;

            if (!OptionStateManager.TryGetOptionById(optionId, out ProductionOption option))
            {
                ProductionDebugLogger.LogMessage(this, $"Option does not exist for Id: <color=cyan>{optionId}</color>", true);
                return;
            }

            if (!OptionStateManager.States[option.Id]) //Checks if the option is disabled
            {
                ProductionDebugLogger.LogMessage(this, $"A disabled option was sent to the production handler from the client. " +
                    $"Name: <color=cyan>{option.DisplayName}</color>", true);
                return;
            }

            if (!option.AvailabilityConfig.IsAvailable(requestData.RequestOwnerId, requestData.SourceId))
            {
                if (_debugLog) ProductionDebugLogger.LogMessage(this, $"The production option <color=cyan>{option.DisplayName}</color> isn't available.");
                return;
            }

            int instanceId = NextInstanceId();

            ProductionInstance instance = new(option, requestData, instanceId);
            if (!Queue.Enqueue(instanceId, instance)) return; //Return if failed to add the instance to the queue


            ServerProductionAdded?.Invoke(instance);
        }


        [ServerRpc(RequireOwnership = false)]
        public void Request_CancelProduction(int instanceId)
        {
            if (_debugLog) ProductionDebugLogger.LogMessage(this, $"Cancellation request sent to server with instanceId: <color=cyan>{instanceId}</color>");


            if (!Queue.TryGetById(instanceId, out ProductionInstance instance))
            {
                ProductionDebugLogger.LogMessage(this, $"Client attempted to cancel a production that doesn't exist on the server! Id:<color=cyan>{instanceId}</color>");
                return;
            }

            Queue.RemoveById(instanceId);

            ServerProductionCanceled?.Invoke(instance);
        }
        #endregion


        [Server]
        private void HandleProductionCompletion(ProductionInstance instance)
        {
            if (_debugLog) ProductionDebugLogger.LogMessage(this, $"Production completed: <color=cyan>{instance.Id}</color>");
            Queue.RemoveById(instance.Id);
            ServerProductionCompleted?.Invoke(instance);
        }
    }
}