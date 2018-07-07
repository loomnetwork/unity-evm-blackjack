using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Loom.BlackJack
{
    public struct Card
    {
        private readonly byte _index;

        public byte Index => this._index;
        public CardValue CardValue => (CardValue) (this._index % 13);
        public CardSuit CardSuit => (CardSuit) (this._index / 13);

        public Card(byte index)
        {
            if (index >= 52)
                throw new ArgumentException("expected index < 52");

            this._index = index;
        }
    }

}