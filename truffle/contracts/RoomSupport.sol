pragma solidity ^0.4.24;

contract RoomSupport {
    struct Room {
        uint id;
        bool discoverable;
        bytes32 name;
        address creator;
        address[] players;
    }
}