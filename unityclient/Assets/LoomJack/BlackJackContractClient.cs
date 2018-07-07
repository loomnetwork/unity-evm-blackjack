using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loom.Nethereum.ABI.FunctionEncoding.Attributes;
using Loom.Unity3d;
using Org.BouncyCastle.Math;
using UnityEngine;

namespace Loom.BlackJack
{
    /// <summary>
    ///     Abstracts interaction with the contract.
    /// </summary>
    public class BlackJackContractClient
    {
        private readonly string abi;
        private readonly Queue<Action> eventActions = new Queue<Action>();
        private readonly ILogger logger;
        private readonly byte[] privateKey;
        private readonly byte[] publicKey;
        private DAppChainClient client;
        private EvmContract contract;
        private IRpcClient reader;
        private IRpcClient writer;

        private GameObject gameObject;
        private RoomObject roomObject;

        public BlackJackContractClient(string abi, byte[] privateKey, byte[] publicKey, ILogger logger)
        {
            this.abi = abi;
            this.privateKey = privateKey;
            this.publicKey = publicKey;
            this.logger = logger;

            gameObject = new GameObject(this);
            roomObject = new RoomObject(this);
        }

        public bool IsConnected => reader.IsConnected;
        public GameObject Game => gameObject;
        public RoomObject Room => roomObject;

        public async Task ConnectToContract()
        {
            if (contract == null)
            {
                contract = await GetContract();
            }
        }

        public void Update()
        {
            while (eventActions.Count > 0)
            {
                Action action = eventActions.Dequeue();
                action();
            }
        }

        private async Task<EvmContract> GetContract()
        {
            writer = RpcClientFactory.Configure()
                .WithLogger(Debug.unityLogger)
                .WithWebSocket("ws://127.0.0.1:46657/websocket")
                .Create();

            reader = RpcClientFactory.Configure()
                .WithLogger(Debug.unityLogger)
                .WithWebSocket("ws://127.0.0.1:9999/queryws")
                .Create();

            client = new DAppChainClient(writer, reader)
                { Logger = logger };

            // required middleware
            client.TxMiddleware = new TxMiddleware(new ITxMiddlewareHandler[]
            {
                new NonceTxMiddleware(publicKey, client),
                new SignedTxMiddleware(privateKey)
            });

            Address contractAddr = await client.ResolveContractAddressAsync("BlackJack");

            Address callerAddr = Address.FromPublicKey(publicKey);
            EvmContract evmContract = new EvmContract(client, contractAddr, callerAddr, abi);

            evmContract.EventReceived += EventReceivedHandler;
            return evmContract;
        }

        private void EventReceivedHandler(object sender, EvmChainEventArgs e)
        {
            Debug.Log("Event: " + e.EventName);
        }

        [FunctionOutput]
        public class GetRoomsOutput
        {
            [Parameter("uint256[]")]
            public byte[] RoomIds { get; set; }

            [Parameter("bytes32[]")]
            public byte[][] RoomNames { get; set; }
        }

        [FunctionOutput]
        public class GetGameStatePlayerOutput
        {
            [Parameter("uint256[]")]
            public byte[] RoomIds { get; set; }

            [Parameter("bytes32[]")]
            public byte[][] RoomNames { get; set; }
        }

        public abstract class LogicObject
        {
            protected readonly BlackJackContractClient Client;

            protected LogicObject(BlackJackContractClient client)
            {
                Client = client;
            }
        }

        public class GameObject : LogicObject
        {
            public GameObject(BlackJackContractClient client) : base(client)
            {
            }

            public async Task PlayerDecision(BigInteger roomId, PlayerDecision decision)
            {
                await Client.ConnectToContract();
                await Client.contract.CallAsync("playerDecision", roomId, (int) decision);
            }

            public async Task<GetGameStatePlayerOutput> GetGameStatePlayer(BigInteger roomId, string playerAddress)
            {
                await Client.ConnectToContract();
                return await Client.contract.StaticCallDtoTypeOutputAsync<GetGameStatePlayerOutput>("getGameStatePlayer", roomId, playerAddress);
            }
        }

        public class RoomObject : LogicObject
        {
            public RoomObject(BlackJackContractClient client) : base(client)
            {
            }

            public async Task CreateRoom(string roomName)
            {
                await Client.ConnectToContract();
                await Client.contract.CallAsync("createRoom", roomName);
            }

            public async Task JoinRoom(BigInteger roomId)
            {
                await Client.ConnectToContract();
                await Client.contract.CallAsync("joinRoom", roomId);
            }

            public async Task StartGame(BigInteger roomId)
            {
                await Client.ConnectToContract();
                await Client.contract.CallAsync("startGame", roomId);
            }

            public async Task<GetRoomsOutput> GetRooms()
            {
                await Client.ConnectToContract();
                return await Client.contract.StaticCallDtoTypeOutputAsync<GetRoomsOutput>("getRooms");
            }
        }
    }
}
