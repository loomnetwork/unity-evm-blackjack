pragma solidity ^0.4.24;

import "./libraries/GameLibrary.sol";

interface BalanceController {
    function payout(uint roomId, address balance) external;
}