using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Loom.BlackJack;
using Loom.Nethereum.ABI.Model;
using Loom.Unity3d;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

namespace Loom.BlackJack
{
    public class Controller : MonoBehaviour
    {
        public string BackendHost = "127.0.0.1";
        public TextAsset ContractAbi;

        public GamePrefabsContainer PrefabsContainer;
        public GameObject GameContainer;
        public GameObject RoomListContainer;
        public GameObject RoomList;
        public Transform GameField;
        public InputField CreateRoomRoomNameInputField;
        public PlayerPositionsContainer PlayerPositionsContainer;
        public Text GameStatus;
        public GameObject GameActionButtonsContainer;

        private BlackJackContractClient[] clients = new BlackJackContractClient[4];
        private BlackJackContractClient client;
        private BlackJackContractClient debugClient;
        private Screen currentScreen;
        private PlayerRole currentRole;
        private BigInteger currentRoomId;
        private bool isInGame;
        private GameState gameState = new GameState();

        private async void Start()
        {
            SetScreen(Screen.RoomList);

            for (int i = 0; i < this.clients.Length; i++)
            {
                var privateKey = CryptoUtils.GeneratePrivateKey();
                var publicKey = CryptoUtils.PublicKeyFromPrivateKey(privateKey);
                this.clients[i] = new BlackJackContractClient(BackendHost, this.ContractAbi.text, privateKey, publicKey, Debug.unityLogger);
            }

            this.client = this.clients[0];
            this.client.RoomCreated += ClientOnRoomCreated;
            this.client.PlayerJoined += ClientOnPlayerJoined;
            this.client.GameStageChanged += ClientOnGameStageChanged;
            this.client.CurrentPlayerIndexChanged += ClientOnCurrentPlayerIndexChanged;
            this.client.PlayerDecisionReceived += ClientOnPlayerDecisionReceived;

            this.debugClient = this.clients[1];
            await RefreshRoomList();
        }

        private void OnDisable()
        {
            this.client.RoomCreated -= ClientOnRoomCreated;
            this.client.PlayerJoined -= ClientOnPlayerJoined;
            this.client.GameStageChanged -= ClientOnGameStageChanged;
            this.client.CurrentPlayerIndexChanged -= ClientOnCurrentPlayerIndexChanged;
            this.client.PlayerDecisionReceived -= ClientOnPlayerDecisionReceived;
        }

        private void Update()
        {
            this.client.Update();
        }

        private async void ClientOnPlayerJoined(BigInteger roomId, Address player)
        {
            if (roomId != this.currentRoomId)
                return;

            await UpdateGameState();
        }

        private async void ClientOnPlayerDecisionReceived(BigInteger roomId, int playerIndex, Address player, PlayerDecision playerDecision)
        {
            if (roomId != this.currentRoomId)
                return;

            Debug.LogFormat("Player {0} decision: {1}", playerIndex, playerDecision);
            await UpdateGameState();
        }

        private async void ClientOnCurrentPlayerIndexChanged(BigInteger roomId, int playerIndex, Address player)
        {
            if (roomId != this.currentRoomId)
                return;

            Debug.Log("Current player index: " + playerIndex);
            await UpdateGameState();
        }

        private void ClientOnGameStageChanged(BigInteger roomId, GameStage stage)
        {
            if (roomId != this.currentRoomId)
                return;

            Debug.Log("Game state changed: " + stage);
            this.GameStatus.text = stage.ToString();
        }

        private async void ClientOnRoomCreated(Address creator, BigInteger roomId)
        {
            Debug.Log("Room created: " + roomId);
            if (this.currentScreen == Screen.Game)
            {
                if (this.currentRole == PlayerRole.Dealer)
                {
                    if (this.client.Address != creator)
                        return;

                    this.currentRoomId = roomId;
                    this.GameStatus.text = "Waiting for players...";
                }
            } else
            {
                await RefreshRoomList();
            }
        }

        private static void CalculateHandScore(IList<Card> hand, out int softScore, out int hardScore)
        {
            int baseScore = 0;
            int aceCount = 0;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].CardValue == CardValue.Ace)
                {
                    aceCount++;
                    continue;
                }

