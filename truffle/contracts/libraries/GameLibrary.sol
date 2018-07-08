pragma solidity ^0.4.24;

import "./../RandomProvider.sol";
import "./../BalanceController.sol";
import "./DeckLibrary.sol";

library GameLibrary {
    event RoomCreated(address creator, uint roomId);
    event PlayerJoined(uint roomId, address player);
    event GameStageChanged(uint roomId, uint stage);
    event CurrentPlayerIndexChanged(uint roomId, uint playerIndex, address playerAddress);
    event PlayerDecisionReceived(uint roomId, uint playerIndex, address playerAddres, uint playerDecision);
    event Log(string message);

    struct GameState {
        uint lastUpdateTime;
        uint roomId;
        GameStage stage;
        uint8[] usedCards;
        address dealer;
        address[] players;
        mapping(address => PlayerState) playerStates;
        uint currentPlayerIndex;
        RandomProvider randomProvider;
        BalanceController balanceController;
    }

    struct PlayerState {
        uint bet;
        uint winnings;
        uint8[] hand;
    }
    
    enum PlayerDecision {
        Stand,
        Hit
    }
    
    enum GameStage {
        Betting,
        Started,
        PlayersTurn,
        DealerTurn,
        Ended
    }
    
    function init(GameState storage self, uint roomId, RandomProvider randomProvider, BalanceController balanceController) internal {
        self.lastUpdateTime = now;
        self.roomId = roomId;
        self.randomProvider = randomProvider;
        self.balanceController = balanceController;
        setGameStage(self, GameStage.Betting);
    }
    
    function destroy(GameState storage self) internal {
        delete self.playerStates[self.dealer];
        for(uint i = 0; i < self.players.length; i++) {
            delete self.playerStates[self.players[i]];
        }
    }
    
    function startGame(GameState storage self, address dealer, address[] players) internal {
        setGameStage(self, GameStage.Started);
        self.dealer = dealer;
        self.players = players;
        if (self.players.length == 0)
            revert("self.players.length == 0");

        uint i;

        // Check if betted
        for(i = 0; i < self.players.length; i++) {
            if (self.playerStates[self.players[i]].bet == 0)
                revert("not all players have betted");
        }

        // Initial deal
        for(i = 0; i < self.players.length; i++) {
            dealCard(self, self.players[i]);
        }

        dealCard(self, self.dealer);

        for(i = 0; i < self.players.length; i++) {
            dealCard(self, self.players[i]);
        }
        
        setGameStage(self, GameStage.PlayersTurn);
        nextPlayerMove(self, true);
    }
    
    function isPlayerInGame(GameState storage self, address player) internal view returns (bool) {
        for(uint i = 0; i < self.players.length; i++) {
            if (self.players[i] == player) {
                return true;
            }
        }
        
        return false;
    }
    
    function playerDecision(GameState storage self, PlayerDecision decision) internal {
        self.lastUpdateTime = now;
        
        bool isInGame = false;
        for(uint i = 0; i < self.players.length; i++) {
            if (self.players[i] == msg.sender) {
                isInGame = true;
                if (i != self.currentPlayerIndex) {
                    revert("not your turn");
                }
            }
        }
        
        if (!isInGame)
            revert("not in this self");
        
        emit PlayerDecisionReceived(self.roomId, self.currentPlayerIndex, self.players[self.currentPlayerIndex], uint(decision));
        
        if (decision == PlayerDecision.Stand) {
            nextPlayerMove(self, false);
            return;
        }
        
        if (decision == PlayerDecision.Hit) {
            dealCard(self, msg.sender);
            uint score = calculateHandScore(self.playerStates[msg.sender].hand);
            if (score >= 21) {
                nextPlayerMove(self, false);
            }
        }
    }
    
    function nextPlayerMove(GameState storage self, bool isGameStart) internal returns (bool) {
        if (self.currentPlayerIndex == self.players.length) {
            emit Log("last player");
            dealerTurn(self);
            
            return true;
        }
        PlayerState storage playerState = self.playerStates[self.players[self.currentPlayerIndex]];
        bool playerHasNatural = playerState.hand.length == 2 && calculateHandScore(playerState.hand) == 21;
        // Player with natural can't hit or stand
        // TODO: player with natural can still have insurance
        if (playerHasNatural) {
            emit CurrentPlayerIndexChanged(self.roomId, self.currentPlayerIndex, self.players[self.currentPlayerIndex]);
            self.currentPlayerIndex++;
            if (nextPlayerMove(self, isGameStart))
                return;
        }

        if (!isGameStart) {
            self.currentPlayerIndex++;
        }
        if (self.currentPlayerIndex == self.players.length) {
            emit Log("last player");
            dealerTurn(self);
            
            return true;
        }
        emit CurrentPlayerIndexChanged(self.roomId, self.currentPlayerIndex, self.players[self.currentPlayerIndex]);

        return false;
    }
    
    function setGameStage(GameState storage self, GameStage stage) internal {
        self.lastUpdateTime = now;
        self.stage = stage;
        emit GameStageChanged(self.roomId, uint(stage));
    }
    
    function dealerTurn(GameState storage self) internal {
        // Check if all player are bust, dealer makes no move if so
        bool allPlayersBust = true;
        for(uint i = 0; i < self.players.length; i++) {
            PlayerState storage playerState = self.playerStates[self.players[i]];
            uint playerScore = calculateHandScore(playerState.hand);
            if (playerScore <= 21) {
                allPlayersBust = false;
                break;
            }
        }

        if (allPlayersBust) {
            gameEnd(self);
            return;
        }

        setGameStage(self, GameStage.DealerTurn);
        PlayerState storage dealerState = self.playerStates[self.dealer];
        bool flag = true;
        while (flag) {
            uint dealerScore = calculateHandScore(dealerState.hand);
            if (dealerScore < 17) {
                dealCard(self, self.dealer);
            } else {
                flag = false;
            }
        }
        
        gameEnd(self);
    }
    
    function gameEnd(GameState storage self) internal {
        setGameStage(self, GameStage.Ended);

        PlayerState storage dealerState = self.playerStates[self.dealer];
        uint dealerScore = calculateHandScore(dealerState.hand);
        bool dealerHasNatural = dealerState.hand.length == 2 && dealerScore == 21;
        // FIXME: natural blackjack beats hand with 21
        // TODO: The delaler will not play out his hand if there are no players in the self. 
        // And if a player busts, and the dealer then plays out his hand (because there is still at least one 
        // active player still in the self) and he subsequently busts,
        // the player still loses because the player busted first.
        for(i = 0; i < self.players.length; i++) {
            PlayerState storage playerState = self.playerStates[self.players[i]];
            uint playerScore = calculateHandScore(playerState.hand);
            bool playerHasNatural = playerState.hand.length == 2 && playerScore == 21;
            // If any player has a natural and the dealer does not, the dealer immediately pays that player one and a half times the amount of his bet. If the dealer has a natural, he immediately collects the bets of all players who do not have naturals, (but no additional amount). If the dealer and another player both have naturals, the bet of that player is a stand-off (a tie), and the player takes back his chips.
            if (!dealerHasNatural && playerHasNatural) {
                emit Log("player has natural");
                playerState.winnings += playerState.bet * 5 / 2;
                dealerState.winnings -= playerState.bet * 5 / 2 - playerState.bet;
            } else if (dealerHasNatural && !playerHasNatural) {
                emit Log("dealer has natural");
                dealerState.winnings += playerState.bet;
            } else if (dealerHasNatural && playerHasNatural) {
                emit Log("natural tie");
                playerState.winnings += playerState.bet;
            } else {
                if (playerScore > 21) {
                    emit Log("player busts");
                    dealerState.winnings += playerState.bet;
                } else if (dealerScore > 21) {
                    emit Log("dealer busts");
                    playerState.winnings += playerState.bet * 2;
                    dealerState.winnings -= playerState.bet;
                } else {
                    if (dealerScore > playerScore) {
                        emit Log("dealer wins");
                        dealerState.winnings += playerState.bet;
                    } else if (dealerScore == playerScore) {
                        emit Log("tie");
                        playerState.winnings += playerState.bet;
                    } else {
                        emit Log("player wins");
                        playerState.winnings += playerState.bet * 2;
                        dealerState.winnings -= playerState.bet;
                    }
                }
            }
        }

        self.balanceController.payout(self.roomId, self.dealer);
        for(uint i = 0; i < self.players.length; i++) {
            playerState = self.playerStates[self.players[i]];
            self.balanceController.payout(self.roomId, self.players[i]);
        }
        
        emit Log("self end");
    }
    
    function getCardScore(DeckLibrary.CardValue card) internal pure returns (uint, uint) {
        if (card == DeckLibrary.CardValue.Ace) {
            return (1, 11);
        } else if (card < DeckLibrary.CardValue.Ace && card > DeckLibrary.CardValue.Ten) {
            return (10, 10);
        }
        
        uint score = uint(card) + 2;
        return (score, score);
    }
    
    function calculateHandScore(uint8[] hand) internal pure returns (uint) {
        uint score = 0;
        uint aceCount = 0;
        for (uint i = 0; i < hand.length; i++) {
            DeckLibrary.CardValue cardValue = DeckLibrary.getCardValue(hand[i]);
            if (cardValue == DeckLibrary.CardValue.Ace) {
                aceCount++;
                continue;
            }
            
            (uint cardScore, ) = getCardScore(cardValue);
            score += cardScore;
        }
        
        for (i = 0; i < aceCount; i++) {
            if (score + 11 > 21) {
                score += 1;
            } else {
                score += 11;
            }
        }
        
        return score;
    }
    
    function dealCard(GameState storage self, address player) internal {
        uint8 card = drawCard(self);
        self.playerStates[player].hand.push(card);
    }

    function drawCard(GameState storage self) internal returns (uint8) {
        uint8 card = uint8(self.randomProvider.random(self.usedCards.length) % 52);
        // TODO: handle case when all cards of this value are already used
        self.usedCards.push(card);
        return card;
    }
}