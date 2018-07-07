pragma solidity ^0.4.24;

import "./../RandomProvider.sol";
import "./../BalanceController.sol";
import "./DeckLibrary.sol";

library GameLibrary {
    event RoomCreated(address creator, uint roomId);
    event GameStageChanged(uint roomId, GameStage stage);
    event CurrentPlayerIndexChanged(uint roomId, uint playerIndex);
    event Log(string message);

    struct GameState {
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
        self.roomId = roomId;
        self.randomProvider = randomProvider;
        self.balanceController = balanceController;
        setGameStage(self, GameStage.Betting);
    }
    
    function startGame(GameState storage game, address dealer, address[] players) internal {
        setGameStage(game, GameStage.Started);
        game.dealer = dealer;
        game.players = players;
        if (game.players.length == 0)
            revert("game.players.length == 0");

        uint i;

        // Check if betted
        for(i = 0; i < game.players.length; i++) {
            if (game.playerStates[game.players[i]].bet == 0)
                revert("not all players have betted");
        }

        // Initial deal
        for(i = 0; i < game.players.length; i++) {
            dealCard(game, game.players[i]);
        }

        dealCard(game, game.dealer);

        for(i = 0; i < game.players.length; i++) {
            dealCard(game, game.players[i]);
        }
        
        setGameStage(game, GameStage.PlayersTurn);
        nextPlayerMove(game, true);
    }
    
    function playerDecision(GameState storage game, PlayerDecision _decision) internal {
        bool isInGame = false;
        for(uint i = 0; i < game.players.length; i++) {
            if (game.players[i] == msg.sender) {
                isInGame = true;
                if (i != game.currentPlayerIndex) {
                    revert("not your turn");
                }
            }
        }
        
        if (!isInGame)
            revert("not in this game");
        
        if (_decision == PlayerDecision.Stand) {
            nextPlayerMove(game, false);
            return;
        }
        
        if (_decision == PlayerDecision.Hit) {
            dealCard(game, msg.sender);
            uint score = calculateHandScore(game.playerStates[msg.sender].hand);
            if (score >= 21) {
                nextPlayerMove(game, false);
            }
        }
    }
    
    function nextPlayerMove(GameState storage game, bool isGameStart) internal returns (bool) {
        if (game.currentPlayerIndex == game.players.length) {
            emit Log("last player");
            setGameStage(game, GameStage.DealerTurn);
            dealerTurn(game);
            
            return true;
        }
        PlayerState storage playerState = game.playerStates[game.players[game.currentPlayerIndex]];
        bool playerHasNatural = playerState.hand.length == 2 && calculateHandScore(playerState.hand) == 21;
        // Player with natural can't hit or stand
        // TODO: player with natural can still have insurance
        if (playerHasNatural) {
            game.currentPlayerIndex++;
            emit CurrentPlayerIndexChanged(game.roomId, game.currentPlayerIndex);
            if (nextPlayerMove(game, isGameStart))
                return;
        }

        if (!isGameStart) {
            game.currentPlayerIndex++;
        }
        if (game.currentPlayerIndex == game.players.length) {
            emit Log("last player");
            setGameStage(game, GameStage.DealerTurn);
            dealerTurn(game);
            
            return true;
        }
        emit CurrentPlayerIndexChanged(game.roomId, game.currentPlayerIndex);

        return false;
    }
    
    function setGameStage(GameState storage game, GameStage stage) internal {
        game.stage = stage;
        emit GameStageChanged(game.roomId, stage);
    }
    
    function dealerTurn(GameState storage game) internal {
        PlayerState storage dealerState = game.playerStates[game.dealer];
        bool flag = true;
        while (flag) {
            uint dealerScore = calculateHandScore(dealerState.hand);
            if (dealerScore < 17) {
                dealCard(game, game.dealer);
            } else {
                flag = false;
            }
        }
        
        gameEnd(game);
    }
    
    function gameEnd(GameState storage game) internal {
        setGameStage(game, GameStage.Ended);

        PlayerState storage dealerState = game.playerStates[game.dealer];
        uint dealerScore = calculateHandScore(dealerState.hand);
        bool dealerHasNatural = dealerState.hand.length == 2 && dealerScore == 21;
        // FIXME: natural blackjack beats hand with 21
        // TODO: The delaler will not play out his hand if there are no players in the game. 
        // And if a player busts, and the dealer then plays out his hand (because there is still at least one 
        // active player still in the game) and he subsequently busts,
        // the player still loses because the player busted first.
        for(i = 0; i < game.players.length; i++) {
            PlayerState storage playerState = game.playerStates[game.players[i]];
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

        game.balanceController.payout(game.roomId, game.dealer);
        for(uint i = 0; i < game.players.length; i++) {
            playerState = game.playerStates[game.players[i]];
            
            game.balanceController.payout(game.roomId, game.players[i]);
            // FIXME
            //balances[game.players[i]].balance += int(playerState.winnings);
        }
        
        emit Log("game end");
    }
    
    function getCardScore(DeckLibrary.CardValue _card) internal pure returns (uint, uint) {
        if (_card == DeckLibrary.CardValue.Ace) {
            return (1, 11);
        } else if (_card < DeckLibrary.CardValue.Ace && _card > DeckLibrary.CardValue.Ten) {
            return (10, 10);
        }
        
        uint score = uint(_card) + 2;
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
    
    function dealCard(GameState storage game, address player) internal {
        uint8 card = drawCard(game);
        game.playerStates[player].hand.push(card);
    }

    function drawCard(GameState storage game) internal returns (uint8) {
        uint8 card = uint8(game.randomProvider.random(game.usedCards.length) % 52);
        // TODO: handle case when all cards of this value are already used
        game.usedCards.push(card);
        return card;
    }
}