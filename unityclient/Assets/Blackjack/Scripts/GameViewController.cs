using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Loom.Unity3d;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Vector3 = UnityEngine.Vector3;

namespace Loom.Blackjack
{
    public class GameViewController : MonoBehaviour
    {
        public GameStateController GameStateController;
        public GamePrefabsContainer PrefabsContainer;
        public GameObject MainCanvas;
        public GameObject GameContainer;
        public GameObject RoomListContainer;
        public GameObject RoomList;
        public GameObject RoomListNoRoomsLabel;
        public GameObject GameFieldContainer;
        public InputField CreateRoomRoomNameInputField;
        public PlayerPositionsContainer PlayerPositionsContainer;
        public GameObject GamePlayerUIContainer;
        public GameObject GamePlayerActionsUIContainer;
        public GameObject GameBettingUIContainer;
        public InputField GameBetInputField;
        public GameObject GameDealerUIContainer;
        public GameObject GameDealerNextRoundUIContainer;
        public GameObject GameDealerNextRoundUINextRoundButton;
        public GameObject GameDealerNextRoundUIWaitingButton;
        public GameObject GameDealerStartGameUIContainer;
        public GameObject GameDealerStartGameUIStartGameButton;
        public GameObject GameDealerStartGameUIWaitingButton;
        public GameObject GameReadyStateToggleContainer;
        public Text GameStatus;
        public Text BalanceValueText;
        public GameObject ModalDialogPrefab;

        private GameUIState gameUIState = new GameUIState();
        private Address? lastPlayerLeft;
        private bool networkLostDialogShown;

        private void Start()
        {
            SetScreen(Screen.RoomList);
            this.GameStateController.StateChanged += GameStateControllerOnStateChanged;
            this.GameStateController.RoomListChanged += GameStateControllerOnRoomListChanged;
            this.GameStateController.PlayerLeft += GameStateControllerOnPlayerLeft;
        }

        private void GameStateControllerOnPlayerLeft(Address player)
        {
            this.lastPlayerLeft = player;
            if (this.GameStateController.Client.Address == player)
            {
                SetScreen(Screen.RoomList);
                this.GameStateController.ResetState();
            }
        }

        private async void GameStateControllerOnRoomListChanged()
        {
            UpdateRoomList();
            await UpdateBalance();
        }

        private void OnDestroy()
        {
            this.GameStateController.StateChanged -= GameStateControllerOnStateChanged;
            this.GameStateController.RoomListChanged -= GameStateControllerOnRoomListChanged;
            this.GameStateController.PlayerLeft -= GameStateControllerOnPlayerLeft;
        }

        private void Update()
        {
            if (!this.networkLostDialogShown && !this.GameStateController.Client.IsConnected)
            {
                this.networkLostDialogShown = true;
                ShowModalDialog(
                    "Connection Lost",
                    "Connection to BlackJack DAppChain could not be established.\n\n" +
                    "Click OK to try to reconnect.",
                    async () =>
                    {
                        this.networkLostDialogShown = false;
                        await this.GameStateController.Client.Reconnect();
                        await this.GameStateController.UpdateRoomList();
                        await UpdateBalance();
                    }
                );
            }
        }

        private async void GameStateControllerOnStateChanged()
        {
            if (this.GameStateController.GameState.Stage == GameStage.Destroyed)
            {
                if (this.lastPlayerLeft == null || this.lastPlayerLeft.Value != this.GameStateController.Client.Address)
                {
                    ShowModalDialog(
                        "Game Terminated",
                        "The game was terminated becaused dealer or other player has left the game.\nYour bet was refunded.",
                        null
                    );
                }

                this.GameStateController.ResetState();
                this.lastPlayerLeft = null;
                this.GameStateController.GameState.IsInGame = false;
                SetScreen(Screen.RoomList);
                await this.GameStateController.UpdateRoomList();
                UpdateRoomList();
            } else if (this.gameUIState.Screen == Screen.RoomList)
            {
                await this.GameStateController.UpdateRoomList();
            }
            UpdateUI();
            await UpdateBalance();
        }

        private async Task UpdateBalance()
        {
            BigInteger balance = await this.GameStateController.GetBalance();
            this.BalanceValueText.text = FormatMonetaryValueAsRichText(balance);
        }

