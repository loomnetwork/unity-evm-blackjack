pragma solidity ^0.4.24;

import "truffle/Assert.sol";
import "truffle/DeployedAddresses.sol";
import "../contracts/Blackjack.sol";
import "../contracts/libraries/DeckLibrary.sol";
import "../contracts/libraries/GameLibrary.sol";

contract TestBlackjack {
    function testCalculateCards() public {
        Assert.equal(int(DeckLibrary.getCardValue(5)), int(DeckLibrary.CardValue.Seven), "");
        Assert.equal(int(DeckLibrary.getCardValue(14)), int(DeckLibrary.CardValue.Three), "");
        Assert.equal(int(DeckLibrary.getCardValue(51)), int(DeckLibrary.CardValue.Ace), "");

        Assert.equal(int(DeckLibrary.getCardSuit(5)), int(DeckLibrary.CardSuit.Clubs), "");
        Assert.equal(int(DeckLibrary.getCardSuit(14)), int(DeckLibrary.CardSuit.Diamonds), "");
        Assert.equal(int(DeckLibrary.getCardSuit(51)), int(DeckLibrary.CardSuit.Spades), "");

        Assert.equal(GameLibrary.getCardScore(DeckLibrary.CardValue.Seven), 7, "");
        Assert.equal(GameLibrary.getCardScore(DeckLibrary.CardValue.Queen), 10, "");
        Assert.equal(GameLibrary.getCardScore(DeckLibrary.CardValue.Ace), 11, "");
    }
}