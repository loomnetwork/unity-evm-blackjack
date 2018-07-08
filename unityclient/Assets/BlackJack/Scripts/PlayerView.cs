using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Loom.BlackJack
{
    public class PlayerView : MonoBehaviour
    {
        public PlayerViewUIContainer UIContainer;

        public void SetCards(IList<Card> cards, GamePrefabsContainer prefabsContainer)
        {
            // Remove current cards
            foreach (Transform child in UIContainer.CardListContainer.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (Card card in cards)
            {
                GameObject cardGO = Instantiate(prefabsContainer.CardPrefab, UIContainer.CardListContainer.transform);
                cardGO.GetComponent<Image>().sprite = prefabsContainer.CardToCardSpriteMap.CardSprite[card.Index];
            }
        }
    }
}
