using UnityEngine;
using UnityEngine.UI;

namespace Loom.Blackjack
{
    public class PlayerViewUIContainer : MonoBehaviour
    {
        public GameObject SelfPlayerMarker;
        public GameObject ActivePlayerMarker;
        public GameObject CardListContainer;
        public GameObject BlackjackLabelContainer;
        public GameObject HandScoreContainer;
        public GameObject ReadyForNextRoundMarker;
        public GameObject NotReadyForNextRoundMarker;
        public GameObject BetContainer;
        public Text HandScore;
        public Text PlayerName;
        public Text BetValue;
    }
}