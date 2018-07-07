pragma solidity ^0.4.24;

interface RandomProvider {
    function random(uint _x) external returns (uint256);
}