using System;
using System.Numerics;
using Loom.Unity3d;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Loom.Blackjack
{
    public class GameDebugUI : MonoBehaviour
    {
        public GameStateController GameStateController;

        private readonly BlackjackContractClient[] clients = new BlackjackContractClient[3];
        private BlackjackContractClient currentClient;
        private bool debugUIEnabled;

        private void Start()
        {
            this.debugUIEnabled = Application.isEditor;
            for (int i = 0; i < this.clients.Length; i++)
            {
                byte[] privateKey = CryptoUtils.GeneratePrivateKey();
                byte[] publicKey = CryptoUtils.PublicKeyFromPrivateKey(privateKey);
                this.clients[i] = new BlackjackContractClient(this.GameStateController.BackendHost, this.GameStateController.ContractAbi.text, privateKey, publicKey, NullLogger.Instance);
            }

            this.currentClient = this.clients[0];
        }

        private async void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.KeypadPlus)
            {
                this.debugUIEnabled = !this.debugUIEnabled;
            }

            if (!this.debugUIEnabled)
                return;

            try
            {
                GUILayout.BeginVertical(GUI.skin.box);
                {
                    GUILayout.Label("Debug Menu");
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Player " + (Array.IndexOf((Array) this.clients, this.currentClient) + 1));
                        for (int i = 0; i < this.clients.Length; i++)
                        {
                            if (GUILayout.Button((i + 1).ToString()))
                            {
                                this.currentClient = this.clients[i];
                            }
                        }
                    }
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Join Room"))
                    {
                        await this.currentClient.Room.JoinRoom(this.GameStateController.GameState.RoomId);
                    }

                    if (GUILayout.Button("Bet 100"))
                    {
                        await this.currentClient.Game.PlaceBet(this.GameStateController.GameState.RoomId, new BigInteger(100));
                    }

                    if (GUILayout.Button("Start Game"))
                    {
                        await this.currentClient.Game.StartGame(this.GameStateController.GameState.RoomId);
                    }

                    if (GUILayout.Button("Leave Room"))
                    {
                        await this.currentClient.Room.LeaveRoom(this.GameStateController.GameState.RoomId);
                    }

                    if (GUILayout.Button("Hit"))
                    {
                        await this.currentClient.Game.PlayerDecision(this.GameStateController.GameState.RoomId, PlayerDecision.Hit);
                    }

                    if (GUILayout.Button("Stand"))
                    {
                        await this.currentClient.Game.PlayerDecision(this.GameStateController.GameState.RoomId, PlayerDecision.Stand);
                    }

                    if (GUILayout.Button("Want next round"))
                    {
                        await this.currentClient.Game.SetPlayerReadyForNextRound(this.GameStateController.GameState.RoomId, true);
                    }

                    if (GUILayout.Button("Next round"))
                    {
                        await this.currentClient.Game.NextRound(this.GameStateController.GameState.RoomId);
                    }

                    if (GUILayout.Button("Create room"))
                    {
                        await this.currentClient.Room.CreateRoom("test " + Random.Range(100, 1000));
                    }
                }
                GUILayout.EndVertical();
            } catch (ArgumentException)
            {
            } catch (NullReferenceException)
            {
            }
        }
    }
}