        private void UpdateRoomList()
        {
            foreach (Transform child in this.RoomList.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (Room room in this.GameStateController.Rooms)
            {
                GameObject roomListItemGo = Instantiate(this.PrefabsContainer.RoomListItemPrefab, this.RoomList.transform);
                RoomListItemUIContainer roomListItemUiContainer = roomListItemGo.GetComponent<RoomListItemUIContainer>();
                roomListItemUiContainer.ButtonText.text = room.Name;
                roomListItemUiContainer.Button.onClick.AddListener(() => JoinRoomClickHandler(room.Id));
            }

            this.RoomListNoRoomsLabel.SetActive(this.GameStateController.Rooms.Count == 0);
        }

        public void UpdateUI()
        {
            this.GamePlayerActionsUIContainer.SetActive(this.GameStateController.GameState.Stage == GameStage.PlayersTurn && this.GameStateController.GameState.CurrentPlayer == this.GameStateController.Client.Address
            );

            bool selfAlreadyBet =
                this.GameStateController.GameState.Players.Any(state => state.Address == this.GameStateController.Client.Address && state.Bet != 0);
            this.GameBettingUIContainer.SetActive(
                !selfAlreadyBet &&
                this.GameStateController.GameState.Stage == GameStage.WaitingForPlayersAndBetting
                );
            //this.GameFieldContainer.SetActive(GameState.Stage == GameStage.PlayersTurn || GameState.Stage == GameStage.DealerTurn);

            if (this.GameStateController.GameState.Stage == GameStage.WaitingForPlayersAndBetting)
            {
                this.GameReadyStateToggleContainer.GetComponentInChildren<Toggle>().isOn = false;
            }

            this.GameReadyStateToggleContainer.SetActive(this.GameStateController.GameState.Stage == GameStage.Ended);

            this.GameDealerNextRoundUIContainer.SetActive(this.GameStateController.GameState.Stage == GameStage.Ended);
            bool allPlayersReady =
                this.GameStateController.GameState.Players.Length != 0 &&
                this.GameStateController.GameState.Players.All(state => state.ReadyForNextRound);
            this.GameDealerNextRoundUINextRoundButton.SetActive(allPlayersReady);
            this.GameDealerNextRoundUIWaitingButton.SetActive(!allPlayersReady);

            this.GameDealerStartGameUIContainer.SetActive(this.GameStateController.GameState.Stage == GameStage.WaitingForPlayersAndBetting);
            bool allPlayersBetted =
                this.GameStateController.GameState.Players.Length != 0 &&
                this.GameStateController.GameState.Players.All(state => state.Bet != 0);
            this.GameDealerStartGameUIStartGameButton.SetActive(allPlayersBetted);
            this.GameDealerStartGameUIWaitingButton.SetActive(!allPlayersBetted);

            string statusText;
            switch (this.GameStateController.GameState.Stage)
            {
                case GameStage.WaitingForPlayersAndBetting:
                case GameStage.Started:
                    statusText = "Waiting for players...";
                    if (allPlayersBetted)
                    {
                        statusText = "Waiting for dealer to start the game...";
                    }
                    break;
                case GameStage.PlayersTurn:
                    statusText = $"Player {this.GameStateController.GameState.CurrentPlayerIndex + 1} Turn";
                    break;
                case GameStage.DealerTurn:
                    statusText = "Dealers Turn";
                    break;
                case GameStage.Ended:
                    statusText = "Round Ended";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            this.GameStatus.text = statusText;

            UpdatePlayerView(
                () => this.gameUIState.DealerView,
                pv => this.gameUIState.DealerView = pv,
                this.PlayerPositionsContainer.Dealer.anchoredPosition3D, this.GameStateController.GameState.Dealer,
                "Dealer",
                true
            );

            for (int i = 0; i < this.gameUIState.PlayerViews.Length; i++)
            {
                PlayerView playerView = this.gameUIState.PlayerViews[i];
                if (playerView == null)
                    continue;

                Destroy(playerView.gameObject);
                this.gameUIState.PlayerViews[i] = null;
            }

            for (int i = 0; i < this.GameStateController.GameState.Players.Length; i++)
            {
                int playerIndex = i;
                UpdatePlayerView(
                    () => this.gameUIState.PlayerViews[playerIndex],
                    pv => this.gameUIState.PlayerViews[playerIndex] = pv,
                    this.PlayerPositionsContainer.Players[playerIndex].anchoredPosition3D, this.GameStateController.GameState.Players[i],
                    "Player " + (i + 1),
                    false
                );
            }
        }

        private void UpdatePlayerView(
            Func<PlayerView> getPlayerViewFunc,
            Action<PlayerView> setPlayerViewAction,
            Vector3 position,
            GameState.PlayerState playerState,
            string playerName,
            bool isDealer
        )
        {
            GameObject playerViewGO;
            if (getPlayerViewFunc() == null)
            {
                playerViewGO = Instantiate(this.PrefabsContainer.PlayerViewPrefab, this.GameFieldContainer.transform);
                setPlayerViewAction(playerViewGO.GetComponent<PlayerView>());
            } else
            {
                playerViewGO = getPlayerViewFunc().gameObject;
            }

            playerViewGO.GetComponent<RectTransform>().anchoredPosition = position;
            PlayerView playerView = getPlayerViewFunc();

            if (this.GameStateController.GameState.Stage == GameStage.Ended)
            {
                playerName += $" ({FormatMonetaryValueAsRichText(playerState.Outcome)})";
            }

            playerView.UIContainer.BetContainer.SetActive(playerState.Bet > 0);
            playerView.UIContainer.BetValue.text = playerState.Bet.ToString();

            playerView.UIContainer.PlayerName.text = playerName;
            playerView.UIContainer.ActivePlayerMarker.SetActive(
                !isDealer &&
                this.GameStateController.GameState.Stage == GameStage.PlayersTurn &&
                this.GameStateController.GameState.CurrentPlayer == playerState.Address
                );
            playerView.UIContainer.SelfPlayerMarker.SetActive(
                playerState.Address == this.GameStateController.Client.Address
                );

            bool showScore = playerState.Hand != null && playerState.Hand.Length != 0;
            playerView.UIContainer.HandScoreContainer.SetActive(showScore);
            playerView.UIContainer.ReadyForNextRoundMarker.SetActive(false);
            playerView.UIContainer.NotReadyForNextRoundMarker.SetActive(false);
            if (showScore)
            {
                playerView.SetCards(playerState.Hand, this.PrefabsContainer);

                int softScore, hardScore;
                BlackjackRules.CalculateHandScore(playerState.Hand, out softScore, out hardScore);

                string scoreText = softScore == hardScore ? softScore.ToString() : softScore + "/" + hardScore;
                playerView.UIContainer.HandScore.text = scoreText;
                playerView.UIContainer.HandScore.color = hardScore > 21 ? new Color(0.65f, 0, 0) : Color.black;
                playerView.UIContainer.BlackjackLabelContainer.SetActive(playerState.Hand.Length == 2 && hardScore == 21);

                if (!isDealer && this.GameStateController.GameState.Stage == GameStage.Ended)
                {
                    playerView.UIContainer.ReadyForNextRoundMarker.SetActive(playerState.ReadyForNextRound);
                    playerView.UIContainer.NotReadyForNextRoundMarker.SetActive(!playerState.ReadyForNextRound);
                }
            } else
            {
                playerView.SetCards(Array.Empty<Card>(), this.PrefabsContainer);
                playerView.UIContainer.BlackjackLabelContainer.SetActive(false);
            }
        }

        private void SetRole(PlayerRole role)
        {
            this.GameStateController.GameState.Role = role;
            switch (this.GameStateController.GameState.Role)
            {
                case PlayerRole.Dealer:
                    this.GamePlayerUIContainer.SetActive(false);
                    this.GameDealerUIContainer.SetActive(true);
                    break;
                case PlayerRole.Player:
                    this.GamePlayerUIContainer.SetActive(true);
                    this.GameDealerUIContainer.SetActive(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SetScreen(Screen screen)
        {
            this.gameUIState.Screen = screen;
            switch (this.gameUIState.Screen)
            {
                case Screen.RoomList:
                    this.GameContainer.SetActive(false);
                    this.RoomListContainer.SetActive(true);
                    break;
                case Screen.Game:
                    this.GameContainer.SetActive(true);
                    this.RoomListContainer.SetActive(false);
                    this.GameStatus.text = "";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(screen), this.gameUIState.Screen, null);
            }
        }

        #region UI Handlers

        private async void JoinRoomClickHandler(BigInteger roomId)
        {
            Room room = this.GameStateController.Rooms.Find(r => r.Id == roomId);
            try
            {
                await this.GameStateController.Client.Room.JoinRoom(roomId);
            } catch (TxCommitException)
            {
                // The room is likely dead, remove it from the list
                this.GameStateController.Rooms.Remove(room);
                UpdateRoomList();
                return;
            }

            this.GameStateController.GameState.RoomId = roomId;
            SetRole(room.Creator == this.GameStateController.Client.Address ? PlayerRole.Dealer : PlayerRole.Player);

            SetScreen(Screen.Game);
            await this.GameStateController.UpdateGameState();
        }

        public async void GameHitClickHandler()
        {
            await this.GameStateController.Client.Game.PlayerDecision(this.GameStateController.GameState.RoomId, PlayerDecision.Hit);
        }

        public async void GameStandClickHandler()
        {
            await this.GameStateController.Client.Game.PlayerDecision(this.GameStateController.GameState.RoomId, PlayerDecision.Stand);
        }

        public async void GameLeaveClickHandler()
        {
            await this.GameStateController.Client.Room.LeaveRoom(this.GameStateController.GameState.RoomId);
        }

        public async void GameBetClickHandler()
        {
            int bet;
            Int32.TryParse(this.GameBetInputField.text, out bet);
            if (bet <= 0 || bet < GameStateController.kMinBet || bet > GameStateController.kMaxBet)
            {
                this.GameBetInputField.GetComponent<Image>().color = new Color32(255, 200, 200, 255);
                return;
            }

            this.GameBetInputField.GetComponent<Image>().color = Color.white;
            await this.GameStateController.Client.Game.PlaceBet(this.GameStateController.GameState.RoomId, bet);
        }

        public async void GameReadyForNextRoundValueChangedHandler(bool isReady)
        {
            await this.GameStateController.Client.Game.SetPlayerReadyForNextRound(this.GameStateController.GameState.RoomId, isReady);
        }

        public async void GameNextRoundClickHandler()
        {
            await this.GameStateController.Client.Game.NextRound(this.GameStateController.GameState.RoomId);
        }

        public async void GameStartGameClickHandler()
        {
            await this.GameStateController.Client.Game.StartGame(this.GameStateController.GameState.RoomId);
        }

        public async void CreateRoomClickHandler()
        {
            if (String.IsNullOrWhiteSpace(this.CreateRoomRoomNameInputField.text) ||
                this.CreateRoomRoomNameInputField.text.Trim().Length < 3)
                return;

            try
            {
                SetScreen(Screen.Game);
                SetRole(PlayerRole.Dealer);
                await this.GameStateController.Client.ConnectToContract();
                await this.GameStateController.Client.Room.CreateRoom(this.CreateRoomRoomNameInputField.text);
                this.GameStateController.GameState.IsInGame = true;
                this.CreateRoomRoomNameInputField.text = "";
            } catch (Exception)
            {
                SetScreen(Screen.RoomList);
            }
        }

        #endregion

        private void ShowModalDialog(string title, string text, Action okAction)
        {
            GameObject modalDialogGO = Instantiate(this.ModalDialogPrefab, this.MainCanvas.transform, false);
            modalDialogGO.transform.localScale = Vector3.one;
            modalDialogGO.transform.localPosition = Vector3.zero;
            ModalDialogUIContainer dialog = modalDialogGO.GetComponent<ModalDialogUIContainer>();
            dialog.Title.text = title;
            dialog.Text.text = text;

            UnityAction onOkClicked = null;
            onOkClicked = () => {
                okAction?.Invoke();
                dialog.OKButton.onClick.RemoveListener(onOkClicked);
                Destroy(modalDialogGO);
            };

            dialog.OKButton.onClick.AddListener(onOkClicked);
        }

        private string FormatMonetaryValueAsRichText(BigInteger value)
        {
            string outcomeColor = value == 0 ? "white" : (value > 0 ? "#30FF30" : "red");
            string outcomeText = (value == 0 ? "" : (value > 0 ? "+" : "−")) + BigInteger.Abs(value);
            return $"<color={outcomeColor}>{outcomeText}</color>";
        }

        private class GameUIState
        {
            public Screen Screen;
            public PlayerView DealerView;
            public PlayerView[] PlayerViews = new PlayerView[3];
        }

        private enum Screen
        {
            RoomList,
            Game
        }
    }
}
