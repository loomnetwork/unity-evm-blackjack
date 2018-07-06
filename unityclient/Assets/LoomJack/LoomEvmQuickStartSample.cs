using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Loom.Unity3d;
using Loom.Nethereum.ABI.FunctionEncoding.Attributes;
using Org.BouncyCastle.Math;

public class LoomEvmQuickStartSample : MonoBehaviour
{
    public TextAsset ABI;

    public class CharacterCreatedEvent {
        [Parameter("address", "Creator", 1, false)]
        public string Creator { get; set; }

        [Parameter("uint256", "roomId", 2, false)]
        public BigInteger RoomId { get; set; }
    }

    [FunctionOutput]
    public class CharacterCreatedEvent1 {
        [Parameter("address", "Creator", 1, false)]
        public string Creator { get; set; }

        [Parameter("uint256", "roomId", 2, false)]
        public BigInteger RoomId { get; set; }
    }

    [FunctionOutput]
    public class getGameStateDeckOutput
    {
        [Parameter("uint8[312]", "deck", 1, false)]
        public List<byte> Deck { get; set; }
    }

    private void test()
    {
        byte[] byteBeef = { 0x1, 0x1, 0x1, 0x1 };
        byteBeef = new byte[]{ 0xDE, 0xAD, 0xBE, 0xEF };
        object bigInt = Assembly.Load("System.Numerics").GetType("System.Numerics.BigInteger").GetConstructor(new Type[] { typeof(byte[]) }).Invoke( new object[] { byteBeef });
        Debug.Log(bigInt.ToString());

        BigInteger bigInteger1 = new BigInteger(1, byteBeef.Reverse().ToArray());
        BigInteger bigInteger2 = new BigInteger(1, byteBeef);
        Debug.Log(new BigInteger(0, byteBeef.Reverse().ToArray()));
    }

    async void Start()
    {
        // The private key is used to sign transactions sent to the DAppChain.
        // Usually you'd generate one private key per player, or let them provide their own.
        // In this sample we just generate a new key every time.
        var privateKey = CryptoUtils.GeneratePrivateKey();
        var publicKey = CryptoUtils.PublicKeyFromPrivateKey(privateKey);
        this.test();
        // Connect to the contract
        var contract = await GetContract(privateKey, publicKey);
        // This should print something like: "hello 6475" in the Unity console window if some data is already stored
        //await StaticCallContract(contract);
        // Listen for events
        contract.EventReceived += this.ContractEventReceived;
        // Store the string in a contract



        byte[] byteBeef = { 0xDE, 0xAD, 0xBE, 0xEF };
        byte[] bytes4 = { 1, 2, 3, 4 };
        byte[] bytes32 = new byte[32];
        Array.Copy(bytes4, bytes32, bytes4.Length);

        string testAddress = "0x1d655354f10499ef1e32e5a4e8b712606af33628";

        await contract.CallAsync("setTestUint", BigInteger.ValueOf(123456789));
        Debug.LogError((await contract.StaticCallSimpleTypeOutputAsync<BigInteger>("getTestUint")).LongValue == 123456789);
        Debug.LogError((await contract.StaticCallSimpleTypeOutputAsync<BigInteger>("getStaticTestUint")).LongValue == 0xDEADBEEF);

        Debug.LogError("------");

        await contract.CallAsync("setTestFixedByteArray", bytes4);
        Debug.LogError((await contract.StaticCallSimpleTypeOutputAsync<byte[]>("getTestFixedByteArray")).SequenceEqual(bytes4));
        byte[] getStaticTestFixedByteArray = await contract.StaticCallSimpleTypeOutputAsync<byte[]>("getStaticTestFixedByteArray");
        Debug.LogError(new BigInteger(1, getStaticTestFixedByteArray).LongValue == 0xDEADBEEF);

        Debug.LogError("------");

        await contract.CallAsync("setTestFixed32ByteArray", bytes32);
        Debug.LogError((await contract.StaticCallSimpleTypeOutputAsync<byte[]>("getTestFixed32ByteArray")).SequenceEqual(bytes32));
        Debug.LogError(new BigInteger(1, await contract.StaticCallSimpleTypeOutputAsync<byte[]>("getStaticTestFixed32ByteArray")).LongValue == 0xDEADBEEF);

        Debug.LogError("------");

        await contract.CallAsync("setTestByteArray", bytes4);
        Debug.LogError((await contract.StaticCallSimpleTypeOutputAsync<byte[]>("getTestByteArray")).SequenceEqual(bytes4));
        Debug.LogError((await contract.StaticCallSimpleTypeOutputAsync<byte[]>("getStaticTestByteArray")).SequenceEqual(bytes4));


        Debug.LogError("------");

        await contract.CallAsync("setTestAddress", testAddress);
        string retAddress = await contract.StaticCallSimpleTypeOutputAsync<string>("getTestAddress");
        Debug.LogError(retAddress);
        Debug.LogError(retAddress == testAddress);
        retAddress = await contract.StaticCallSimpleTypeOutputAsync<string>("getStaticTestAddress");
        Debug.LogError(retAddress);
        Debug.LogError(retAddress == testAddress);



        //Debug.Log(CryptoUtils.BytesToHexString(BitConverter.GetBytes(res.LongValue)));
        /*await contract.CallAsync("createRoom", Encoding.UTF8.GetBytes("hello " + UnityEngine.Random.Range(0, 10000)));
        getGameStateDeckOutput getGameStateDeckOutput = await contract.StaticCallDTOTypeOutputAsync<getGameStateDeckOutput>("getGameStateDeck", 1);
        Debug.Log(getGameStateDeckOutput.Deck.Count);
        Debug.Log(getGameStateDeckOutput.Deck[0]);
        Debug.Log(getGameStateDeckOutput.Deck[1]);
        Debug.Log(getGameStateDeckOutput.Deck[2]);
        Debug.Log(getGameStateDeckOutput.Deck[3]);*/
        //await CallContract(contract);
    }

    private void ContractEventReceived(object sender, EvmChainEventArgs e)
    {
        Debug.LogFormat("Received smart contract event: " + e.EventName);
        if (e.EventName == "RoomCreated")
        {
            CharacterCreatedEvent evt = e.DecodeEventDTO<CharacterCreatedEvent>();
            Debug.LogFormat("RoomCreated: {0}, {1}", evt.Creator, evt.RoomId);
        }
    }

    async Task<EvmContract> GetContract(byte[] privateKey, byte[] publicKey)
    {
        var writer = RPCClientFactory.Configure()
            .WithLogger(Debug.unityLogger)
            .WithWebSocket("ws://127.0.0.1:46657/websocket")
            .Create();

        var reader = RPCClientFactory.Configure()
            .WithLogger(Debug.unityLogger)
            .WithWebSocket("ws://127.0.0.1:9999/queryws")
            .Create();

        var client = new DAppChainClient(writer, reader)
            { Logger = Debug.unityLogger };

        // required middleware
        client.TxMiddleware = new TxMiddleware(new ITxMiddlewareHandler[]
        {
            new NonceTxMiddleware
            {
                PublicKey = publicKey,
                Client = client
            },
            new SignedTxMiddleware(privateKey)
        });

        var contractAddr = await client.ResolveContractAddressAsync("LoomJack1");
        var callerAddr = Address.FromPublicKey(publicKey);

        return new EvmContract(client, contractAddr, callerAddr, this.ABI.text);
    }

}