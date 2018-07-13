using System.Collections.Generic;
using System.Numerics;
using Loom.Unity3d;
using UnityEngine;

namespace Loom.Blackjack
{
    public class GameState
    {
        public BigInteger RoomId = -1;
        public PlayerRole Role;
        public GameStage Stage;
        public bool IsInGame;
        public Address CurrentPlayer;
        public int CurrentPlayerIndex;

        public readonly PlayerState Dealer = new PlayerState();
        public PlayerState[] Players = new PlayerState[0];

        public class PlayerState
        {
            public Address Address;
            public int Bet;
            public Card[] Hand;
            public bool ReadyForNextRound;
            public int Outcome;
        }
    }

}