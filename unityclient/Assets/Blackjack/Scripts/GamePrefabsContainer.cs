using UnityEngine;

namespace Loom.Blackjack
{
    [CreateAssetMenu]
    public class GamePrefabsContainer : ScriptableObject
    {
        public GameObject RoomListItemPrefab;
        public GameObject PlayerViewPrefab;
        public GameObject CardPrefab;
        public CardIndexToCardSpriteMap CardToCardSpriteMap;
    }
}