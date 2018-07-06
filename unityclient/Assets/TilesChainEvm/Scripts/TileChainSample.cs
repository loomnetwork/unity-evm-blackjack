using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Loom.Unity3d.Samples.TilesChainEvm {
    public class TileChainSample : MonoBehaviour {
        public Sprite PointSprite;
        public Sprite SquareSprite;
        public Vector2 GameFieldSize = new Vector2(640, 480);
        public Text StatusText;
        public Button ReconnectButton;

        private readonly List<GameObject> tileGameObjects = new List<GameObject>();
        private TileChainContractClient client;
        private JsonTileMapState jsonTileMapState = new JsonTileMapState();
        private Color32 color;

        private async void Start() {
            Camera.main.orthographicSize = GameFieldSize.y / 2f;
            Camera.main.transform.position = new Vector3(GameFieldSize.x / 2f, GameFieldSize.y / 2f, Camera.main.transform.position.z);

            GameObject gameFieldGo = new GameObject("GameField");
            SpriteRenderer gameFieldSpriteRenderer = gameFieldGo.AddComponent<SpriteRenderer>();
            gameFieldSpriteRenderer.sprite = SquareSprite;
            gameFieldSpriteRenderer.sortingOrder = -1;
            gameFieldSpriteRenderer.color = Color.black;
            gameFieldGo.transform.localScale = new Vector3(GameFieldSize.x, GameFieldSize.y, 1f);
            gameFieldGo.transform.position = GameFieldSize * 0.5f;

            // Pick nice random color for this player
            this.color = Random.ColorHSV(0, 1, 1, 1, 1, 1);

            // The private key is used to sign transactions sent to the DAppChain.
            // Usually you'd generate one private key per player, or let them provide their own.
            // In this sample we just generate a new key every time.
            var privateKey = CryptoUtils.GeneratePrivateKey();
            var publicKey = CryptoUtils.PublicKeyFromPrivateKey(privateKey);
            this.client = new TileChainContractClient(privateKey, publicKey, Debug.unityLogger);
            this.client.TileMapStateUpdated += ClientOnTileMapStateUpdated;

            await ConnectClient();
        }

        private void Update() {
            this.client.Update();
            if (Input.GetMouseButtonDown(0) && this.client.IsConnected) {
                Ray screenPointToRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                Vector2 dotPosition = screenPointToRay.origin;

                if (dotPosition.x < 0f || dotPosition.x > GameFieldSize.x ||
                    dotPosition.y < 0f || dotPosition.y > GameFieldSize.y)
                    return;

                JsonTileMapState.Tile tile = new JsonTileMapState.Tile {
                    color = new JsonTileMapState.Tile.Color {
                        r = this.color.r,
                        g = this.color.g,
                        b = this.color.b,
                    },
                    point = new Vector2Int((int) dotPosition.x, (int) GameFieldSize.y - (int) dotPosition.y)
                };
                this.jsonTileMapState.tiles.Add(tile);
#pragma warning disable 4014
                this.client.SetTileMapState(this.jsonTileMapState);
#pragma warning restore 4014
            }
        }

        private async Task ConnectClient()
        {
            ReconnectButton.gameObject.SetActive(false);
            StatusText.gameObject.SetActive(true);
            StatusText.text = "Connecting...";
            try
            {
                await this.client.ConnectToContract();
                JsonTileMapState jsonTileMapState = await this.client.GetTileMapState();
                UpdateTileMap(jsonTileMapState);

                StatusText.gameObject.SetActive(false);
            } catch (Exception e)
            {
                StatusText.text = "Error: " + e.Message;
                ReconnectButton.gameObject.SetActive(true);
                Debug.LogException(e);
            }
        }

        private void ClientOnTileMapStateUpdated(JsonTileMapState obj) {
            UpdateTileMap(obj);
        }

        private void UpdateTileMap(JsonTileMapState jsonTileMapState) {
            this.jsonTileMapState = jsonTileMapState ?? new JsonTileMapState();

            foreach (GameObject tile in this.tileGameObjects) {
                Destroy(tile);
            }

            this.tileGameObjects.Clear();
            foreach (JsonTileMapState.Tile tile in this.jsonTileMapState.tiles) {
                GameObject go = new GameObject("Tile");
                go.transform.localScale = Vector3.one * 16f;
                go.transform.position = new Vector3(tile.point.x, GameFieldSize.y - tile.point.y, 0);
                SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = PointSprite;
                spriteRenderer.color = new Color32((byte) tile.color.r, (byte) tile.color.g, (byte) tile.color.b, 255);

                this.tileGameObjects.Add(go);
            }
        }

        public async void ReconnectButtonHandler()
        {
            await ConnectClient();
        }
    }
}