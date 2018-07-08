using System.Collections.Generic;

namespace Loom.Blackjack
{
    public class BlackjackRules
    {
        public static void CalculateHandScore(IList<Card> hand, out int softScore, out int hardScore)
        {
            int baseScore = 0;
            int aceCount = 0;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].CardValue == CardValue.Ace)
                {
                    aceCount++;
                    continue;
                }

                baseScore += hand[i].CardScore;
            }

            hardScore = softScore = baseScore;
            for (int i = 0; i < aceCount; i++) {
                if (hardScore + 11 > 21) {
                    hardScore += 1;
                } else {
                    hardScore += 11;
                }

                softScore += 1;
            }
        }
    }
}