using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Loom.Unity3d;
using UnityEngine;

namespace Loom.Blackjack
{
    public class GameStateController : MonoBehaviour
    {
        public const int kMaxPlayers = 3;
        public const int kMinBet = 5;
        public const int kMaxBet = 100;

        public event Action RoomListChanged;
        public event Action StateChanged;
        public string BackendHost = "127.0.0.1";
        public TextAsset ContractAbi;

        private BlackjackContractClient client;
        private GameState gameState = new GameState();
        private List<Room> rooms = new List<Room>();

        public BlackjackContractClient Client => this.client;
        public GameState GameState => this.gameState;
        public List<Room> Rooms => this.rooms;

        public async Task UpdateRoomList()
        {
            await this.client.ConnectToContract();
            BlackjackContractClient.GetRoomsOutput getRoomsOutput = await this.client.Room.GetRooms();
            this.rooms.Clear();
            for (int i = 0; i < getRoomsOutput.RoomNames.Count; i++)
            {
                byte[] roomNameBytes = getRoomsOutput.RoomNames[i];
                string roomName = Encoding.UTF8.GetString(roomNameBytes);
                BigInteger roomId = getRoomsOutput.RoomIds[i];
                this.rooms.Add(new Room
                {
                    Id = roomId,
                    Name = roomName
                });
            }

            this.RoomListChanged?.Invoke();
        }

        public async Task UpdateGameState()
        {
            BlackjackContractClient.GetGameStateOutput updatedGameState = await this.client.Game.GetGameState(this.gameState.RoomId);
            if (this.gameState.Players == null || this.gameState.Players.Length != updatedGameState.Players.Count)
            {
                this.gameState.Players = new GameState.PlayerState[updatedGameState.Players.Count];
            }

            this.gameState.Dealer.Address = (Address) updatedGameState.Dealer;
            this.gameState.Dealer.Hand = updatedGameState.DealerHand.Select(cardIndex => new Card(cardIndex)).ToArray();
            //this.gameState.Dealer.Winning = (int) updatedGameState.DealerWinning;
            for (int i = 0; i < updatedGameState.Players.Count; i++)
            {
                BlackjackContractClient.GetGameStatePlayerOutput playerState =
                    await this.client.Game.GetGameStatePlayer(this.gameState.RoomId, updatedGameState.Players[i]);
                if (this.gameState.Players[i] == null)
                {
                    this.gameState.Players[i] = new GameState.PlayerState();
                }

                this.gameState.Players[i].Address = (Address) updatedGameState.Players[i];
                this.gameState.Players[i].Hand = playerState.Hand.Select(cardIndex => new Card(cardIndex)).ToArray();
                this.gameState.Players[i].Bet = (int) playerState.Bet;
                //this.gameState.Players[i].Winning = (int) playerState.Winning;
                this.gameState.Players[i].ReadyForNextRound = playerState.ReadyForNextRound;
            }

            this.StateChanged?.Invoke();
        }

        private async void Start()
        {
            byte[] privateKey = CryptoUtils.GeneratePrivateKey();
            byte[] publicKey = CryptoUtils.PublicKeyFromPrivateKey(privateKey);
            this.client = new BlackjackContractClient(this.BackendHost, this.ContractAbi.text, privateKey, publicKey, NullLogger.Instance);
            this.client.RoomCreated += ClientOnRoomCreated;
            this.client.PlayerJoined += ClientOnPlayerJoined;
            this.client.PlayerLeft += ClientOnPlayerLeft;
            this.client.PlayerBetted += ClientOnPlayerBetted;
            this.client.PlayerReadyForNextRoundChanged += ClientOnPlayerReadyForNextRoundChanged;
            this.client.GameStageChanged += ClientOnGameStageChanged;
            this.client.GameRoundResultsAnnounced += ClientOnGameRoundResultsAnnounced;
            this.client.CurrentPlayerIndexChanged += ClientOnCurrentPlayerIndexChanged;
            this.client.PlayerDecisionReceived += ClientOnPlayerDecisionReceived;

            await UpdateRoomList();
        }

