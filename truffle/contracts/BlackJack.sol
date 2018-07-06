pragma solidity ^0.4.24;

import "./Owned.sol";
import "./Mortal.sol";
import "./CircuitBreaker.sol";
import "./RandomProvider.sol";
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



contract BlackJack is RandomProvider, GameRooms, Owned, Mortal, CircuitBreaker {
    using GameLibrary for GameLibrary.GameState;
    
    uint16 constant DECK_COUNT = 6;
    uint16 constant TOTAL_CARD_COUNT = DECK_COUNT * 52;
    uint16 constant MAX_PLAYERS = 3;
    uint nonce;
    
    event RoomCreated(address creator, uint roomId);
    event GameStageChanged(uint roomId, GameLibrary.GameStage stage);
    event CurrentPlayerIndexChanged(uint roomId, uint playerIndex);
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
    
    function createRoom(bytes32 _name) public {
        uint roomIndex = rooms.length++;
        Room storage room = rooms[roomIndex];
        room.id = nonce;
        room.name = _name;
        room.state = RoomState.WaitingForPlayers;
        room.creator = msg.sender;

        GameLibrary.GameState storage game = games[room.id];
        game.init(room.id, this);
        nonce++;
        
        emit RoomCreated(msg.sender, room.id);
    }
    
    function getBalance(address _player) public view returns (int) {
        PlayerBalance storage balance = balances[_player];
        if (!balance.exists)
            revert("balance doesn't exist");
            
        return balance.balance;
    }

    function placeBet(uint _roomId, uint bet) public {
        GameLibrary.GameState storage game = games[_roomId];
        // TODO: check if player has enough balance to bet
        game.playerStates[msg.sender].bet = bet;
        balances[msg.sender].balance -= int(bet);
    }
    
    function startGame(uint _roomId) public {
        Room storage room = rooms[_getRoomIndexByRoomId(_roomId)];
        room.state = RoomState.Started;
        
        GameLibrary.GameState storage game = games[_roomId];
        game.startGame(room.creator, room.players);
    }
    
    function playerDecision(uint _roomId, GameLibrary.PlayerDecision _decision) public {
        GameLibrary.GameState storage game = games[_roomId];
        game.playerDecision(_decision);
    }

    function getGameStatePlayer(uint _roomId, address player) public view returns (uint8[], uint, uint) {
        GameLibrary.GameState storage game = games[_roomId];
        return (game.playerStates[player].hand, game.playerStates[player].bet, game.playerStates[player].winnings);
    }
    
    function getGameState(uint _roomId) public view returns (uint8[], uint8[]) {
        GameLibrary.GameState storage game = games[_roomId];
        return (game.usedCards, game.playerStates[game.dealer].hand);
    }

    function roomJoin(uint _roomId) public {
        Room storage room = rooms[_getRoomIndexByRoomId(_roomId)];
        for(uint i = 0; i < room.players.length; i++) {
            if (room.players[i] == msg.sender)
                revert("Player already added");
        }
        
        rooms[_getRoomIndexByRoomId(_roomId)].players.push(msg.sender);
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

    function _getRoomIndexByRoomId(uint _roomId) private view returns (uint) {
        for (uint i = 0; i < rooms.length; i++) {
            if (rooms[i].id == _roomId)
                return i;
        }
        
        revert("Unknown room id ");
    }
    
    function random(uint _x) public returns (uint256) {
        return uint256(keccak256(abi.encodePacked(block.timestamp, block.difficulty, nonce++, _x)));
    }
}