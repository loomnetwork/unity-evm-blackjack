var TestBlackJack = artifacts.require("./TestBlackJack.sol");

function hexToAscii(hex) {
    var str = "";
    var i = 0,
        l = hex.length;
    if (hex.substring(0, 2) === "0x") {
        i = 2;
    }
    for (; i < l; i += 2) {
        var code = parseInt(hex.substr(i, 2), 16);
        if (code != 0) {
            str += String.fromCharCode(code);
        }
    }

    return str;
}

async function getLastEventArgs(event) {
    return new Promise(function(resolve, reject) {
        event.get((error, logs) => {
            if (!error) {
                resolve(logs[logs.length - 1].args);
            } else {
                reject(Error());
            }
        });
    });
}

contract("TestBlackJack", function(accounts) {
    let contract;

    async function startTestGame(deck) {
        await contract.createRoom("test room1");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;
    
        await contract.roomJoin(roomId, {from: accounts[1]});
        await contract.placeBet(roomId, 100, {from: accounts[1]});
    
        await contract.setNextFakeRandomNumbers(deck);
        await contract.startGame(roomId);
    
        return roomId;
    }

    beforeEach("Deploy contract", async function() {
        contract = await TestBlackJack.new();
    });

    it("should create rooms", async function() {
        return;
        await contract.createRoom("test room1");
        await contract.createRoom("test room2");
        let rooms = await contract.getRooms();
        assert.equal(hexToAscii(rooms[1][0]), "test room1");
        assert.equal(hexToAscii(rooms[1][1]), "test room2");
        /*         
        let event = contract.allEvents();
         event.watch((error, log) => {
            // Do whatever you want
            if (!error) {
                console.log("Watched Log:", log);
            }
        });  */

        /*           event.get((error, logs) => {
            if (!error) {
                console.log("count: " + logs.length);
                logs.forEach(log => console.log(log.args))
            }
        });
  */
    });

    it("should calculate cards", async function() {
        return;
        assert.equal((await contract.getCardValue(5)).toNumber(), 5);
        assert.equal((await contract.getCardValue(14)).toNumber(), 1);
        assert.equal((await contract.getCardValue(51)).toNumber(), 12);

        assert.equal((await contract.getCardSuit(5)).toNumber(), 0);
        assert.equal((await contract.getCardSuit(14)).toNumber(), 1);
        assert.equal((await contract.getCardSuit(51)).toNumber(), 3);
    });



    it("should play test game", async function() {
        let roomId = await startTestGame([1, 2, 3, 4, 5, 6, 7, 8]);

        var player1State = await contract.getGameStatePlayer(roomId, accounts[1]);
        var dealerState = await contract.getGameStatePlayer(roomId, accounts[0]);

        assert.deepEqual(player1State[0].map(x => x.toNumber()), [1, 3]);
        assert.deepEqual(dealerState[0].map(x => x.toNumber()), [2]);

        await contract.playerDecision(roomId, 0, {from: accounts[1]});
        player1State = await contract.getGameStatePlayer(roomId, accounts[1]);
        dealerState = await contract.getGameStatePlayer(roomId, accounts[0]);
        assert.deepEqual(player1State[0].map(x => x.toNumber()), [1, 3]);
        assert.deepEqual(dealerState[0].map(x => x.toNumber()), [2, 4, 5]);
    });

    it("should play test game 2", async function() {
        let roomId = await startTestGame([1, 2, 3, 4, 5, 6, 7, 8]);

        var player1State = await contract.getGameStatePlayer(roomId, accounts[1]);
        var dealerState = await contract.getGameStatePlayer(roomId, accounts[0]);

        assert.deepEqual(player1State[0].map(x => x.toNumber()), [1, 3]);
        assert.deepEqual(dealerState[0].map(x => x.toNumber()), [2]);

        await contract.playerDecision(roomId, 1, {from: accounts[1]});
        await contract.playerDecision(roomId, 0, {from: accounts[1]});

        player1State = await contract.getGameStatePlayer(roomId, accounts[1]);
        dealerState = await contract.getGameStatePlayer(roomId, accounts[0]);
        assert.deepEqual(player1State[0].map(x => x.toNumber()), [1, 3, 4]); 
        assert.deepEqual(dealerState[0].map(x => x.toNumber()), [2, 5, 6]);
    });
});
