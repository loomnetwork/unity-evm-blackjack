pragma solidity ^0.4.24;

import "./Owned.sol";
import "./Mortal.sol";
import "./CircuitBreaker.sol";
import "./RandomProvider.sol";
import "./BalanceController.sol";
import "./libraries/DeckLibrary.sol";
import "./libraries/GameLibrary.sol";

contract GameRooms {
    enum RoomState {
        WaitingForPlayers,
        Started
    } 
    
    struct Room {
        uint id;
        bytes32 name;
        RoomState state;
        address creator;
        address[] players;
    }
}

contract BlackJack is RandomProvider, BalanceController, GameRooms, Owned, Mortal, CircuitBreaker {
    using GameLibrary for GameLibrary.GameState;
    
    uint16 constant DECK_COUNT = 6;
    uint16 constant TOTAL_CARD_COUNT = DECK_COUNT * 52;
    uint16 constant MAX_PLAYERS = 3;
    uint nonce;
    
    event RoomCreated(address creator, uint roomId);
    event PlayerJoined(uint roomId, address player);
    event GameStageChanged(uint roomId, uint stage);
    event CurrentPlayerIndexChanged(uint roomId, uint playerIndex, address player);
    event PlayerDecisionReceived(uint roomId, uint playerIndex, address playerAddres, uint playerDecision);
    event Log(string message);
    
    struct PlayerBalance {
        bool exists;
        int balance;
    }
    
    Room[] rooms;
    mapping(uint => GameLibrary.GameState) public games;
    mapping(address => PlayerBalance) balances;

    constructor() public {
    }
    
    function random(uint _x) public returns (uint256) {
        return uint256(keccak256(abi.encodePacked(block.timestamp, block.difficulty, nonce++, _x)));
    }
    
    function payout(uint roomId, address balance) public {
        GameLibrary.GameState storage game = games[roomId];
        GameLibrary.PlayerState storage playerState = game.playerStates[balance];
        balances[balance].balance += int(playerState.winnings);
        playerState.winnings = 0;
    }
    
    function createRoom(bytes32 _name) public {
        clearInactiveRooms();
        
        balances[msg.sender].exists = true;
        
        uint roomIndex = rooms.length++;
        Room storage room = rooms[roomIndex];
        room.id = nonce;
        room.name = _name;
        room.state = RoomState.WaitingForPlayers;
        room.creator = msg.sender;
        
        emit RoomCreated(msg.sender, room.id);

        GameLibrary.GameState storage game = games[room.id];
        game.init(room.id, this, this);
        nonce++;
    }

    function getBalance(address _player) public view returns (int) {
        PlayerBalance storage balance = balances[_player];
        if (!balance.exists)
            revert("balance doesn't exist");
            
        return balance.balance;
    }

    function placeBet(uint _roomId, uint bet) public {
        if (bet == 0)
            revert("bet must be > 0");
            
        GameLibrary.GameState storage game = games[_roomId];
        // TODO: check if player has enough balance to bet?
        GameLibrary.PlayerState storage playerState = game.playerStates[msg.sender];
        if (playerState.bet != 0)
            revert("already betted");
            
        game.playerStates[msg.sender].bet = bet;
        balances[msg.sender].exists = true;
        balances[msg.sender].balance -= int(bet);
    }
    
    function startGame(uint _roomId) public {
        uint roomIndex = _getRoomIndexByRoomId(_roomId);
        Room storage room = rooms[roomIndex];
        if (room.creator != msg.sender)
            revert("only room creator can start game");
            
        room.state = RoomState.Started;
        
        GameLibrary.GameState storage game = games[_roomId];
        game.startGame(room.creator, room.players);
        
        // Started games are not discoverable
        deleteRoom(roomIndex);
    }
    
    function playerDecision(uint _roomId, GameLibrary.PlayerDecision _decision) public {
        GameLibrary.GameState storage game = games[_roomId];
        game.playerDecision(_decision);
    }

    function getGameStatePlayer(uint _roomId, address player) public view returns (uint8[], uint, uint) {
        GameLibrary.GameState storage game = games[_roomId];
        return (game.playerStates[player].hand, game.playerStates[player].bet, game.playerStates[player].winnings);
    }
    
    function getGameState(uint _roomId) public view returns (GameLibrary.GameStage, uint8[], address[], uint, uint8[]) {
        GameLibrary.GameState storage game = games[_roomId];
        return (game.stage, game.usedCards, game.players, game.currentPlayerIndex, game.playerStates[game.dealer].hand);
    }

    function joinRoom(uint _roomId) public {
        Room storage room = rooms[_getRoomIndexByRoomId(_roomId)];
        for(uint i = 0; i < room.players.length; i++) {
            if (room.players[i] == msg.sender)
                revert("already joined");
        }
        
        room.players.push(msg.sender);
    }
    
    function leaveRoom(uint _roomId) public {
        (bool found, uint roomIndex) = _getRoomIndexByRoomIdSafe(_roomId);
        if (!found)
            return;
            
        Room storage room = rooms[roomIndex];
        GameLibrary.GameState storage game = games[_roomId];
        bool isInGame = game.dealer == msg.sender;
        for(uint i = 0; i < game.players.length; i++) {
            if (room.players[i] == msg.sender) {
                isInGame = true;
            }
        }
        
        if (!isInGame)
            revert("not in room, can't leave");
        
        // Stop the game if any player leaves
        deleteGameAndRoom(game, roomIndex);
    }
        
    function getRoomPlayers(uint _roomId) public view returns (address[]) {
        return rooms[_getRoomIndexByRoomId(_roomId)].players;
    }

    function getRooms() public view returns (uint[], bytes32[]) {
        uint[] memory ids = new uint[](rooms.length);
        bytes32[] memory names = new bytes32[](rooms.length);

        for (uint i = 0; i < rooms.length; i++) {
            Room storage room = rooms[i];
            ids[i] = room.id;
            names[i] = room.name;
        }
        
        return (ids, names);
    }
    
    function deleteGameAndRoom(GameLibrary.GameState storage game, uint roomIndex) private {
        uint roomId = game.roomId;
        game.destroy();
        delete games[roomId];
        deleteRoom(roomIndex);
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
                deleteGameAndRoom(game, i);
            }
        }
    }

    function _getRoomIndexByRoomIdSafe(uint _roomId) private view returns (bool, uint) {
        for (uint i = 0; i < rooms.length; i++) {
            if (rooms[i].id == _roomId)
                return (true, i);
        }
        
        return (false, 0);
    }

    function _getRoomIndexByRoomId(uint _roomId) private view returns (uint) {
        (bool found, uint roomIndex) = _getRoomIndexByRoomIdSafe(_roomId);
        if (!found)
            revert("Unknown room id ");
            
        return roomIndex;
    }

    event TestEvent(uint number);

    function sendTestEvents(uint base) public {
        emit TestEvent(base + 1);
        emit TestEvent(base + 2);
        emit TestEvent(base + 3);
        emit TestEvent(base + 4);
        emit TestEvent(base + 5);
        emit TestEvent(base + 6);
        emit TestEvent(base + 7);
        emit TestEvent(base + 8);
        emit TestEvent(base + 9);
        emit TestEvent(base + 10);
        emit TestEvent(base + 12);
        emit TestEvent(base + 13);
        emit TestEvent(base + 14);
        emit TestEvent(base + 15);
    }
}