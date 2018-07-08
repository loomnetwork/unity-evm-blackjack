using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Loom.Nethereum.ABI.FunctionEncoding.Attributes;
using Loom.Unity3d;
using UnityEngine;

namespace Loom.Blackjack
{
    /// <summary>
    ///     Abstracts interaction with the contract.
    /// </summary>
    public class BlackjackContractClient
    {
        private readonly string backendHost;
        private readonly string abi;
        private readonly Queue<Action> eventActions = new Queue<Action>();
        private readonly ILogger logger;
        private readonly byte[] privateKey;
        private readonly byte[] publicKey;
        private readonly GameObject gameObject;
        private readonly RoomObject roomObject;

        private DAppChainClient client;
        private EvmContract contract;
        private IRpcClient reader;
        private IRpcClient writer;

        public delegate void RoomCreatedEventHandler(Address creator, BigInteger roomId);
        public delegate void PlayerJoinedEventHandler(BigInteger roomId,  Address player);
        public delegate void GameStageChangedEventHandler(BigInteger roomId, GameStage stage);
        public delegate void CurrentPlayerIndexChangedEventHandler(BigInteger roomId, int playerIndex, Address player);
        public delegate void PlayerDecisionReceivedEventHandler(BigInteger roomId, int playerIndex, Address player, PlayerDecision playerDecision);

        public event RoomCreatedEventHandler RoomCreated;
        public event PlayerJoinedEventHandler PlayerJoined;
        public event GameStageChangedEventHandler GameStageChanged;
        public event CurrentPlayerIndexChangedEventHandler CurrentPlayerIndexChanged;
        public event PlayerDecisionReceivedEventHandler PlayerDecisionReceived;

        public BlackjackContractClient(string backendHost, string abi, byte[] privateKey, byte[] publicKey, ILogger logger)
        {
            this.backendHost = backendHost;
            this.abi = abi;
            this.privateKey = privateKey;
            this.publicKey = publicKey;
            this.logger = logger;

            this.gameObject = new GameObject(this);
            this.roomObject = new RoomObject(this);
        }

        public bool IsConnected => this.reader.IsConnected;
        public GameObject Game => this.gameObject;
        public RoomObject Room => this.roomObject;
        public Address Address => this.contract.Caller;

        public async Task ConnectToContract()
        {
            if (this.contract == null)
            {
                this.contract = await GetContract();
            }
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
            this.writer = RpcClientFactory.Configure()
                .WithLogger(this.logger)
                .WithWebSocket("ws://" + this.backendHost + ":46657/websocket")
                .Create();

            this.reader = RpcClientFactory.Configure()
                .WithLogger(this.logger)
                .WithWebSocket("ws://" + this.backendHost + ":9999/queryws")
                .Create();

            this.client = new DAppChainClient(this.writer, this.reader)
                { Logger = this.logger };

            // required middleware
            this.client.TxMiddleware = new TxMiddleware(new ITxMiddlewareHandler[]
            {
                new NonceTxMiddleware(this.publicKey, this.client),
                new SignedTxMiddleware(this.privateKey)
            });

            Address contractAddr = await this.client.ResolveContractAddressAsync("BlackJack");

            Address callerAddr = Address.FromPublicKey(this.publicKey);
            EvmContract evmContract = new EvmContract(this.client, contractAddr, callerAddr, this.abi);

            evmContract.EventReceived += EventReceivedHandler;
            return evmContract;
        }

        private void EventReceivedHandler(object sender, EvmChainEventArgs e)
        {
            //Debug.Log("Event: " + e.EventName);
            switch (e.EventName)
            {
                case "RoomCreated":
                {
                    RoomCreatedEventData eventDto = e.DecodeEventDto<RoomCreatedEventData>();
                    this.eventActions.Enqueue(() => RoomCreated?.Invoke((Address) eventDto.Creator, eventDto.RoomId));
                    break;
                }
                case "PlayerJoined":
                {
                    PlayerJoinedEventData eventDto = e.DecodeEventDto<PlayerJoinedEventData>();
                    this.eventActions.Enqueue(() => PlayerJoined?.Invoke(eventDto.RoomId, (Address) eventDto.Player));
                    break;
                }
                case "GameStageChanged":
                {
                    GameStageChangedEventData eventDto = e.DecodeEventDto<GameStageChangedEventData>();
                    this.eventActions.Enqueue(() => GameStageChanged?.Invoke(eventDto.RoomId, eventDto.Stage));
                    break;
                }
                case "CurrentPlayerIndexChanged":
                {
                    CurrentPlayerIndexChangedEventData eventDto = e.DecodeEventDto<CurrentPlayerIndexChangedEventData>();
                    this.eventActions.Enqueue(() => CurrentPlayerIndexChanged?.Invoke(eventDto.RoomId, eventDto.PlayerIndex, (Address) eventDto.PlayerAddress));
                    break;
                }
                case "PlayerDecisionReceived":
                {
                    PlayerDecisionReceivedEventData eventDto = e.DecodeEventDto<PlayerDecisionReceivedEventData>();
                    this.eventActions.Enqueue(() => PlayerDecisionReceived?.Invoke(eventDto.RoomId, eventDto.PlayerIndex, (Address) eventDto.PlayerAddress, eventDto.PlayerDecision));
                    break;
                }
            }
        }

