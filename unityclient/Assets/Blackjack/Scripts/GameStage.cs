namespace Loom.Blackjack
{
    public enum GameStage {
        WaitingForPlayersAndBetting,
        Started,
        PlayersTurn,
        DealerTurn,
        Ended,
        Destroyed
    }
}