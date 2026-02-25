using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.NPCs.Modules
{
    /// <summary>
    /// Makes any GameObject selectable by the player via click or box-select.
    /// Manages its own visual indicator and tracks all active selectables in the scene.
    /// Decoupled from NPC logic — buildings or resources could also use this.
    /// </summary>
    public class Selectable : MonoBehaviour
    {
        [SerializeField] private GameObject selectionIndicator;

        private static readonly HashSet<Selectable> allSelectables = new();
        public static IReadOnlyCollection<Selectable> All => allSelectables;

        public bool IsSelected { get; private set; }

        private void Awake()
        {
            if (selectionIndicator != null)
                selectionIndicator.SetActive(false);
        }

        private void OnEnable() => allSelectables.Add(this);
        private void OnDisable() => allSelectables.Remove(this);

        public void Select()
        {
            if (IsSelected) return;

            IsSelected = true;
            Debug.Log($"{gameObject.name} selected");

            if (selectionIndicator != null)
                selectionIndicator.SetActive(true);
        }

        public void Deselect()
        {
            if (!IsSelected) return;

            IsSelected = false;

            if (selectionIndicator != null)
                selectionIndicator.SetActive(false);
        }
    }
}
