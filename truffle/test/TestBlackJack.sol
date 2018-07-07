pragma solidity ^0.4.24;

import "truffle/Assert.sol";
import "truffle/DeployedAddresses.sol";
import "../contracts/BlackJack.sol";
import "../contracts/libraries/DeckLibrary.sol";
import "../contracts/libraries/GameLibrary.sol";

contract TestBlackJack {
    function testCalculateCards() public {
        Assert.equal(int(DeckLibrary.getCardValue(5)), int(DeckLibrary.CardValue.Seven), "");
        Assert.equal(int(DeckLibrary.getCardValue(14)), int(DeckLibrary.CardValue.Three), "");
        Assert.equal(int(DeckLibrary.getCardValue(51)), int(DeckLibrary.CardValue.Ace), "");

        Assert.equal(int(DeckLibrary.getCardSuit(5)), int(DeckLibrary.CardSuit.Clubs), "");
        Assert.equal(int(DeckLibrary.getCardSuit(14)), int(DeckLibrary.CardSuit.Diamonds), "");
        Assert.equal(int(DeckLibrary.getCardSuit(51)), int(DeckLibrary.CardSuit.Spades), "");

        uint soft;
        uint hard;
        (soft, hard) = GameLibrary.getCardScore(DeckLibrary.CardValue.Seven);
        Assert.equal(soft, 7, "");
        Assert.equal(hard, 7, "");

        (soft, hard) = GameLibrary.getCardScore(DeckLibrary.CardValue.Queen);
        Assert.equal(soft, 10, "");
        Assert.equal(hard, 10, "");

        (soft, hard) = GameLibrary.getCardScore(DeckLibrary.CardValue.Ace);
        Assert.equal(soft, 1, "");
        Assert.equal(hard, 11, "");
    }
}