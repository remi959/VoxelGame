namespace Assets.Scripts.Core
{
    using Assets.Scripts.Events;
    using Assets.Scripts.NPCs.Units;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public Worker PlayerWorker { get; private set; }

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

        private void OnDestroy()
        {
            EventBus.Clear();
        }
    }
}