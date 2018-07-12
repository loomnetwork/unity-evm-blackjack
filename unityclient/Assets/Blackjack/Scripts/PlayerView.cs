using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Loom.Blackjack
{
    public class PlayerView : MonoBehaviour
    {
        public PlayerViewUIContainer UIContainer;

        public void SetCards(IList<Card> cards, GamePrefabsContainer prefabsContainer)
        {
            // Remove current cards
            foreach (Transform child in this.UIContainer.CardListContainer.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (Card card in cards)
            {
                GameObject cardGO = Instantiate(prefabsContainer.CardPrefab, this.UIContainer.CardListContainer.transform);
                cardGO.GetComponent<Image>().sprite = prefabsContainer.CardToCardSpriteMap.CardSprite[card.Index];
            }
        }
    }
}
