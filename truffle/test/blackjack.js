let tryCatch = require("./exceptions.js").tryCatch;
let errTypes = require("./exceptions.js").errTypes;

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

var TestingBlackjack = artifacts.require("./TestingBlackjack.sol");

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

contract("TestingBlackjack", function(accounts) {
    let dealerAddress = accounts[0];
    let player1Address = accounts[1];
    let player2Address = accounts[2];
    let player3Address = accounts[3];
    let contract;

    var state = {};

    beforeEach("Deploy contract", async function() {
        //contract = await TestingBlackjack.new();
        //return;
        contract = await TestingBlackjack.deployed();
        await contract.resetBalances(accounts);
        state = {};
    });

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

    async function updateThreePlayerState(roomId) {
        await updateSingleState(roomId, "dealer", dealerAddress);
        await updateSingleState(roomId, "player1", player1Address);
        await updateSingleState(roomId, "player2", player2Address);
        await updateSingleState(roomId, "player3", player3Address);
    }

    async function startTestGame(deck, beforeStart, afterStart) {
        //console.log("Deck: " + deck);
        await contract.createRoom("test room1");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await beforeStart(roomId);

        await contract.setNextFakeRandomNumbers(deck);
        await contract.startGame(roomId);

        await afterStart(roomId);

        return roomId;
    }

    async function startTestOnePlayerGame(deck) {
        return startTestGame(
            deck,
            async roomId => {
                await contract.joinRoom(roomId, { from: player1Address });
                await contract.placeBet(roomId, 100, { from: player1Address });
            },
            async roomId => {
                await updateOnePlayerState(roomId);
            }
        );
    }

    async function startTestTwoPlayerGame(deck) {
        return startTestGame(
            deck,
            async roomId => {
                await contract.joinRoom(roomId, { from: player1Address });
                await contract.placeBet(roomId, 100, { from: player1Address });

                await contract.joinRoom(roomId, { from: player2Address });
                await contract.placeBet(roomId, 500, { from: player2Address });
            },
            async roomId => {
                await updateTwoPlayerState(roomId);
            }
        );
    }

    async function startTestThreePlayerGame(deck) {
        return startTestGame(
            deck,
            async roomId => {
                await contract.joinRoom(roomId, { from: player1Address });
                await contract.placeBet(roomId, 100, { from: player1Address });

                await contract.joinRoom(roomId, { from: player2Address });
                await contract.placeBet(roomId, 500, { from: player2Address });

                await contract.joinRoom(roomId, { from: player3Address });
                await contract.placeBet(roomId, 700, { from: player3Address });
            },
            async roomId => {
                await updateThreePlayerState(roomId);
            }
        );
    }

    it("all players win if dealer leaves mid-game", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });

        await contract.joinRoom(roomId, { from: player2Address });
        await contract.placeBet(roomId, 500, { from: player2Address });

        await contract.setNextFakeRandomNumbers(Array(15).fill(CardValue.Six));
        await contract.startGame(roomId);

        assert.equal((await contract.getBalance(dealerAddress)).toNumber(), 0);
        assert.equal((await contract.getBalance(player1Address)).toNumber(), -100);
        assert.equal((await contract.getBalance(player2Address)).toNumber(), -500);

        await contract.leaveRoom(roomId);

        assert.equal((await contract.getBalance(dealerAddress)).toNumber(), -100 - 500);
        assert.equal((await contract.getBalance(player1Address)).toNumber(), 100);
        assert.equal((await contract.getBalance(player2Address)).toNumber(), 500);
    });

    it("should create rooms", async function() {
        contract = await TestingBlackjack.new();
        await contract.createRoom("test room1");
        await contract.createRoom("test room2");
        let rooms = await contract.getRooms();
        assert.equal(hexToAscii(rooms[1][0]), "test room1");
        assert.equal(hexToAscii(rooms[1][1]), "test room2");
    });

    it("should join correctly", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        var players = await contract.getRoomPlayers(roomId);
        assert.deepEqual(players, [dealerAddress]);

        await contract.joinRoom(roomId, { from: player1Address });
        players = await contract.getRoomPlayers(roomId);
        assert.deepEqual(players, [dealerAddress, player1Address]);

        await contract.joinRoom(roomId, { from: player2Address });
        players = await contract.getRoomPlayers(roomId);
        assert.deepEqual(players, [dealerAddress, player1Address, player2Address]);
    });

    it("dealer can not join the room he created as a player", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        var players = await contract.getRoomPlayers(roomId);
        assert.deepEqual(players, [dealerAddress]);

        await contract.joinRoom(roomId, { from: player1Address });
        players = await contract.getRoomPlayers(roomId);
        assert.deepEqual(players, [dealerAddress, player1Address]);

        await contract.joinRoom(roomId, { from: player2Address });
        players = await contract.getRoomPlayers(roomId);
        assert.deepEqual(players, [dealerAddress, player1Address, player2Address]);
    });

    it("player can not double-leave", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        var players = await contract.getRoomPlayers(roomId);
        assert.deepEqual(players, [dealerAddress, player1Address]);

        await contract.leaveRoom(roomId, { from: player1Address });
        players = await contract.getRoomPlayers(roomId);
        assert.deepEqual(players, [dealerAddress]);

        await tryCatch(contract.leaveRoom(roomId, { from: player1Address }), errTypes.revert);
    });

    it("refund player if he leaves at WaitingForPlayersAndBetting", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });

        await contract.joinRoom(roomId, { from: player2Address });
        await contract.placeBet(roomId, 500, { from: player2Address });

        assert.equal((await contract.getBalance(player1Address)).toNumber(), -100);
        assert.equal((await contract.getBalance(player2Address)).toNumber(), -500);

        await contract.leaveRoom(roomId, { from: player1Address });

        assert.equal((await contract.getBalance(player1Address)).toNumber(), 0);
        assert.equal((await contract.getBalance(player2Address)).toNumber(), -500);

        await contract.leaveRoom(roomId, { from: player2Address });

        assert.equal((await contract.getBalance(player1Address)).toNumber(), 0);
        assert.equal((await contract.getBalance(player2Address)).toNumber(), 0);
    });

    it("refund all players if dealer leaves at WaitingForPlayersAndBetting", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });

        await contract.joinRoom(roomId, { from: player2Address });
        await contract.placeBet(roomId, 500, { from: player2Address });

        assert.equal((await contract.getBalance(player1Address)).toNumber(), -100);
        assert.equal((await contract.getBalance(player2Address)).toNumber(), -500);

        await contract.leaveRoom(roomId);

        assert.equal((await contract.getBalance(player1Address)).toNumber(), 0);
        assert.equal((await contract.getBalance(player2Address)).toNumber(), 0);
    });

    it("when player leaves mid-game, refund all players except the one who left mid-game", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });

        await contract.joinRoom(roomId, { from: player2Address });
        await contract.placeBet(roomId, 500, { from: player2Address });

        await contract.joinRoom(roomId, { from: player3Address });
        await contract.placeBet(roomId, 700, { from: player3Address });

        await contract.setNextFakeRandomNumbers(Array(15).fill(CardValue.Six));
        await contract.startGame(roomId);

        assert.equal((await contract.getBalance(dealerAddress)).toNumber(), 0);
        assert.equal((await contract.getBalance(player1Address)).toNumber(), -100);
        assert.equal((await contract.getBalance(player2Address)).toNumber(), -500);
        assert.equal((await contract.getBalance(player3Address)).toNumber(), -700);

        await contract.leaveRoom(roomId, { from: player2Address });

        assert.equal((await contract.getBalance(dealerAddress)).toNumber(), 500);
        assert.equal((await contract.getBalance(player1Address)).toNumber(), 0);
        assert.equal((await contract.getBalance(player2Address)).toNumber(), -500);
        assert.equal((await contract.getBalance(player3Address)).toNumber(), 0);
    });

    it("when player leaves mid-game, room and game must be destroyed", async function() {
        let roomId = await startTestOnePlayerGame(Array(15).fill(CardValue.Six));
        await contract.getGameState(roomId);
        await contract.leaveRoom(roomId, { from: player1Address });

        await tryCatch(contract.getGameState(roomId), errTypes.revert);
    });

    it("dealer can leave room after round has ended", async function() {
        let roomId = await startTestOnePlayerGame([
            CardValue.Five,
            CardValue.Four,
            CardValue.Jack,
            CardValue.Five,
            CardValue.Seven,
            CardValue.Six
        ]);

        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Five, CardValue.Jack]);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player1Address });
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });
        await updateOnePlayerState(roomId);

        assert.equal((await contract.getBalance(dealerAddress)).toNumber(), -100);
        assert.equal((await contract.getBalance(player1Address)).toNumber(), 100);

        await contract.leaveRoom(roomId, { from: dealerAddress });

        assert.equal((await contract.getBalance(dealerAddress)).toNumber(), -100);
        assert.equal((await contract.getBalance(player1Address)).toNumber(), 100);
    });

    it("player can leave room after round has ended", async function() {
        let roomId = await startTestOnePlayerGame([
            CardValue.Five,
            CardValue.Four,
            CardValue.Jack,
            CardValue.Five,
            CardValue.Seven,
            CardValue.Six
        ]);

        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Five, CardValue.Jack]);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player1Address });
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });
        await updateOnePlayerState(roomId);

        assert.equal((await contract.getBalance(dealerAddress)).toNumber(), -100);
        assert.equal((await contract.getBalance(player1Address)).toNumber(), 100);

        await contract.leaveRoom(roomId, { from: player1Address });

        assert.equal((await contract.getBalance(dealerAddress)).toNumber(), -100);
        assert.equal((await contract.getBalance(player1Address)).toNumber(), 100);
    });

    it("when game starts, room becomes non-discoverable, then discoverable again when round ends", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });

        await contract.setNextFakeRandomNumbers([
            CardValue.Ten,
            CardValue.Ace,
            CardValue.Jack,
            CardValue.Queen
        ]);

        var rooms = await contract.getRooms();
        assert.notEqual(rooms[0].map(x => x.toNumber()).indexOf(roomId.toNumber()), -1);

        await contract.startGame(roomId);

        rooms = await contract.getRooms();
        assert.equal(rooms[0].map(x => x.toNumber()).indexOf(roomId.toNumber()), -1);

        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });

        rooms = await contract.getRooms();
        assert.notEqual(rooms[0].map(x => x.toNumber()).indexOf(roomId.toNumber()), -1);
    });

    it("can start next round", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });
        await contract.setNextFakeRandomNumbers([
            CardValue.Ten,
            CardValue.Six,
            CardValue.Ace,
            CardValue.Queen,
            CardValue.Queen,
            CardValue.Queen
        ]);

        await contract.startGame(roomId);

        await updateOnePlayerState(roomId);
        assert.equal(state.dealer.balance, -150);
        assert.equal(state.player1.balance, 150);

        // Starting next round must fail until all players are ready
        await tryCatch(contract.nextRound(roomId), errTypes.revert);
        await contract.setPlayerReadyForNextRound(roomId, true, { from: player1Address });

        await contract.nextRound(roomId);

        await contract.placeBet(roomId, 100, { from: player1Address });

        await contract.setNextFakeRandomNumbers([
            CardValue.Ten,
            CardValue.Six,
            CardValue.Ace,
            CardValue.Queen,
            CardValue.Queen,
            CardValue.Queen
        ]);

        await contract.startGame(roomId);

        await updateOnePlayerState(roomId);
        assert.equal(state.dealer.balance, -300);
        assert.equal(state.player1.balance, 300);
    });

    it("should not double-join", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        var gameState = await contract.getGameState(roomId);
        assert.equal(gameState[3].length, 1);

        await contract.joinRoom(roomId, { from: player1Address });
        var gameState = await contract.getGameState(roomId);
        assert.equal(gameState[3].length, 1);

        await contract.joinRoom(roomId, { from: dealerAddress });
        var gameState = await contract.getGameState(roomId);
        assert.equal(gameState[3].length, 1);
    });

    it("should not double-bet", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });

        await tryCatch(contract.placeBet(roomId, 100, { from: player1Address }), errTypes.revert);
        await tryCatch(contract.placeBet(roomId, 100, { from: player1Address }), errTypes.revert);
    });

    it("dealer can leave room", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.leaveRoom(roomId);
    });

    it("player can leave room", async function() {
        await contract.createRoom("test room");
        let roomCreatedEvent = await getLastEventArgs(contract.RoomCreated());
        let roomId = roomCreatedEvent.roomId;

        await contract.joinRoom(roomId, { from: player1Address });
        await contract.placeBet(roomId, 100, { from: player1Address });

        await contract.leaveRoom(roomId, { from: player1Address });
    });


    it("1 player - player wins", async function() {
        let roomId = await startTestOnePlayerGame([
            CardValue.Five,
            CardValue.Four,
            CardValue.Jack,
            CardValue.Five,
            CardValue.Seven,
            CardValue.Six
        ]);

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
        let roomId = await startTestOnePlayerGame([
            CardValue.Three,
            CardValue.Four,
            CardValue.Five,
            CardValue.Six,
            CardValue.Seven,
            CardValue.Eight
        ]);

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
        let roomId = await startTestOnePlayerGame([
            CardValue.Jack,
            CardValue.Jack,
            CardValue.Jack,
            CardValue.Jack,
            CardValue.Jack
        ]);

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
        let roomId = await startTestOnePlayerGame([
            CardValue.Three,
            CardValue.Four,
            CardValue.Jack,
            CardValue.Queen,
            CardValue.Seven,
            CardValue.Eight
        ]);

        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Three, CardValue.Jack]);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player1Address });
        await updateOnePlayerState(roomId);

        // If player busts, dealer must not do his move
        assert.deepEqual(state.dealer.hand, [CardValue.Four]);
        assert.deepEqual(state.player1.hand, [CardValue.Three, CardValue.Jack, CardValue.Queen]);

        assert.equal(state.dealer.balance, 100);
        assert.equal(state.player1.balance, -100);

        throwError();
    });

    it("1 player - dealer busts", async function() {
        let roomId = await startTestOnePlayerGame([
            CardValue.Jack,
            CardValue.Four,
            CardValue.King,
            CardValue.Jack,
            CardValue.Queen
        ]);

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
        let roomId = await startTestOnePlayerGame([
            CardValue.Ace,
            CardValue.Four,
            CardValue.Jack,
            CardValue.Six,
            CardValue.Seven
        ]);

        assert.deepEqual(state.dealer.hand, [CardValue.Four, CardValue.Six, CardValue.Seven]);
        assert.deepEqual(state.player1.hand, [CardValue.Ace, CardValue.Jack]);

        assert.equal(state.dealer.balance, -150);
        assert.equal(state.player1.balance, 150);

        throwError();
    });

    it("1 player - dealer has natural", async function() {
        let roomId = await startTestOnePlayerGame([
            CardValue.Ten,
            CardValue.Ace,
            CardValue.Jack,
            CardValue.Queen
        ]);
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });

        await updateOnePlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Ace, CardValue.Queen]);
        assert.deepEqual(state.player1.hand, [CardValue.Ten, CardValue.Jack]);

        assert.equal(state.dealer.balance, 100);
        assert.equal(state.player1.balance, -100);

        throwError();
    });

    it("1 player - dealer and player have natural", async function() {
        let roomId = await startTestOnePlayerGame([
            CardValue.Ace,
            CardValue.Ace,
            CardValue.Jack,
            CardValue.Queen
        ]);

        assert.deepEqual(state.dealer.hand, [CardValue.Ace, CardValue.Queen]);
        assert.deepEqual(state.player1.hand, [CardValue.Ace, CardValue.Jack]);

        assert.equal(state.dealer.balance, 0);
        assert.equal(state.player1.balance, 0);

        throwError();
    });

    it("1 player - dealer has blackjack, player has 21", async function() {
        let roomId = await startTestOnePlayerGame([
            CardValue.Jack,
            CardValue.Queen,
            CardValue.Five,
            CardValue.Six,
            CardValue.Ace
        ]);

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
        let roomId = await startTestTwoPlayerGame([
            CardValue.Jack,
            CardValue.Four,
            CardValue.Four,
            CardValue.Queen,
            CardValue.Seven,
            CardValue.Five,
            CardValue.Four,
            CardValue.Nine
        ]);

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
        let roomId = await startTestTwoPlayerGame([
            CardValue.Jack,
            CardValue.Four,
            CardValue.Four,
            CardValue.Four,
            CardValue.Eight,
            CardValue.Nine,
            CardValue.Six,
            CardValue.Two,
            CardValue.Nine,
            CardValue.Five
        ]);

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
        assert.deepEqual(state.player2.hand, [
            CardValue.Four,
            CardValue.Eight,
            CardValue.Six,
            CardValue.Two
        ]);

        assert.equal(state.dealer.score, 18);
        assert.equal(state.player1.score, 23);
        assert.equal(state.player2.score, 20);

        assert.equal(state.dealer.balance, 100 - 500);
        assert.equal(state.player1.balance, -100);
        assert.equal(state.player2.balance, 500);

        throwError();
    });

    it("2 player - dealer busts, player1 busts, player2 wins", async function() {
        let roomId = await startTestTwoPlayerGame([
            CardValue.Jack,
            CardValue.Four,
            CardValue.Jack,
            CardValue.Four,
            CardValue.Eight,
            CardValue.Nine,
            CardValue.Six,
            CardValue.Two,
            CardValue.Five,
            CardValue.Nine
        ]);

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
        assert.deepEqual(state.player2.hand, [
            CardValue.Four,
            CardValue.Eight,
            CardValue.Six,
            CardValue.Two
        ]);

        assert.equal(state.dealer.score, 24);
        assert.equal(state.player1.score, 23);
        assert.equal(state.player2.score, 20);

        assert.equal(state.dealer.balance, 100 - 500);
        assert.equal(state.player1.balance, -100);
        assert.equal(state.player2.balance, 500);

        throwError();
    });

    it("2 player - dealer has blackjack", async function() {
        let roomId = await startTestTwoPlayerGame([
            // player 1
            CardValue.Jack,
            // player 2
            CardValue.Four,
            // dealer
            CardValue.Jack,
            // player 1
            CardValue.Four,
            // player 2
            CardValue.Eight,
            // player 1
            CardValue.Nine,
            // player 2
            CardValue.Six,
            CardValue.Two,
            // dealer
            CardValue.Ace
        ]);

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

        assert.deepEqual(state.dealer.hand, [CardValue.Jack, CardValue.Ace]);
        assert.deepEqual(state.player1.hand, [CardValue.Jack, CardValue.Four, CardValue.Nine]);
        assert.deepEqual(state.player2.hand, [
            CardValue.Four,
            CardValue.Eight,
            CardValue.Six,
            CardValue.Two
        ]);

        assert.equal(state.dealer.score, 21);
        assert.equal(state.player1.score, 23);
        assert.equal(state.player2.score, 20);

        assert.equal(state.dealer.balance, 100 + 500);
        assert.equal(state.player1.balance, -100);
        assert.equal(state.player2.balance, -500);

        throwError();
    });

    it("2 player - dealer has blackjack, player 1 has blackjack, player 2 loses", async function() {
        let roomId = await startTestTwoPlayerGame([
            // player 1
            CardValue.Ace,
            // player 2
            CardValue.Four,
            // dealer
            CardValue.Jack,
            // player 1
            CardValue.Queen,
            // player 2
            CardValue.Eight,
            // player 2
            CardValue.Six,
            CardValue.Two,
            // dealer
            CardValue.Ace
        ]);

        assert.deepEqual(state.dealer.hand, [CardValue.Jack]);
        assert.deepEqual(state.player1.hand, [CardValue.Ace, CardValue.Queen]);
        assert.deepEqual(state.player2.hand, [CardValue.Four, CardValue.Eight]);

        assert.equal(state.dealer.score, 10);
        assert.equal(state.player1.score, 21);
        assert.equal(state.player2.score, 12);

        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player2Address });
        await contract.playerDecision(roomId, PlayerDecision.Hit, { from: player2Address });
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player2Address });

        await updateTwoPlayerState(roomId);

        assert.deepEqual(state.dealer.hand, [CardValue.Jack, CardValue.Ace]);
        assert.deepEqual(state.player1.hand, [CardValue.Ace, CardValue.Queen]);
        assert.deepEqual(state.player2.hand, [
            CardValue.Four,
            CardValue.Eight,
            CardValue.Six,
            CardValue.Two
        ]);

        assert.equal(state.dealer.score, 21);
        assert.equal(state.player1.score, 21);
        assert.equal(state.player2.score, 20);

        assert.equal(state.dealer.balance, 500);
        assert.equal(state.player1.balance, 0);
        assert.equal(state.player2.balance, -500);

        throwError();
    });

    it("3 player - player 1 has blackjack, he can't make a move", async function() {
        let roomId = await startTestThreePlayerGame([
            // player 1
            CardValue.Ace,
            // player 2
            CardValue.Four,
            // player 3
            CardValue.Four,
            // dealer
            CardValue.Four,
            // player 1
            CardValue.Jack,
            // player 2
            CardValue.Four,
            // player 3
            CardValue.Four
        ]);

        assert.equal(state.dealer.score, 4);
        assert.equal(state.player1.score, 21);
        assert.equal(state.player2.score, 8);
        assert.equal(state.player3.score, 8);

        let currentPlayerIndexChangedEvent = await getLastEventArgs(
            contract.CurrentPlayerIndexChanged()
        );

        assert.equal(currentPlayerIndexChangedEvent.playerIndex.toNumber(), 1);
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player2Address });

        currentPlayerIndexChangedEvent = await getLastEventArgs(
            contract.CurrentPlayerIndexChanged()
        );
        assert.equal(currentPlayerIndexChangedEvent.playerIndex.toNumber(), 2);
    });

    it("3 player - player 2 has blackjack, he can't make a move", async function() {
        let roomId = await startTestThreePlayerGame([
            // player 1
            CardValue.Four,
            // player 2
            CardValue.Ace,
            // player 3
            CardValue.Four,
            // dealer
            CardValue.Four,
            // player 1
            CardValue.Four,
            // player 2
            CardValue.Jack,
            // player 3
            CardValue.Four
        ]);

        assert.equal(state.dealer.score, 4);
        assert.equal(state.player1.score, 8);
        assert.equal(state.player2.score, 21);
        assert.equal(state.player3.score, 8);

        let currentPlayerIndexChangedEvent = await getLastEventArgs(
            contract.CurrentPlayerIndexChanged()
        );

        assert.equal(currentPlayerIndexChangedEvent.playerIndex.toNumber(), 0);
        await contract.playerDecision(roomId, PlayerDecision.Stand, { from: player1Address });

        currentPlayerIndexChangedEvent = await getLastEventArgs(
            contract.CurrentPlayerIndexChanged()
        );
        assert.equal(currentPlayerIndexChangedEvent.playerIndex.toNumber(), 2);
    });

    it("3 player - all players have blackjack", async function() {
        let roomId = await startTestThreePlayerGame([
            // player 1
            CardValue.Ace,
            // player 2
            CardValue.Ace,
            // player 3
            CardValue.Ace,
            // dealer
            CardValue.Four,
            // player 1
            CardValue.Jack,
            // player 2
            CardValue.Jack,
            // player 3
            CardValue.Jack,
            // dealer
            CardValue.Four,
            CardValue.Four,
            CardValue.Four,
            CardValue.Four,
            CardValue.Four,
        ]);

        assert.equal(state.dealer.score, 20);
        assert.equal(state.player1.score, 21);
        assert.equal(state.player2.score, 21);
        assert.equal(state.player3.score, 21);

        let gameStageChangedEvent = await getLastEventArgs(
            contract.GameStageChanged()
        );

        assert.equal(gameStageChangedEvent.stage.toNumber(), 4);
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
