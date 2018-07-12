pragma solidity ^0.4.24;

import "./Owned.sol";
import "./CircuitBreaker.sol";
import "./RandomProvider.sol";
import "./BalanceController.sol";
import "./RoomSupport.sol";
import "./libraries/ArrayLibrary.sol";
import "./libraries/DeckLibrary.sol";
import "./libraries/GameLibrary.sol";

contract Blackjack is RandomProvider, BalanceController, RoomSupport, Owned, CircuitBreaker {
    using GameLibrary for GameLibrary.GameState;

    uint16 constant MAX_PLAYERS = 4;
    uint constant MIN_BET = 5;
    uint constant MAX_BET = 100;
    uint nonce;

    Room[] rooms;
    mapping(uint => GameLibrary.GameState) games;
    mapping(address => PlayerBalance) balances;

    event RoomCreated(uint roomId, address creator);
    event PlayerBetted(uint order, uint roomId, address player, uint bet);
    event PlayerJoined(uint order, uint roomId, address player);
    event PlayerLeft(uint order, uint roomId, address player);
    event PlayerReadyForNextRoundChanged(uint order, uint roomId, address player, bool ready);
    event PlayerDecisionReceived(uint order, uint roomId, address player, uint playerDecision);
    event GameStageChanged(uint order, uint roomId, uint stage);
    event CurrentPlayerIndexChanged(uint order, uint roomId, uint playerIndex, address player);
    event GameRoundResultsAnnounced(uint order, uint roomId, int dealerOutcomes, address[] players, int[] playerOutcomes);
    event Log(string message);

    struct PlayerBalance {
        bool exists;
        int balance;
    }

    constructor() public {
    }

    function random(uint x) public returns (uint256) {
        return uint256(keccak256(abi.encodePacked(block.timestamp, block.difficulty, nonce++, x)));
    }

    function payout(uint roomId, address player)
        public
        gameMustExist(roomId)
        atAnyOfStage(games[roomId], GameLibrary.GameStage.Ended, GameLibrary.GameStage.WaitingForPlayersAndBetting)
    {
        GameLibrary.GameState storage game = games[roomId];
        GameLibrary.PlayerState storage playerState = game.playerStates[player];

        balances[player].balance += int(playerState.winning);
        playerState.winning = 0;
    }

    function refund(uint roomId, address player)
        internal
        gameMustExist(roomId)
    {
        GameLibrary.GameState storage game = games[roomId];
        GameLibrary.PlayerState storage playerState = game.playerStates[player];

        balances[player].balance += int(playerState.bet);
        playerState.bet = 0;
    }

    function createRoom(bytes32 name) public {
        clearInactiveRooms();

        balances[msg.sender].exists = true;

        uint roomIndex = rooms.length++;
        Room storage room = rooms[roomIndex];
        room.id = nonce++;
        room.name = name;
        room.discoverable = true;
        room.creator = msg.sender;
        room.players.push(msg.sender);

        GameLibrary.GameState storage game = games[room.id];
        emit RoomCreated(room.id, msg.sender);
        game.init(room.id, this, this);
        game.dealer = room.creator;
    }

    function getEventNonce(uint roomId) public view returns(uint) {
        return games[roomId].eventNonce;
    }

    function getBalance(address player)
        public
        view
        balanceMustExist(player)
        returns (int)
    {
        PlayerBalance storage balance = balances[player];
        return balance.balance;
    }

    function placeBet(uint roomId, uint bet)
        public
        gameMustExist(roomId)
        onlyPlayerParticipants(roomId, msg.sender)
        atStage(games[roomId], GameLibrary.GameStage.WaitingForPlayersAndBetting)
    {
        require(bet > 0, "bet must be > 0");
        require(bet < MIN_BET, "bet too small");
        require(bet > MAX_BET, "bet too big");

        GameLibrary.GameState storage game = games[roomId];
        // TODO: check if player has enough balance to bet?
        GameLibrary.PlayerState storage playerState = game.playerStates[msg.sender];
        require(playerState.bet == 0, "already betted");

        game.playerStates[msg.sender].bet = bet;
        balances[msg.sender].exists = true;
        balances[msg.sender].balance -= int(bet);

        emit PlayerBetted(game.eventNonce++, roomId, msg.sender, bet);
    }

    function startGame(uint roomId)
        public
        gameMustExist(roomId)
        onlyRoomCreator(roomId)
        atStage(games[roomId], GameLibrary.GameStage.WaitingForPlayersAndBetting)
    {
        startGameUnchecked(roomId);
    }

    function playerDecision(uint roomId, GameLibrary.PlayerDecision decision)
        public
        gameMustExist(roomId)
    {
        GameLibrary.GameState storage game = games[roomId];
        game.playerDecision(decision);

        setRoomDiscoverableOnGameEnded(game);
    }

    function setPlayerReadyForNextRound(uint roomId, bool ready)
        public
        gameMustExist(roomId)
    {
        GameLibrary.GameState storage game = games[roomId];
        game.setPlayerReadyForNextRound(ready);
    }

    function nextRound(uint roomId)
        public
        gameMustExist(roomId)
    {
        GameLibrary.GameState storage game = games[roomId];
        game.nextRound();
    }

    function getGameStatePlayer(uint roomId, address player)
        public
        view
        gameMustExist(roomId)
        returns (uint8[], uint, uint, bool)
    {
        GameLibrary.GameState storage game = games[roomId];
        GameLibrary.PlayerState storage playerState = game.playerStates[player];
        return (playerState.hand, playerState.bet, playerState.winning, playerState.readyForNextRound);
    }

    function getGameState(uint roomId)
        public
        view
        gameMustExist(roomId)
        onlyParticipants(roomId)
        returns (GameLibrary.GameStage, uint8[], address, address[], uint, uint8[], uint)
    {
        GameLibrary.GameState storage game = games[roomId];
        return (game.stage, game.usedCards, game.dealer, game.players, game.currentPlayerIndex, game.playerStates[game.dealer].hand, game.playerStates[game.dealer].winning);
    }

    function joinRoom(uint roomId)
        public
    {
        Room storage room = rooms[getRoomIndexByRoomId(roomId)];
        for(uint i = 0; i < room.players.length; i++) {
            if (room.players[i] == msg.sender) {
                // Already joined
                return;
            }
        }

        require(room.players.length < MAX_PLAYERS, "room is full");
        room.players.push(msg.sender);

        GameLibrary.GameState storage game = games[roomId];
        game.addPlayerUnsafe(msg.sender);

        emit PlayerJoined(game.eventNonce++, roomId, msg.sender);
    }

    function leaveRoom(uint roomId)
        public
        gameMustExist(roomId)
        onlyParticipants(roomId)
    {
        (bool found, uint roomIndex) = getRoomIndexByRoomIdSafe(roomId);
        if (!found)
            return;

        GameLibrary.GameState storage game = games[roomId];

        // Stop the game if any player leaves mid-game, or if game creator leaves at any time
        if (!(game.stage == GameLibrary.GameStage.WaitingForPlayersAndBetting || game.stage == GameLibrary.GameStage.Ended) ||
            game.dealer == msg.sender) {
            emit Log("Player left, refund all other players and destroy game");
            for (uint i = 0; i < game.players.length; i++) {
                if (game.players[i] == msg.sender) {
                    // If player leaves mid-game, his bet goes to the dealer
                    if (game.dealer != msg.sender) {
                        GameLibrary.PlayerState storage dealerState = game.playerStates[game.dealer];
                        GameLibrary.PlayerState storage playerState = game.playerStates[msg.sender];

                        dealerState.winning += playerState.bet;
                        playerState.bet = 0;

                        balances[game.dealer].balance += int(dealerState.winning);
                        dealerState.winning = 0;
                    } else {
                        continue;
                    }
                }

                refund(game.roomId, game.players[i]);
            }

            destroyGameAndRoom(game, roomIndex);
        } else {
            // Refund player if he leaves before game has started
            ArrayLibrary.removeAddressFromArrayUnordered(rooms[roomIndex].players, msg.sender);
            refund(game.roomId, msg.sender);
            game.removePlayer(msg.sender);

            emit PlayerLeft(game.eventNonce++, game.roomId, msg.sender);
        }
    }

    function getRooms() public view returns (uint[], bytes32[], uint8[]) {
        uint i;
        uint discoverableRoomsCounter = 0;
        for (i = 0; i < rooms.length; i++) {
            Room storage room = rooms[i];
            if (!room.discoverable)
                continue;

            discoverableRoomsCounter++;
        }

        uint[] memory ids = new uint[](discoverableRoomsCounter);
        bytes32[] memory names = new bytes32[](discoverableRoomsCounter);
        uint8[] memory playerCounts = new uint8[](discoverableRoomsCounter);

        discoverableRoomsCounter = 0;
        for (i = 0; i < rooms.length; i++) {
            room = rooms[i];
            if (!room.discoverable)
                continue;

            ids[discoverableRoomsCounter] = room.id;
            names[discoverableRoomsCounter] = room.name;
            playerCounts[discoverableRoomsCounter] = uint8(room.players.length);
            discoverableRoomsCounter++;
        }

        return (ids, names, playerCounts);
    }

    function setRoomDiscoverableOnGameEnded(GameLibrary.GameState storage game) private {
        if (game.stage == GameLibrary.GameStage.Ended) {
            Room storage room = rooms[getRoomIndexByRoomId(game.roomId)];
            room.discoverable = true;
            emit RoomCreated(room.id, room.creator);
        }
    }

    function startGameUnchecked(uint roomId) private {
        uint roomIndex = getRoomIndexByRoomId(roomId);
        Room storage room = rooms[roomIndex];

        GameLibrary.GameState storage game = games[roomId];
        game.startGame();

        // Started games are not discoverable
        room.discoverable = false;

        setRoomDiscoverableOnGameEnded(game);
    }

    function destroyGameAndRoom(GameLibrary.GameState storage game, uint roomIndex) private {
        destroyGame(game);
        deleteRoom(roomIndex);
    }

    function destroyGame(GameLibrary.GameState storage game) private {
        uint roomId = game.roomId;
        game.destroy();
        delete games[roomId];
    }

    function deleteRoom(uint roomIndex) private {
        delete rooms[roomIndex];

        while (roomIndex < rooms.length - 1) {
            rooms[roomIndex] = rooms[roomIndex + 1];
            roomIndex++;
        }
        rooms.length--;
    }

    function clearInactiveRooms() private {
        for (uint i = rooms.length; i-- > 0; ) {
            Room storage room = rooms[i];
            GameLibrary.GameState storage game = games[room.id];

            if (now - game.lastUpdateTime > 10 minutes) {
                destroyGameAndRoom(game, i);
            }
        }
    }

    function getRoomIndexByRoomIdSafe(uint roomId) internal view returns (bool, uint) {
        for (uint i = 0; i < rooms.length; i++) {
            if (rooms[i].id == roomId)
                return (true, i);
        }

        return (false, 0);
    }

    function getRoomIndexByRoomId(uint roomId) internal view returns (uint) {
        (bool found, uint roomIndex) = getRoomIndexByRoomIdSafe(roomId);
        require(found, "Unknown room id ");

        return roomIndex;
    }

    function isParticipating(uint roomId, address player) private view returns (bool) {
        GameLibrary.GameState storage game = games[roomId];
        if (game.dealer == player)
            return true;

        for(uint i = 0; i < game.players.length; i++) {
            if (game.players[i] == player) {
                return true;
            }
        }

        return false;
    }

    modifier onlyPlayerParticipants(uint roomId, address player) {
        GameLibrary.GameState storage game = games[roomId];
        for(uint i = 0; i < game.players.length; i++) {
            if (game.players[i] == player) {
                _;
                return;
            }
        }

        revert("only players can execute this method");
    }

    modifier onlyParticipants(uint roomId) {
        require(isParticipating(roomId, msg.sender), "must be a game participant");
        _;
    }

    modifier atStage(GameLibrary.GameState storage game, GameLibrary.GameStage stage) {
        require(game.stage == stage, "can't be called at this game stage");
        _;
    }

    modifier atAnyOfStage(GameLibrary.GameState storage game, GameLibrary.GameStage stage1, GameLibrary.GameStage stage2) {
        require(game.stage == stage1 || game.stage == stage2, "can't be called at this game stage");
        _;
    }

    modifier gameMustExist(uint roomId) {
        GameLibrary.GameState storage game = games[roomId];
        if (!game.initialized) {
            revert("game doesn't exists");
        }

        _;
    }

    modifier onlyRoomCreator(uint roomId) {
        uint roomIndex = getRoomIndexByRoomId(roomId);
        Room storage room = rooms[roomIndex];
        require(room.creator == msg.sender, "only room creator can start game");
        _;
    }

    modifier balanceMustExist(address balance) {
        require(balances[balance].exists, "balance must exist");
        _;
    }
}