                baseScore += hand[i].CardScore;
            }


            hardScore = softScore = baseScore;
            for (int i = 0; i < aceCount; i++) {
                if (hardScore + 11 > 21) {
                    hardScore += 1;
                } else {
                    hardScore += 11;
                }

                softScore += 1;
            }
        }

        private void UpdatePlayerView(
            Func<PlayerView> getPlayerViewFunc,
            Action<PlayerView> setPlayerViewAction,
            Vector3 position,
            IList<byte> handRaw
            )
        {
            GameObject playerViewGO;
            if (getPlayerViewFunc() == null)
            {
                playerViewGO = Instantiate(this.PrefabsContainer.PlayerViewPrefab, this.GameField);
                setPlayerViewAction(playerViewGO.GetComponent<PlayerView>());
            } else
            {
                playerViewGO = getPlayerViewFunc().gameObject;
            }

            playerViewGO.GetComponent<RectTransform>().anchoredPosition = position;

            Card[] hand = handRaw.Select(cardIndex => new Card(cardIndex)).ToArray();
            getPlayerViewFunc().SetCards(hand, this.PrefabsContainer);

            int softScore, hardScore;
            CalculateHandScore(hand, out softScore, out hardScore);

            string scoreText = softScore == hardScore ? softScore.ToString() : softScore + "/" + hardScore;
            getPlayerViewFunc().UIContainer.HandScore.text = scoreText;
            getPlayerViewFunc().UIContainer.HandScore.color = hardScore > 21 ? Color.red : Color.black;
        }

        private async Task UpdateGameState()
        {
            BlackJackContractClient.GetGameStateOutput gameState = await this.client.Game.GetGameState(this.currentRoomId);

            UpdatePlayerView(
                () => this.gameState.DealerView,
                pv => this.gameState.DealerView = pv,
                this.PlayerPositionsContainer.Dealer.anchoredPosition3D,
                gameState.DealerHand
            );
            this.gameState.DealerView.UIContainer.PlayerName.text = "Dealer";
            this.gameState.DealerView.UIContainer.ActivePlayerMarker.SetActive(false);
            for (int i = 0; i < gameState.Players.Count; i++)
            {
                BlackJackContractClient.GetGameStatePlayerOutput playerState =
                    await this.client.Game.GetGameStatePlayer(this.currentRoomId, gameState.Players[i]);
                int playerIndex = i;
                UpdatePlayerView(
                    () => this.gameState.PlayerViews[playerIndex],
                    pv => this.gameState.PlayerViews[playerIndex] = pv,
                    this.PlayerPositionsContainer.Players[playerIndex].anchoredPosition3D,
                    playerState.Hand
                );

                this.gameState.PlayerViews[playerIndex].UIContainer.PlayerName.text = "Player " + (i + 1);
                this.gameState.PlayerViews[playerIndex].UIContainer.ActivePlayerMarker.SetActive(gameState.PlayerIndex == i);
            }
        }

        private async Task RefreshRoomList()
        {
            await this.client.ConnectToContract();
            BlackJackContractClient.GetRoomsOutput getRoomsOutput = await this.client.Room.GetRooms();
            foreach (Transform child in this.RoomList.transform)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < getRoomsOutput.RoomNames.Count; i++)
            {
                byte[] roomNameBytes = getRoomsOutput.RoomNames[i];
                string roomName = Encoding.UTF8.GetString(roomNameBytes);
                GameObject roomListItemGo = Instantiate(this.PrefabsContainer.RoomListItemPrefab, this.RoomList.transform);
                RoomListItemUIContainer roomListItemUiContainer = roomListItemGo.GetComponent<RoomListItemUIContainer>();
                roomListItemUiContainer.ButtonText.text = roomName;
                int index = i;
                roomListItemUiContainer.Button.onClick.AddListener(async () =>
                {
                    BigInteger roomId = getRoomsOutput.RoomIds[index];
                    await this.client.Room.JoinRoom(roomId);
                    this.currentRoomId = roomId;
                });
            }
        }

        private void SetRole(PlayerRole role)
        {
            this.currentRole = role;
            switch (this.currentRole)
            {
                case PlayerRole.Dealer:
                    this.GameActionButtonsContainer.SetActive(false);
                    break;
                case PlayerRole.Player:
                    this.GameActionButtonsContainer.SetActive(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SetScreen(Screen screen)
        {
            this.currentScreen = screen;
            switch (this.currentScreen)
            {
                case Screen.RoomList:
                    this.GameContainer.SetActive(false);
                    this.RoomListContainer.SetActive(true);
                    break;
                case Screen.Game:
                    this.GameContainer.SetActive(true);
                    this.RoomListContainer.SetActive(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(screen), this.currentScreen, null);
            }
        }

        private async void OnGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Debug Menu");
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Player " + Array.IndexOf(this.clients, this.debugClient));
                    for (int i = 0; i < this.clients.Length; i++)
                    {
                        if (GUILayout.Button(i.ToString()))
                        {
                            this.debugClient = this.clients[i];
                        }
                    }
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Join Room"))
                {
                    await this.debugClient.Room.JoinRoom(this.currentRoomId);
                }

                if (GUILayout.Button("Bet 100"))
                {
                    await this.debugClient.Game.PlaceBet(this.currentRoomId, new BigInteger(100));
                }

                if (GUILayout.Button("Start Game"))
                {
                    await this.debugClient.Room.StartGame(this.currentRoomId);
                }

                if (GUILayout.Button("Leave Room"))
                {
                    await this.debugClient.Room.LeaveRoom(this.currentRoomId);
                }

                if (GUILayout.Button("Hit"))
                {
                    await this.debugClient.Game.PlayerDecision(this.currentRoomId, PlayerDecision.Hit);
                }

                if (GUILayout.Button("Stand"))
                {
                    await this.debugClient.Game.PlayerDecision(this.currentRoomId, PlayerDecision.Stand);
                }

                if (GUILayout.Button("Update"))
                {
                    await UpdateGameState();
                }

                if (GUILayout.Button("Create room"))
                {
                    await this.debugClient.Room.CreateRoom("test " + Random.Range(100, 1000));
                }
            }
            GUILayout.EndVertical();
        }

        #region UI Handlers

        public async void GameHitClickHandler()
        {

        }

        public async void GameStandClickHandler()
        {

        }

        public async void GameLeaveClickHandler()
        {
            await this.client.Room.LeaveRoom(this.currentRoomId);
            this.isInGame = false;
        }

        public async void RefreshRoomListClickHandler()
        {
            await RefreshRoomList();
        }

        public async void CreateRoomClickHandler()
        {
            if (String.IsNullOrWhiteSpace(this.CreateRoomRoomNameInputField.text))
                return;

            await this.client.ConnectToContract();
            await this.client.Room.CreateRoom(this.CreateRoomRoomNameInputField.text);
            this.isInGame = true;
            this.CreateRoomRoomNameInputField.text = "";

            SetScreen(Screen.Game);
            SetRole(PlayerRole.Dealer);
        }

        #endregion

        private class GameState
        {
            public PlayerView DealerView;
            public PlayerView[] PlayerViews = new PlayerView[3];
        }

        private enum PlayerRole
        {
            Dealer,
            Player
        }

        private enum Screen
        {
            RoomList,
            Game
        }
    }
}