        private void OnDisable()
        {
            this.client.RoomCreated -= ClientOnRoomCreated;
            this.client.PlayerJoined -= ClientOnPlayerJoined;
            this.client.PlayerLeft -= ClientOnPlayerLeft;
            this.client.PlayerBetted -= ClientOnPlayerBetted;
            this.client.PlayerReadyForNextRoundChanged -= ClientOnPlayerReadyForNextRoundChanged;
            this.client.GameStageChanged -= ClientOnGameStageChanged;
            this.client.GameRoundResultsAnnounced -= ClientOnGameRoundResultsAnnounced;
            this.client.CurrentPlayerIndexChanged -= ClientOnCurrentPlayerIndexChanged;
            this.client.PlayerDecisionReceived -= ClientOnPlayerDecisionReceived;
        }

        private void Update()
        {
            this.client.Update();
        }

        private async Task ClientOnRoomCreated(Address creator, BigInteger roomId)
        {
            Debug.Log("Room created: " + roomId);
            if (this.gameState.IsInGame)
            {
                if (this.gameState.Role == PlayerRole.Dealer)
                {
                    if (this.client.Address != creator)
                        return;

                    Debug.Log("Set room: " + roomId);
                    this.gameState.RoomId = roomId;
                }
            } else
            {
                await UpdateRoomList();
                this.StateChanged?.Invoke();
            }
        }

        private async Task ClientOnPlayerJoined(BigInteger roomId, Address player)
        {
            if (roomId != this.gameState.RoomId)
                return;

            await UpdateGameState();
        }

        private Task ClientOnGameRoundResultsAnnounced(BigInteger roomId, BigInteger dealerOutcome, Address[] players, BigInteger[] playerOutcome)
        {
            if (roomId != this.gameState.RoomId)
                return Task.CompletedTask;

            this.gameState.Dealer.Outcome = (int) dealerOutcome;
            for (int i = 0; i < players.Length; i++)
            {
                GameState.PlayerState playerState = this.gameState.Players.First(ps => ps.Address == players[i]);
                playerState.Outcome = (int) playerOutcome[i];
            }

            this.StateChanged?.Invoke();
            return Task.CompletedTask;
        }

        private Task ClientOnPlayerBetted(BigInteger roomId, Address player, BigInteger bet)
        {
            if (roomId != this.gameState.RoomId)
                return Task.CompletedTask;

            GameState.PlayerState playerState = this.gameState.Players.FirstOrDefault(state => state.Address == player);
            if (playerState == null)
                return Task.CompletedTask;

            playerState.Bet = (int) bet;
            this.StateChanged?.Invoke();
            return Task.CompletedTask;
        }

        private async Task ClientOnPlayerLeft(BigInteger roomId, Address player)
        {
            if (roomId != this.gameState.RoomId)
                return;

            await UpdateGameState();
        }

        private async Task ClientOnPlayerDecisionReceived(BigInteger roomId, Address address, PlayerDecision decision)
        {
            if (roomId != this.gameState.RoomId)
                return;

            await UpdateGameState();
        }

        private Task ClientOnPlayerReadyForNextRoundChanged(BigInteger roomId, Address player, bool ready)
        {
            if (roomId != this.gameState.RoomId)
                return Task.CompletedTask;

            GameState.PlayerState playerState = this.gameState.Players.FirstOrDefault(state => state.Address == player);
            if (playerState == null)
                return Task.CompletedTask;

            playerState.ReadyForNextRound = ready;
            this.StateChanged?.Invoke();
            return Task.CompletedTask;
        }

        private async Task ClientOnCurrentPlayerIndexChanged(BigInteger roomId, int playerIndex, Address player)
        {
            if (roomId != this.gameState.RoomId)
                return;

            this.gameState.CurrentPlayer = player;
            this.gameState.CurrentPlayerIndex = playerIndex;
            await UpdateGameState();
        }

        private async Task ClientOnGameStageChanged(BigInteger roomId, GameStage stage)
        {
            if (stage == GameStage.Destroyed)
                return;

            if (roomId != this.gameState.RoomId)
                return;

            Debug.Log("Game state changed: " + stage);
            this.gameState.Stage = stage;
            await UpdateGameState();
        }
    }
}
