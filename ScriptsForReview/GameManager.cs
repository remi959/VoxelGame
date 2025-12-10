namespace Assets.Scripts.Core
{
    using Assets.Scripts.Events;
    using UnityEngine;

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        private void OnDestroy() => EventBus.Clear();
    }
}