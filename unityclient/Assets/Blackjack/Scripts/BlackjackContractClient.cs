using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
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
        private readonly Dictionary<BigInteger, RoomEventActionsState> roomIdToRoomEventActionsMap =
            new Dictionary<BigInteger, RoomEventActionsState>();
        private readonly ILogger logger;
        private readonly byte[] privateKey;
        private readonly byte[] publicKey;
        private readonly Address address;

        private DAppChainClient client;
        private EvmContract contract;
        private IRpcClient reader;
        private IRpcClient writer;

        public delegate Task RoomCreatedEventHandler(Address creator, BigInteger roomId);
        public delegate Task PlayerJoinedEventHandler(BigInteger roomId, Address player);
        public delegate Task PlayerLeftEventHandler(BigInteger roomId, Address player);
        public delegate Task PlayerBettedEventHandler(BigInteger roomId, Address player, BigInteger bet);
        public delegate Task PlayerReadyForNextRoundChangedEventHandler(BigInteger roomId, Address player, bool ready);
        public delegate Task GameStageChangedEventHandler(BigInteger roomId, GameStage stage);
        public delegate Task GameRoundResultsAnnouncedEventHandler(BigInteger roomId, BigInteger dealerOutcome, Address[] players, BigInteger[] playerOutcome);
        public delegate Task CurrentPlayerIndexChangedEventHandler(BigInteger roomId, int playerIndex, Address player);
        public delegate Task PlayerDecisionReceivedEventHandler(BigInteger roomId, Address player, PlayerDecision playerDecision);

        public event RoomCreatedEventHandler RoomCreated;
        public event PlayerJoinedEventHandler PlayerJoined;
        public event PlayerLeftEventHandler PlayerLeft;
        public event PlayerBettedEventHandler PlayerBetted;
        public event PlayerReadyForNextRoundChangedEventHandler PlayerReadyForNextRoundChanged;
        public event GameStageChangedEventHandler GameStageChanged;
        public event GameRoundResultsAnnouncedEventHandler GameRoundResultsAnnounced;
        public event CurrentPlayerIndexChangedEventHandler CurrentPlayerIndexChanged;
        public event PlayerDecisionReceivedEventHandler PlayerDecisionReceived;

        public BlackjackContractClient(string backendHost, string abi, byte[] privateKey, byte[] publicKey, ILogger logger)
        {
            this.backendHost = backendHost;
            this.abi = abi;
            this.privateKey = privateKey;
            this.publicKey = publicKey;
            this.logger = logger;

            this.Game = new GameObject(this);
            this.Room = new RoomObject(this);
            this.Common = new CommonObject(this);

            this.address = Address.FromPublicKey(this.publicKey);
        }

        public bool IsConnected => this.reader.IsConnected;
        public GameObject Game { get; }
        public RoomObject Room { get; }
        public CommonObject Common { get; }
        public Address Address => this.address;

        public async Task Reconnect()
        {
            if (this.contract != null)
            {
                this.contract.EventReceived -= EventReceivedHandler;
            }
            this.contract = await GetContract();
        }

        public async Task ConnectToContract()
        {
            if (this.contract == null)
            {
                this.contract = await GetContract();
            }
        }

        private class RoomEventActionsState
        {
            public BigInteger? NextExpectedEventNonce;
            public List<Tuple<BigInteger, Func<Task>>> RoomEventActions = new List<Tuple<BigInteger, Func<Task>>>();
        }

        public async void Update()
        {
            try
            {
                List<Func<Task>> orderedActions = new List<Func<Task>>();
                lock (this.roomIdToRoomEventActionsMap)
                {
                    foreach (KeyValuePair<BigInteger, RoomEventActionsState> roomEventActionsStatePair in this.roomIdToRoomEventActionsMap)
                    {
                        RoomEventActionsState roomEventActionsState = roomEventActionsStatePair.Value;
                        List<Tuple<BigInteger, Func<Task>>> roomEventActions = roomEventActionsState.RoomEventActions;
                        foreach (Tuple<BigInteger, Func<Task>> action in roomEventActions.Where(t => t.Item1 < 0))
                        {
                            orderedActions.Add(action.Item2);
                        }

                        roomEventActions = roomEventActions.Where(t => t.Item1 >= 0).ToList();
                        roomEventActionsState.RoomEventActions = roomEventActions;

                        lock (roomEventActions)
                        {
                            if (roomEventActions.Count == 0)
                                continue;

                            if (roomEventActionsState.NextExpectedEventNonce == null)
                            {
                                roomEventActionsState.NextExpectedEventNonce = 0;
                            }

                            BigInteger nextNonce = roomEventActionsState.NextExpectedEventNonce.Value;
                            if (roomEventActions.Find(t => t.Item1 == nextNonce) == null)
                                continue;

                            foreach (Tuple<BigInteger, Func<Task>> action in roomEventActions.OrderBy(t => t.Item1))
                            {
                                //action.Item2();
                                orderedActions.Add(action.Item2);
                                roomEventActionsState.NextExpectedEventNonce = action.Item1 + 1;
                            }

                            roomEventActions.Clear();
                        }
                    }
                }

                for (int i = 0; i < orderedActions.Count; i++)
                {
                    IEnumerable<Task> handlersTasks;
                    lock (orderedActions[i])
                    {
                        handlersTasks =
                            orderedActions[i]
                                .GetInvocationList()
                                .Cast<Func<Task>>()
                                .Select(handler => handler.Invoke());
                    }

                    await Task.WhenAll(handlersTasks);
                }
            } catch (Exception e)
            {
                Debug.LogException(e);
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

            Address contractAddr = await this.client.ResolveContractAddressAsync("Blackjack");
            EvmContract evmContract = new EvmContract(this.client, contractAddr, this.address, this.abi);

            evmContract.EventReceived += EventReceivedHandler;
            return evmContract;
        }

        private void EnqueueRoomEventAction(BigInteger roomId, BigInteger nonce, Func<Task> action)
        {
            RoomEventActionsState roomEventActionsState = GetRoomEventActionsState(roomId);

            lock (roomEventActionsState.RoomEventActions)
            {
                roomEventActionsState.RoomEventActions.Add(Tuple.Create(nonce, action));
            }
        }

        private RoomEventActionsState GetRoomEventActionsState(BigInteger roomId)
        {
            RoomEventActionsState roomEventActionsState;
            lock (this.roomIdToRoomEventActionsMap)
            {
                if (!this.roomIdToRoomEventActionsMap.TryGetValue(roomId, out roomEventActionsState))
                {
                    roomEventActionsState = new RoomEventActionsState();
                    this.roomIdToRoomEventActionsMap.Add(roomId, roomEventActionsState);
                }
            }

            return roomEventActionsState;
        }

        private void EventReceivedHandler(object sender, EvmChainEventArgs e)
        {
            lock (this.roomIdToRoomEventActionsMap)
            {
                switch (e.EventName)
                {
                    case "RoomCreated":
                    {
                        RoomCreatedEventData eventDto = e.DecodeEventDto<RoomCreatedEventData>();
                        EnqueueRoomEventAction(
                            eventDto.RoomId,
                            -1,
                            () => RoomCreated?.Invoke((Address) eventDto.Creator, eventDto.RoomId)
                            );
                        break;
                    }
                    case "PlayerJoined":
                    {
                        PlayerRelatedEventData eventDto = e.DecodeEventDto<PlayerRelatedEventData>();
                        EnqueueRoomEventAction(
                            eventDto.RoomId,
                            eventDto.Nonce,
                            () => PlayerJoined?.Invoke(eventDto.RoomId, (Address) eventDto.Player)
                        );
                        break;
                    }
                    case "PlayerLeft":
                    {
                        PlayerRelatedEventData eventDto = e.DecodeEventDto<PlayerRelatedEventData>();
                        EnqueueRoomEventAction(
                            eventDto.RoomId,
                            eventDto.Nonce,
                            () => PlayerLeft?.Invoke(eventDto.RoomId, (Address) eventDto.Player)
                        );
                        break;
                    }
                    case "PlayerBetted":
                    {
                        PlayerBettedEventData eventDto = e.DecodeEventDto<PlayerBettedEventData>();
                        EnqueueRoomEventAction(
                            eventDto.RoomId,
                            eventDto.Nonce,
                            () => PlayerBetted?.Invoke(eventDto.RoomId, (Address) eventDto.Player, eventDto.Bet)
                        );
                        break;
                    }
                    case "PlayerReadyForNextRoundChanged":
                    {
                        PlayerReadyForNextRoundChangedEventData eventDto = e.DecodeEventDto<PlayerReadyForNextRoundChangedEventData>();
                        EnqueueRoomEventAction(
                            eventDto.RoomId,
                            eventDto.Nonce,
                            () => PlayerReadyForNextRoundChanged?.Invoke(eventDto.RoomId, (Address) eventDto.Player, eventDto.Ready)
                        );
                        break;
                    }
                    case "GameStageChanged":
                    {
                        GameStageChangedEventData eventDto = e.DecodeEventDto<GameStageChangedEventData>();
                        EnqueueRoomEventAction(
                            eventDto.RoomId,
                            eventDto.Nonce,
                            () => GameStageChanged?.Invoke(eventDto.RoomId, eventDto.Stage)
                        );
                        break;
                    }
                    case "CurrentPlayerIndexChanged":
                    {
                        CurrentPlayerIndexChangedEventData eventDto = e.DecodeEventDto<CurrentPlayerIndexChangedEventData>();
                        EnqueueRoomEventAction(
                            eventDto.RoomId,
                            eventDto.Nonce,
                            () => CurrentPlayerIndexChanged?.Invoke(eventDto.RoomId, eventDto.PlayerIndex, (Address) eventDto.PlayerAddress)
                        );
                        break;
                    }
                    case "PlayerDecisionReceived":
                    {
                        PlayerDecisionReceivedEventData eventDto = e.DecodeEventDto<PlayerDecisionReceivedEventData>();
                        EnqueueRoomEventAction(
                            eventDto.RoomId,
                            eventDto.Nonce,
                            () => PlayerDecisionReceived?.Invoke(eventDto.RoomId, (Address) eventDto.Player, eventDto.PlayerDecision)
                        );
                        break;
                    }
                    case "GameRoundResultsAnnounced":
                    {
                        GameRoundResultsAnnouncedEventData eventDto = e.DecodeEventDto<GameRoundResultsAnnouncedEventData>();
                        EnqueueRoomEventAction(
                            eventDto.RoomId,
                            eventDto.Nonce,
                            () => GameRoundResultsAnnounced?.Invoke(
                                eventDto.RoomId,
                                eventDto.DealerOutcome,
                                eventDto.Players.Select(p => (Address) p).ToArray(),
                                eventDto.PlayerOutcome.ToArray())
                        );
                        break;
                    }
                    case "Log":
                    {
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException($"Unknown event {e.EventName}");
                }
            }
        }

        /// <summary>
        /// Truncates the strings to a number of bytes in a given encoding.
        /// </summary>
        /// <param name="str">String to truncate</param>
        /// <param name="maxByteCount">Maximum number of bytes the string can take in <paramref name="encoding"/></param>
        /// <param name="encoding">Target encoding. UTF8 is used if null</param>
        private static string TruncateStringToEncodingBytes(string str, int maxByteCount, Encoding encoding = null) {
            if (str == null)
                return null;

            if (encoding == null) {
                encoding = Encoding.UTF8;
            }

            int byteCount = encoding.GetByteCount(str);
            if (byteCount <= maxByteCount)
                return str;

            // Get first chars, no more than maxByteCount
            char[] chars = str.ToCharArray(0, str.Length > maxByteCount ? maxByteCount : str.Length);
            int targetCharIndex = 0;
            for (int i = chars.Length; i >= 1; i--) {
                byteCount = encoding.GetByteCount(chars, 0, i);
                if (byteCount <= maxByteCount) {
                    targetCharIndex = i;
                    break;
                }
            }

            str = new String(chars, 0, targetCharIndex);
            return str;
        }

        public abstract class LogicObject
        {
            protected BlackjackContractClient Client { get; }

            protected LogicObject(BlackjackContractClient client)
            {
                this.Client = client;
            }
        }

        public class CommonObject : LogicObject
        {
            public CommonObject(BlackjackContractClient client) : base(client)
            {
            }

            public async Task<BigInteger> GetBalance(Address address)
            {
                await this.Client.ConnectToContract();
                return await this.Client.contract.StaticCallSimpleTypeOutputAsync<BigInteger>("getBalance", address.LocalAddress);
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

            public async Task SetPlayerReadyForNextRound(BigInteger roomId, bool ready)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("setPlayerReadyForNextRound", roomId, ready);
            }

            public async Task NextRound(BigInteger roomId)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("nextRound", roomId);
            }

            public async Task StartGame(BigInteger roomId)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("startGame", roomId);
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
                roomName = TruncateStringToEncodingBytes(roomName, 32, Encoding.UTF8);
                byte[] roomNameBytes = Encoding.UTF8.GetBytes(roomName);
                await this.Client.contract.CallAsync("createRoom", roomNameBytes);
            }

            public async Task JoinRoom(BigInteger roomId)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("joinRoom", roomId);
                BigInteger eventNonce = await this.Client.contract.StaticCallSimpleTypeOutputAsync<BigInteger>("getEventNonce", roomId);
                this.Client.GetRoomEventActionsState(roomId).NextExpectedEventNonce = eventNonce;
            }

            public async Task LeaveRoom(BigInteger roomId)
            {
                await this.Client.ConnectToContract();
                await this.Client.contract.CallAsync("leaveRoom", roomId);
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

            [Parameter("address[]")]
            public List<string> Creators { get; set; }

            [Parameter("uint8[]")]
            public List<int> PlayerCounts { get; set; }
        }

        [FunctionOutput]
        public class GetGameStateOutput
        {
            [Parameter("uint")]
            public GameStage Stage { get; set; }

            [Parameter("uint8[]")]
            public List<byte> UsedCards { get; set; }

            [Parameter("address")]
            public string Dealer { get; set; }

            [Parameter("address[]")]
            public List<string> Players { get; set; }

            [Parameter("uint")]
            public int PlayerIndex { get; set; }

            [Parameter("uint8[]")]
            public List<byte> DealerHand { get; set; }

            [Parameter("int")]
            public BigInteger DealerWinning { get; set; }
        }

        [FunctionOutput]
        public class GetGameStatePlayerOutput
        {
            [Parameter("uint8[]")]
            public List<byte> Hand { get; set; }

            [Parameter("uint")]
            public BigInteger Bet { get; set; }

            [Parameter("int")]
            public BigInteger Winning { get; set; }

            [Parameter("bool")]
            public bool ReadyForNextRound { get; set; }
        }

        [FunctionOutput]
        private abstract class OrderedEventData
        {
            [Parameter("uint", -2)]
            public BigInteger Nonce { get; set; }
        }

        private class GameRoundResultsAnnouncedEventData : OrderedEventData
        {
            [Parameter("uint")]
            public BigInteger RoomId { get; set; }

            [Parameter("int")]
            public BigInteger DealerOutcome { get; set; }

            [Parameter("address[]")]
            public List<string> Players { get; set; }

            [Parameter("int[]")]
            public List<BigInteger> PlayerOutcome { get; set; }
        }

        private class RoomCreatedEventData
        {
            [Parameter("uint")]
            public BigInteger RoomId { get; set; }

            [Parameter("address")]
            public string Creator { get; set; }
        }

        private class PlayerRelatedEventData : OrderedEventData
        {
            [Parameter("uint", -1)]
            public BigInteger RoomId { get; set; }

            [Parameter("address", 0)]
            public string Player { get; set; }
        }

        private class PlayerReadyForNextRoundChangedEventData : PlayerRelatedEventData
        {
            [Parameter("bool")]
            public bool Ready { get; set; }
        }

        private class PlayerBettedEventData : PlayerRelatedEventData
        {
            [Parameter("uint")]
            public BigInteger Bet { get; set; }
        }

        private class PlayerDecisionReceivedEventData : PlayerRelatedEventData
        {
            [Parameter("uint")]
            public PlayerDecision PlayerDecision { get; set; }
        }

        private class GameStageChangedEventData : OrderedEventData
        {
            [Parameter("uint")]
            public BigInteger RoomId { get; set; }

            [Parameter("uint")]
            public GameStage Stage { get; set; }
        }

        private class CurrentPlayerIndexChangedEventData : OrderedEventData
        {
            [Parameter("uint")]
            public BigInteger RoomId { get; set; }

            [Parameter("uint")]
            public int PlayerIndex { get; set; }

            [Parameter("address")]
            public string PlayerAddress { get; set; }
        }
    }
}
