pragma solidity ^0.4.24;

import "./Owned.sol";
import "./Mortal.sol";
import "./CircuitBreaker.sol";
import "./RandomProvider.sol";
import "./BalanceController.sol";
import "./libraries/DeckLibrary.sol";
import "./libraries/GameLibrary.sol";


contract BlackJackLogs {
    event RoomCreated(address creator, uint roomId);
    event GameStageChanged(uint roomId, GameLibrary.GameStage stage);
    event CurrentPlayerIndexChanged(uint roomId, uint playerIndex, address playerAddress);
    event Log(string message);
  
    function emitRoomCreatedEvent(address creator, uint roomId) internal {
        emit RoomCreated(creator, roomId);
    }

}