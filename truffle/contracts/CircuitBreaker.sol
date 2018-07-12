pragma solidity ^0.4.24;

import "./Owned.sol";

contract CircuitBreaker is Owned {
    bool stopped;
    
    constructor() public {
        stopped = false;
    }
    
    function toggleActive() onlyOwner public {
        stopped = !stopped;
    }
    
    modifier stopIfEmergency() {
        if (!stopped) _;
    }
    
    modifier emergencyOnly() {
        if (stopped) _;
    }
}
