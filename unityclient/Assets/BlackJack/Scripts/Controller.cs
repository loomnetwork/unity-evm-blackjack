using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Loom.BlackJack;
using Loom.Unity3d;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    public TextAsset ContractAbi;
    public GameObject RoomListContainer;
    public GameObject RoomList;
    public GameObject RoomListItemPrefab;
    public InputField CreateRoomRoomNameField;

    private BlackJackContractClient client;

    private async void Start()
    {
        var privateKey = CryptoUtils.GeneratePrivateKey();
        var publicKey = CryptoUtils.PublicKeyFromPrivateKey(privateKey);
        this.client = new BlackJackContractClient(ContractAbi.text, privateKey, publicKey, Debug.unityLogger);
        this.client.RoomCreated += ClientOnRoomCreated;
        await RefreshRoomList();
    }

    private void Update()
    {
        client.Update();
    }

    private async void ClientOnRoomCreated()
    {
        await RefreshRoomList();
    }

    public async void RefreshRoomListClickHandler()
    {
        await RefreshRoomList();
    }

    private async Task RefreshRoomList()
    {
        await this.client.ConnectToContract();
        BlackJackContractClient.GetRoomsOutput getRoomsOutput = await this.client.Room.GetRooms();
        foreach (Transform child in RoomList.transform)
        {
            Destroy(child.gameObject);
        }

        foreach (byte[] roomNameBytes in getRoomsOutput.RoomNames)
        {
            string roomName = Encoding.UTF8.GetString(roomNameBytes);
            GameObject roomListItemGo = Instantiate(RoomListItemPrefab, RoomList.transform);
            RoomListItem roomListItem = roomListItemGo.GetComponent<RoomListItem>();
            roomListItem.ButtonText.text = roomName;
            roomListItem.Button.onClick.AddListener(() =>
            {
                Debug.Log("Join " + roomName);
            });
        }
    }

    public async void CreateRoomClickHandler()
    {
        await this.client.ConnectToContract();
        await this.client.Room.CreateRoom(CreateRoomRoomNameField.text);
        CreateRoomRoomNameField.text = "";
    }
}
