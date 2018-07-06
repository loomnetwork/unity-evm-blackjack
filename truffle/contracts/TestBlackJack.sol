pragma solidity ^0.4.24;

import "./BlackJack.sol";

contract TestBlackJack is BlackJack {
    uint[] nextFakeRandomNumbers;
    uint fakeRandomNumberIndex;
    
    function setNextFakeRandomNumber(uint number) public {
        nextFakeRandomNumbers.push(number);
    }
    
    function setNextFakeRandomNumbers(uint[] numbers) public {
        for (uint i = 0; i < numbers.length; i++) {
            nextFakeRandomNumbers.push(numbers[i]);
        }
    }
    
    function random(uint /* _x */) public  returns (uint256) {
        fakeRandomNumberIndex++;
        return nextFakeRandomNumbers[fakeRandomNumberIndex - 1];
    }
}