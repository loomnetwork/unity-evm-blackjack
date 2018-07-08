using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Loom.Blackjack
{
    public struct Card
    {
        private readonly byte index;

        public byte Index => this.index;
        public CardValue CardValue => (CardValue) (this.index % 13);
        public CardSuit CardSuit => (CardSuit) (this.index / 13);

        public int CardScore
        {
            get
            {
                if (CardValue == CardValue.Ace)
                    return 11;

                if (CardValue < CardValue.Ace && CardValue >= CardValue.Ten)
                    return 10;

                return (int) CardValue + 2;
            }
        }

        public Card(byte index)
        {
            if (index >= 52)
                throw new ArgumentException("expected index < 52");

            this.index = index;
        }
    }

}