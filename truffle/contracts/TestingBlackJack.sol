pragma solidity ^0.4.24;

import "./BlackJack.sol";

contract TestingBlackJack is BlackJack {
    uint[] nextFakeRandomNumbers;
    uint fakeRandomNumberIndex;
    
    function setNextFakeRandomNumbers(uint[] numbers) public {
        nextFakeRandomNumbers = numbers;
    }
    
    function random(uint /* _x */) public  returns (uint256) {
        fakeRandomNumberIndex++;
        if (fakeRandomNumberIndex > nextFakeRandomNumbers.length)
            revert("out of fake random numbers");
            
        return nextFakeRandomNumbers[fakeRandomNumberIndex - 1];
    }
    
    function getCardScore(DeckLibrary.CardValue _card) public pure returns (uint, uint) {
        return GameLibrary.getCardScore(_card);
    }
    
    function calculateHandScore(uint8[] hand) public pure returns (uint) {
        return GameLibrary.calculateHandScore(hand);
    }
}