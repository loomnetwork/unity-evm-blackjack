pragma solidity ^0.4.24;

import "./../RandomProvider.sol";
import "./../BalanceController.sol";
import "./ArrayLibrary.sol";
import "./DeckLibrary.sol";

library GameLibrary {
    uint16 constant DECK_COUNT = 6;
    uint16 constant TOTAL_CARD_COUNT = DECK_COUNT * 52;

    event RoomCreated(uint roomId, address creator);
    event PlayerBetted(uint order, uint roomId, address player, uint bet);
    event PlayerJoined(uint order, uint roomId, address player);
    event PlayerLeft(uint order, uint roomId, address player);
    event PlayerReadyForNextRoundChanged(uint order, uint roomId, address player, bool ready);
    event PlayerDecisionReceived(uint order, uint roomId, address player, uint playerDecision);
    event GameStageChanged(uint order, uint roomId, uint stage);
    event CurrentPlayerIndexChanged(uint order, uint roomId, uint playerIndex, address player);
    event GameRoundResultsAnnounced(uint order, uint roomId, int dealerOutcomes, address[] players, int[] playerOutcomes);
    event Log(string message);

    struct GameState {
        bool initialized;
        uint roomId;
        uint lastUpdateTime;
        RandomProvider randomProvider;
        BalanceController balanceController;

        GameStage stage;
        uint8[] usedCards;
        address dealer;
        address[] players;
        mapping(address => PlayerState) playerStates;
        uint currentPlayerIndex;
        uint roundNumber;
        uint eventNonce;
    }

    struct PlayerState {
        uint bet;
        int winning;
        int outcome;
        uint8[] hand;
        bool readyForNextRound;
    }

    enum PlayerDecision {
        Stand,
        Hit
    }

    enum GameStage {
        WaitingForPlayersAndBetting,
        Started,
        PlayersTurn,
        DealerTurn,
        Ended,
        Destroyed
    }

    function init(
        GameState storage self,
        uint roomId,
        RandomProvider randomProvider,
        BalanceController balanceController
    ) internal {
        self.initialized = true;
        self.roomId = roomId;
        self.lastUpdateTime = now;
        self.randomProvider = randomProvider;
        self.balanceController = balanceController;
        setGameStage(self, GameStage.WaitingForPlayersAndBetting);
    }

    function destroy(GameState storage self) internal {
        delete self.playerStates[self.dealer];
        for(uint i = 0; i < self.players.length; i++) {
            delete self.playerStates[self.players[i]];
        }

        setGameStage(self, GameStage.Destroyed);
    }

    function removePlayer(GameState storage self, address player)
        internal
        atAnyOfStage(self, GameStage.WaitingForPlayersAndBetting, GameStage.Ended)
    {
        delete self.playerStates[player];
        ArrayLibrary.removeAddressFromArrayUnordered(self.players, player);
    }

    function setGameStage(GameState storage self, GameStage stage) internal {
        self.lastUpdateTime = now;
        self.stage = stage;
        emit GameStageChanged(self.eventNonce++, self.roomId, uint(stage));
    }

    function startGame(GameState storage self) internal {
        require(self.players.length != 0, "can't start a game with 0 players");

        setGameStage(self, GameStage.Started);
        uint i;

        // Check if betted
        for(i = 0; i < self.players.length; i++) {
            require(self.playerStates[self.players[i]].bet != 0, "all players must bet before starting the game");
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

    function playerDecision(GameState storage self, PlayerDecision decision)
        internal
        onlyPlayerParticipants(self, msg.sender)
        atStage(self, GameStage.PlayersTurn)
    {
        self.lastUpdateTime = now;
        require(self.players[self.currentPlayerIndex] == msg.sender, "not your turn");

        emit PlayerDecisionReceived(self.eventNonce++, self.roomId, self.players[self.currentPlayerIndex], uint(decision));

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

    function nextPlayerMove(GameState storage self, bool isGameStart) internal {
        if (!isGameStart) {
            self.currentPlayerIndex++;
        }

        while(true) {
            if (self.currentPlayerIndex == self.players.length) {
                emit Log("last player");
                dealerTurn(self);
                return;
            }

            PlayerState storage playerState = self.playerStates[self.players[self.currentPlayerIndex]];
            bool playerHasNatural = playerState.hand.length == 2 && calculateHandScore(playerState.hand) == 21;
            if (playerHasNatural) {
                self.currentPlayerIndex++;
                continue;
            }

            break;
        }

        emit CurrentPlayerIndexChanged(self.eventNonce++, self.roomId, self.currentPlayerIndex, self.players[self.currentPlayerIndex]);
    }

    function setPlayerReadyForNextRound(GameState storage self, bool ready)
        internal
        onlyPlayerParticipants(self, msg.sender)
        atStage(self, GameStage.Ended)
    {
        PlayerState storage playerState = self.playerStates[msg.sender];
        playerState.readyForNextRound = ready;
        emit PlayerReadyForNextRoundChanged(self.eventNonce++, self.roomId, msg.sender, ready);
    }

    function nextRound(GameState storage self)
        internal
        onlyDealer(self)
        atStage(self, GameStage.Ended)
    {
        for(uint i = 0; i < self.players.length; i++) {
            require(self.playerStates[self.players[i]].readyForNextRound, "all players must be ready for next round");
        }

        // Clear state
        setGameStage(self, GameStage.WaitingForPlayersAndBetting);
        self.roundNumber++;
        self.lastUpdateTime = now;
        self.usedCards.length = 0;
        self.currentPlayerIndex = 0;

        delete self.playerStates[self.dealer];
        for(i = 0; i < self.players.length; i++) {
            delete self.playerStates[self.players[i]];
        }
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
        PlayerState storage dealerState = self.playerStates[self.dealer];
        uint dealerScore = calculateHandScore(dealerState.hand);
        bool dealerHasNatural = dealerState.hand.length == 2 && dealerScore == 21;
        // TODO: The delaler will not play out his hand if there are no players in the self.
        // And if a player busts, and the dealer then plays out his hand (because there is still at least one
        // active player still in the self) and he subsequently busts,
        // the player still loses because the player busted first.
        for(i = 0; i < self.players.length; i++) {
            PlayerState storage playerState = self.playerStates[self.players[i]];
            uint playerScore = calculateHandScore(playerState.hand);
            bool playerHasNatural = playerState.hand.length == 2 && playerScore == 21;
            // If any player has a natural and the dealer does not, the dealer immediately pays that player one and a half times the amount of his bet.
            // If the dealer has a natural, he immediately collects the bets of all players who do not have naturals, (but no additional amount).
            // If the dealer and another player both have naturals, the bet of that player is a stand-off (a tie), and the player takes back his chips.
            if (!dealerHasNatural && playerHasNatural) {
                emit Log("player has natural");
                playerState.winning += int(playerState.bet * 5 / 2);
                dealerState.winning -= int(playerState.bet * 5 / 2 - playerState.bet);
            } else if (dealerHasNatural && !playerHasNatural) {
                emit Log("dealer has natural");
                dealerState.winning += int(playerState.bet);
            } else if (dealerHasNatural && playerHasNatural) {
                emit Log("natural tie");
                playerState.winning += int(playerState.bet);
            } else {
                if (playerScore > 21) {
                    emit Log("player busts");
                    dealerState.winning += int(playerState.bet);
                } else if (dealerScore > 21) {
                    emit Log("dealer busts");
                    playerState.winning += int(playerState.bet * 2);
                    dealerState.winning -= int(playerState.bet);
                } else {
                    if (dealerScore > playerScore) {
                        emit Log("dealer wins");
                        dealerState.winning += int(playerState.bet);
                    } else if (dealerScore == playerScore) {
                        emit Log("tie");
                        playerState.winning += int(playerState.bet);
                    } else {
                        emit Log("player wins");
                        playerState.winning += int(playerState.bet * 2);
                        dealerState.winning -= int(playerState.bet);
                    }
                }
            }

            calculateOutcome(self.playerStates[self.players[i]]);
            playerState.bet = 0;
        }

        calculateOutcome(self.playerStates[self.dealer]);

        setGameStage(self, GameStage.Ended);
        emitGameRoundResultsAnnounced(self);

        self.balanceController.payout(self.roomId, self.dealer);
        for(uint i = 0; i < self.players.length; i++) {
            playerState = self.playerStates[self.players[i]];
            self.balanceController.payout(self.roomId, self.players[i]);
        }

        emit Log("game end");
    }

    function calculateOutcome(PlayerState storage playerState) private {
        playerState.outcome = int(playerState.winning) - int(playerState.bet);
    }

    function emitGameRoundResultsAnnounced(GameState storage self) private {
        address[] memory players = new address[](self.players.length);
        int[] memory playerOutcomes = new int[](self.players.length);

        for (uint i = 0; i < self.players.length; i++) {
            players[i] = self.players[i];
            PlayerState storage playerState = self.playerStates[players[i]];
            playerOutcomes[i] = playerState.outcome;
        }

        emit GameRoundResultsAnnounced(self.eventNonce++, self.roomId, self.playerStates[self.dealer].outcome, players, playerOutcomes);
    }

    function addPlayerUnsafe(GameState storage self, address player)
        atStage(self, GameStage.WaitingForPlayersAndBetting)
        internal
    {
        self.players.push(player);
    }

    function getCardScore(DeckLibrary.CardValue card) internal pure returns (uint) {
        if (card < DeckLibrary.CardValue.Ten)
            return uint(card) + 2;

        if (card == DeckLibrary.CardValue.Ace)
            return 11;

        return 10;
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

            score += getCardScore(cardValue);
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

    modifier atStage(GameLibrary.GameState storage game, GameLibrary.GameStage stage) {
        require(game.stage == stage, "can't be called at this game stage");
        _;
    }

    modifier atAnyOfStage(GameLibrary.GameState storage game, GameLibrary.GameStage stage1, GameLibrary.GameStage stage2) {
        require(game.stage == stage1 || game.stage == stage2, "can't be called at this game stage");
        _;
    }

    modifier onlyDealer(GameState storage self) {
        require(self.dealer == msg.sender, "only dealer can execute this method");
        _;
    }

    modifier onlyPlayerParticipants(GameState storage self, address player) {
        for(uint i = 0; i < self.players.length; i++) {
            if (self.players[i] == player) {
                _;
                return;
            }
        }

        revert("only players can execute this method");
    }
}