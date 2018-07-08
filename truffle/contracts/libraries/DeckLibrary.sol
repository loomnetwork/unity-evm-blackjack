pragma solidity ^0.4.24;

library DeckLibrary {
    enum CardValue {
        Two,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Ten,
        Jack,
        Queen,
        King,
        Ace
    }
   
    enum CardSuit {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }
    
    function getCardValue(uint8 card) internal pure returns (CardValue) {
        return CardValue(card % 13);
    }
    
    function getCardSuit(uint8 card) internal pure returns (CardSuit) {
        return CardSuit(card / 13);
    }
}