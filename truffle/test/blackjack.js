const PlayerDecision = Object.freeze({
    Stand: 0,
    Hit: 1
});

const CardValue = Object.freeze({
    Two: 0,
    Three: 1,
    Four: 2,
    Five: 3,
    Six: 4,
    Seven: 5,
    Eight: 6,
    Nine: 7,
    Ten: 8,
    Jack: 9,
    Queen: 10,
    King: 11,
    Ace: 12
});

const CardSuit = Object.freeze({
    Clubs: 0,
    Diamonds: 1,
    Hearts: 2,
    Spades: 3
});

var TestingBlackJack = artifacts.require("./TestingBlackJack.sol");

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

contract("TestingBlackJack", function(accounts) {
    let dealerAddress = accounts[0];
    let player1Address = accounts[1];
    let player2Address = accounts[2];
    let contract;

    let state = {
    }

    function throwError() {
        //throw Error();
    }

    async function updateSingleState(roomId, x, address) {
        state[x] = await contract.getGameStatePlayer(roomId, address);
        state[x][0] = state[x][0].map(x => x.toNumber());
        state[x].hand = state[x][0];
        state[x].balance = (await contract.getBalance(address)).toNumber();
        state[x].score = (await contract.calculateHandScore(state[x][0])).toNumber();
    }

    async function updateOnePlayerState(roomId) {
        await updateSingleState(roomId, "dealer", dealerAddress);
        await updateSingleState(roomId, "player1", player1Address);
    }

    async function updateTwoPlayerState(roomId) {
        await updateSingleState(roomId, "dealer", dealerAddress);
        await updateSingleState(roomId, "player1", player1Address);
        await updateSingleState(roomId, "player2", player2Address);
    }

    async function startTestOnePlayerGame(deck) {
        //console.log("Deck: " + deck);
        await contract.createRoom("test room1");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });

        await contract.setNextFakeRandomNumbers(deck);
        await contract.startGame(roomId);

        await updateOnePlayerState(roomId);

        return roomId;
    }

    async function startTestTwoPlayerGame(deck) {
        //console.log("Deck: " + deck);
        await contract.createRoom("test room2");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });

        await contract.joinRoom(roomId, { from: player2Address });
        await contract.placeBet(roomId, 500, { from: player2Address });

        await contract.setNextFakeRandomNumbers(deck);
        await contract.startGame(roomId);

        await updateTwoPlayerState(roomId);

        return roomId;
    }

    beforeEach("Deploy contract", async function() {
        contract = await TestingBlackJack.new();
    });

    it("should create rooms", async function() {
        await contract.createRoom("test room1");
        await contract.createRoom("test room2");
        let rooms = await contract.getRooms();
        assert.equal(hexToAscii(rooms[1][0]), "test room1");
        assert.equal(hexToAscii(rooms[1][1]), "test room2");
    });

    it("1 player - player wins", async function() {
        let roomId = await startTestOnePlayerGame([CardValue.Five, CardValue.Four, CardValue.Jack, CardValue.Five, CardValue.Seven, CardValue.Six]);

        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Five, CardValue.Jack]);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player1Address });
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });
        await updateOnePlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Four, CardValue.Seven, CardValue.Six]);
        assert.deepEqual(state.player1.hand, [CardValue.Five, CardValue.Jack, CardValue.Five]);

        assert.equal(state.dealer.balance, -100);
        assert.equal(state.player1.balance, 100);

        throwError();
    });

    it("1 player - dealer wins", async function() {
        let roomId = await startTestOnePlayerGame([CardValue.Three, CardValue.Four, CardValue.Five, CardValue.Six, CardValue.Seven, CardValue.Eight]);

        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Three, CardValue.Five]);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player1Address });
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });
        await updateOnePlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Four, CardValue.Seven, CardValue.Eight]);
        assert.deepEqual(state.player1.hand, [CardValue.Three, CardValue.Five, CardValue.Six]);

        assert.equal(state.dealer.balance, 100);
        assert.equal(state.player1.balance, -100);

        throwError();
    });

    it("1 player - tie", async function() {
        let roomId = await startTestOnePlayerGame([CardValue.Jack, CardValue.Jack, CardValue.Jack, CardValue.Jack, CardValue.Jack]);

        assert.deepEqual(state.dealer.hand, [CardValue.Jack]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Jack]);

        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });
        await updateOnePlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Jack, CardValue.Jack]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Jack]);

        assert.equal(state.dealer.balance, 0);
        assert.equal(state.player1.balance, 0);

        throwError();
    });

    it("1 player - player busts", async function() {
        let roomId = await startTestOnePlayerGame([CardValue.Three, CardValue.Four, CardValue.Jack, CardValue.Queen, CardValue.Seven, CardValue.Eight]);

        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Three, CardValue.Jack]);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player1Address });
        await updateOnePlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Four, CardValue.Seven, CardValue.Eight]);
        assert.deepEqual(state.player1.hand, [CardValue.Three, CardValue.Jack, CardValue.Queen]);

        assert.equal(state.dealer.balance, 100);
        assert.equal(state.player1.balance, -100);

        throwError();
    });

    it("1 player - dealer busts", async function() {
        let roomId = await startTestOnePlayerGame([CardValue.Jack, CardValue.Four, CardValue.King, CardValue.Jack, CardValue.Queen]);

        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.King]);

        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });
        await updateOnePlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Four, CardValue.Jack, CardValue.Queen]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.King]);

        assert.equal(state.dealer.balance, -100);
        assert.equal(state.player1.balance, 100);

        throwError();
    });

    it("1 player - player has natural", async function() {
        let roomId = await startTestOnePlayerGame([CardValue.Ace, CardValue.Four, CardValue.Jack, CardValue.Six, CardValue.Seven]);

        assert.deepEqual(state.dealer.hand, [CardValue.Four, CardValue.Six, CardValue.Seven]);
        assert.deepEqual(state.player1.hand, [CardValue.Ace, CardValue.Jack]);

        assert.equal(state.dealer.balance, -150);
        assert.equal(state.player1.balance, 150);
        
        throwError();
    });

    it("1 player - dealer has natural", async function() {
        let roomId = await startTestOnePlayerGame([CardValue.Ten, CardValue.Ace, CardValue.Jack, CardValue.Queen]);
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });

        await updateOnePlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Ace, CardValue.Queen]);
        assert.deepEqual(state.player1.hand, [CardValue.Ten, CardValue.Jack]);

        assert.equal(state.dealer.balance, 100);
        assert.equal(state.player1.balance, -100);

        throwError();
    });

    it("1 player - dealer and player have natural", async function() {
        let roomId = await startTestOnePlayerGame([CardValue.Ace, CardValue.Ace, CardValue.Jack, CardValue.Queen]);

        assert.deepEqual(state.dealer.hand, [CardValue.Ace, CardValue.Queen]);
        assert.deepEqual(state.player1.hand, [CardValue.Ace, CardValue.Jack]);

        assert.equal(state.dealer.balance, 0);
        assert.equal(state.player1.balance, 0);

        throwError();
    });

    it("1 player - dealer has blackjack, player has 21", async function() {
        let roomId = await startTestOnePlayerGame([CardValue.Jack, CardValue.Queen, CardValue.Five, CardValue.Six, CardValue.Ace]);

        assert.deepEqual(state.dealer.hand, [CardValue.Queen]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Five]);

        assert.equal(state.dealer.score, 10);
        assert.equal(state.player1.score, 15);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player1Address });
        await updateOnePlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Queen, CardValue.Ace]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Five, CardValue.Six]);

        assert.equal(state.dealer.score, 21);
        assert.equal(state.player1.score, 21);

        assert.equal(state.dealer.balance, 100);
        assert.equal(state.player1.balance, -100);

        throwError();
    });

    it("2 player - player1 wins, player2 loses", async function() {
        let roomId = await startTestTwoPlayerGame(
            [CardValue.Jack, CardValue.Four, CardValue.Four, CardValue.Queen, CardValue.Seven, CardValue.Five, CardValue.Four, CardValue.Nine]
        );

        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Queen]);
        assert.deepEqual(state.player2.hand, [CardValue.Four, CardValue.Seven]);

        assert.equal(state.dealer.score, 4);
        assert.equal(state.player1.score, 20);
        assert.equal(state.player2.score, 11);

        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });
        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player2Address });
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player2Address });
        await updateTwoPlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Four, CardValue.Four, CardValue.Nine]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Queen]);
        assert.deepEqual(state.player2.hand, [CardValue.Four, CardValue.Seven, CardValue.Five]);

        assert.equal(state.dealer.score, 17);
        assert.equal(state.player1.score, 20);
        assert.equal(state.player2.score, 16);

        assert.equal(state.dealer.balance, -100 + 500);
        assert.equal(state.player1.balance, 100); 
        assert.equal(state.player2.balance, -500);

        throwError();
    });

    it("2 player - player1 busts, player2 wins", async function() {
        let roomId = await startTestTwoPlayerGame(
            [CardValue.Jack, CardValue.Four, CardValue.Four, CardValue.Four, CardValue.Eight, CardValue.Nine, CardValue.Six, CardValue.Two, CardValue.Nine, CardValue.Five]
        );

        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Four]);
        assert.deepEqual(state.player2.hand, [CardValue.Four, CardValue.Eight]);

        assert.equal(state.dealer.score, 4);
        assert.equal(state.player1.score, 14);
        assert.equal(state.player2.score, 12);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player1Address });
        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player2Address });
        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player2Address });
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player2Address });
        await updateTwoPlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Four, CardValue.Nine, CardValue.Five]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Four, CardValue.Nine]);
        assert.deepEqual(state.player2.hand, [CardValue.Four, CardValue.Eight, CardValue.Six, CardValue.Two]);

        assert.equal(state.dealer.score, 18);
        assert.equal(state.player1.score, 23);
        assert.equal(state.player2.score, 20);

        assert.equal(state.dealer.balance, 100 - 500);
        assert.equal(state.player1.balance, -100); 
        assert.equal(state.player2.balance, 500);

        throwError();
    });

    it("2 player - dealer busts, player1 busts, player2 wins", async function() {
        let roomId = await startTestTwoPlayerGame(
            [CardValue.Jack, CardValue.Four, CardValue.Jack, CardValue.Four, CardValue.Eight, CardValue.Nine, CardValue.Six, CardValue.Two, CardValue.Five, CardValue.Nine]
        );

        assert.deepEqual(state.dealer.hand, [CardValue.Jack]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Four]);
        assert.deepEqual(state.player2.hand, [CardValue.Four, CardValue.Eight]);

        assert.equal(state.dealer.score, 10);
        assert.equal(state.player1.score, 14);
        assert.equal(state.player2.score, 12);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player1Address });
        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player2Address });
        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player2Address });
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player2Address });
        await updateTwoPlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Jack, CardValue.Five, CardValue.Nine]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Four, CardValue.Nine]);
        assert.deepEqual(state.player2.hand, [CardValue.Four, CardValue.Eight, CardValue.Six, CardValue.Two]);

        assert.equal(state.dealer.score, 24);
        assert.equal(state.player1.score, 23);
        assert.equal(state.player2.score, 20);

        assert.equal(state.dealer.balance, 100 - 500);
        assert.equal(state.player1.balance, -100); 
        assert.equal(state.player2.balance, 500);

        throwError();
    });

    it("event test", async function() {
        await contract.sendTestEvents(0);

        //throw Error();
    });

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
