using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoomListItem : MonoBehaviour
{
    public Button Button;
    public Text ButtonText;

    private void OnDestroy()
    {
        Button.onClick.RemoveAllListeners();
    }
}
