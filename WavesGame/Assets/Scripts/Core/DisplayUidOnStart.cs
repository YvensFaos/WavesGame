using TMPro;
using UnityEngine;

namespace Core
{
    public class DisplayUidOnStart : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI textMesh;

        private void Start()
        {
            textMesh.text = GameManager.GetSingleton().GetSessionUid();
        }
    }
}
