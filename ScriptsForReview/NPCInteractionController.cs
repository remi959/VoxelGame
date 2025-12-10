using UnityEngine;

namespace Assets.Scripts.Core
{
    public class NPCInteractionController : MonoBehaviour
    {
        public void OnNPCClicked(RaycastHit hitInfo)
        {
            GameObject npc = hitInfo.collider.gameObject;
            Debug.Log($"Interacting with NPC: {npc.name}");

            // Get NPC component and trigger interaction
            // var npcComponent = npc.GetComponent<NPC>();
            // npcComponent?.Interact();
        }
    }
}