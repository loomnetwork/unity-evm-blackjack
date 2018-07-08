using UnityEngine;

namespace Loom.BlackJack
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