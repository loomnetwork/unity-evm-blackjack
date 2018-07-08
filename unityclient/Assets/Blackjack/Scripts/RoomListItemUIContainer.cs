using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Loom.Blackjack
{
    public class RoomListItemUIContainer : MonoBehaviour
    {
        public Button Button;
        public Text ButtonText;

        private void OnDestroy()
        {
            this.Button.onClick.RemoveAllListeners();
        }
    }
}