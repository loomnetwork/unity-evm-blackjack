pragma solidity ^0.4.24;

import "./Blackjack.sol";

contract TestingBlackjack is Blackjack {
    uint[] nextFakeRandomNumbers;
    uint fakeRandomNumberIndex;

    function setNextFakeRandomNumbers(uint[] numbers) public {
        nextFakeRandomNumbers = numbers;
        fakeRandomNumberIndex = 0;
    }

    function random(uint /* x */) public returns (uint256) {
        require(fakeRandomNumberIndex <= nextFakeRandomNumbers.length, "out of fake random numbers");
        return nextFakeRandomNumbers[fakeRandomNumberIndex++];
    }

    function getCardScore(DeckLibrary.CardValue card) public pure returns (uint) {
        return GameLibrary.getCardScore(card);
    }

    function calculateHandScore(uint8[] hand) public pure returns (uint) {
        return GameLibrary.calculateHandScore(hand);
    }

    function getRoomPlayers(uint roomId) public view returns (address[]) {
        return rooms[getRoomIndexByRoomId(roomId)].players;
    }

    function getGamePlayers(uint roomId) public view returns (address[]) {
        return games[roomId].players;
    }

    function resetBalances(address[] array) public {
        for (uint i = 0; i < array.length; i++) {
            balances[array[i]].balance = 0;
        }
    }
}