        public abstract class LogicObject
        {
            protected readonly BlackjackContractClient Client;

            protected LogicObject(BlackjackContractClient client)
            {
                this.Client = client;
            }
        }

        public class GameObject : LogicObject
        {
            public GameObject(BlackjackContractClient client) : base(client)
            {
            }

            public async Task PlaceBet(BigInteger roomId, BigInteger bet)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("placeBet", roomId, bet);
            }

            public async Task PlayerDecision(BigInteger roomId, PlayerDecision decision)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("playerDecision", roomId, (int) decision);
            }

            public async Task<GetGameStateOutput> GetGameState(BigInteger roomId)
            {
                await this.Client.ConnectToContract();
                return await this.Client.contract.StaticCallDtoTypeOutputAsync<GetGameStateOutput>("getGameState", roomId);
            }

            public async Task<GetGameStatePlayerOutput> GetGameStatePlayer(BigInteger roomId, string playerAddress)
            {
                await this.Client.ConnectToContract();
                return await this.Client.contract.StaticCallDtoTypeOutputAsync<GetGameStatePlayerOutput>("getGameStatePlayer", roomId, playerAddress);
            }
        }

        public class RoomObject : LogicObject
        {
            public RoomObject(BlackjackContractClient client) : base(client)
            {
            }

            public async Task CreateRoom(string roomName)
            {
                await this.Client.ConnectToContract();
                byte[] roomNameBytes = Encoding.UTF8.GetBytes(roomName);
                await this.Client.contract.CallAsync("createRoom", roomNameBytes);
            }

            public async Task JoinRoom(BigInteger roomId)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("joinRoom", roomId);
            }

            public async Task LeaveRoom(BigInteger roomId)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("leaveRoom", roomId);
            }

            public async Task StartGame(BigInteger roomId)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("startGame", roomId);
            }

            public async Task<GetRoomsOutput> GetRooms()
            {
                await this.Client.ConnectToContract();
                return await this.Client.contract.StaticCallDtoTypeOutputAsync<GetRoomsOutput>("getRooms");
            }
        }

        [FunctionOutput]
        public class GetRoomsOutput
        {
            [Parameter("uint256[]")]
            public List<BigInteger> RoomIds { get; set; }

            [Parameter("bytes32[]")]
            public List<byte[]> RoomNames { get; set; }
        }

        [FunctionOutput]
        public class GetGameStateOutput
        {
            [Parameter("uint")]
            public GameStage Stage { get; set; }

            [Parameter("uint8[]")]
            public List<byte> UsedCards { get; set; }

            [Parameter("address[]")]
            public List<string> Players { get; set; }

            [Parameter("uint")]
            public int PlayerIndex { get; set; }

            [Parameter("uint8[]")]
            public List<byte> DealerHand { get; set; }
        }

        [FunctionOutput]
        public class GetGameStatePlayerOutput
        {
            [Parameter("uint8[]")]
            public List<byte> Hand { get; set; }

            [Parameter("uint")]
            public BigInteger Bet { get; set; }

            [Parameter("uint")]
            public BigInteger Winnings { get; set; }
        }

        public class RoomCreatedEventData
        {
            [Parameter("address")]
            public string Creator { get; set; }

            [Parameter("uint")]
            public BigInteger RoomId { get; set; }
        }

        public class PlayerJoinedEventData
        {
            [Parameter("uint")]
            public BigInteger RoomId { get; set; }

            [Parameter("address")]
            public string Player { get; set; }
        }

        public class GameStageChangedEventData
        {
            [Parameter("uint")]
            public BigInteger RoomId { get; set; }

            [Parameter("uint")]
            public GameStage Stage { get; set; }
        }

        public class CurrentPlayerIndexChangedEventData
        {
            [Parameter("uint")]
            public BigInteger RoomId { get; set; }

            [Parameter("uint")]
            public int PlayerIndex{ get; set; }

            [Parameter("address")]
            public string PlayerAddress { get; set; }
        }

        public class PlayerDecisionReceivedEventData
        {
            [Parameter("uint")]
            public BigInteger RoomId { get; set; }

            [Parameter("uint")]
            public int PlayerIndex { get; set; }

            [Parameter("address")]
            public string PlayerAddress { get; set; }

            [Parameter("uint")]
            public PlayerDecision PlayerDecision { get; set; }
        }
    }
}
