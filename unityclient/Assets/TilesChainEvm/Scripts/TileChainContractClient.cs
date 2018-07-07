using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loom.Nethereum.ABI.FunctionEncoding;
using Loom.Nethereum.ABI.FunctionEncoding.Attributes;
using Loom.Nethereum.ABI.Model;
using UnityEngine;

namespace Loom.Unity3d.Samples.TilesChainEvm
{
    /// <summary>
    /// Abstracts interaction with the contract.
    /// </summary>
    public class TileChainContractClient
    {
        private readonly byte[] privateKey;
        private readonly byte[] publicKey;
        private readonly ILogger logger;
        private readonly Queue<Action> eventActions = new Queue<Action>();
        private EvmContract contract;
        private DAppChainClient client;
        private IRpcClient reader;
        private IRpcClient writer;

        public event Action<JsonTileMapState> TileMapStateUpdated;

        public TileChainContractClient(byte[] privateKey, byte[] publicKey, ILogger logger)
        {
            this.privateKey = privateKey;
            this.publicKey = publicKey;
            this.logger = logger;
        }

        public bool IsConnected => this.reader.IsConnected;

        public async Task ConnectToContract()
        {
            if (this.contract == null)
            {
                this.contract = await GetContract();
            }
        }

        public async Task<JsonTileMapState> GetTileMapState()
        {
            await ConnectToContract();

            TileMapStateOutput result = await this.contract.StaticCallDTOTypeOutputAsync<TileMapStateOutput>("GetTileMapState");
            if (result == null)
                throw new Exception("Smart contract didn't return anything!");

            JsonTileMapState jsonTileMapState = JsonUtility.FromJson<JsonTileMapState>(result.State);
            return jsonTileMapState;
        }

        public async Task SetTileMapState(JsonTileMapState jsonTileMapState)
        {
            await ConnectToContract();

            string tileMapState = JsonUtility.ToJson(jsonTileMapState);
            await this.contract.CallAsync("SetTileMapState", tileMapState);
        }

        public void Update()
        {
            while (this.eventActions.Count > 0)
            {
                Action action = this.eventActions.Dequeue();
                action();
            }
        }

        private async Task<EvmContract> GetContract()
        {
            this.writer = RPCClientFactory.Configure()
                .WithLogger(Debug.unityLogger)
                .WithWebSocket("ws://127.0.0.1:46657/websocket")
                .Create();

            this.reader = RPCClientFactory.Configure()
                .WithLogger(Debug.unityLogger)
                .WithWebSocket("ws://127.0.0.1:9999/queryws")
                .Create();

            this.client = new DAppChainClient(this.writer, this.reader)
                { Logger = this.logger };

            // required middleware
            this.client.TxMiddleware = new TxMiddleware(new ITxMiddlewareHandler[]
            {
                new NonceTxMiddleware(this.publicKey, this.client),
                new SignedTxMiddleware(this.privateKey)
            });

            const string abi = "[{\"constant\":false,\"inputs\":[{\"name\":\"_tileState\",\"type\":\"string\"}],\"name\":\"SetTileMapState\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"GetTileMapState\",\"outputs\":[{\"name\":\"\",\"type\":\"string\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"name\":\"state\",\"type\":\"string\"}],\"name\":\"OnTileMapStateUpdate\",\"type\":\"event\"}]\r\n";
            var contractAddr = await this.client.ResolveContractAddressAsync("TilesChain");

            var callerAddr = Address.FromPublicKey(this.publicKey);
            EvmContract evmContract = new EvmContract(this.client, contractAddr, callerAddr, abi);

            evmContract.EventReceived += this.EventReceivedHandler;
            return evmContract;
        }

        private void EventReceivedHandler(object sender, EvmChainEventArgs e)
        {
            if (e.EventName != "OnTileMapStateUpdate")
                return;

            OnTileMapStateUpdateEvent onTileMapStateUpdateEvent = e.DecodeEventDTO<OnTileMapStateUpdateEvent>();
            JsonTileMapState jsonTileMapState = JsonUtility.FromJson<JsonTileMapState>(onTileMapStateUpdateEvent.State);

            this.eventActions.Enqueue(() =>
            {
                TileMapStateUpdated?.Invoke(jsonTileMapState);
            });
        }

        [FunctionOutput]
        public class TileMapStateOutput
        {
            [Parameter("string", "state", 1)]
            public string State { get; set; }
        }

        [Function("GetTileMapState", "string")]
        public class TileMapStateFunction
        {
            [Parameter("string", "state", 1)]
            public string State { get; set; }
        }

        public class OnTileMapStateUpdateEvent
        {
            [Parameter("string", "state", 1)]
            public string State { get; set; }
        }
    }
}
