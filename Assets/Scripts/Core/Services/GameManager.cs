namespace Assets.Scripts.Core.Services
{
    using Assets.Scripts.Economy;
    using Assets.Scripts.Core.Events;
    using Assets.Scripts.Shared.Enums;
    using UnityEngine;

    /// <summary>
    /// Core game manager responsible for high-level game state and initialization.
    /// 
    /// PERSISTENCE:
    /// This component should be placed as a child of the [Services] GameObject.
    /// PersistentServices handles DontDestroyOnLoad for the entire hierarchy.
    /// Do NOT add DontDestroyOnLoad calls to this class.
    /// </summary>
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
            ServiceLocator.Register(this);
            
            // NOTE: DontDestroyOnLoad is handled by parent PersistentServices
            // Do not call it here to avoid "only works for root GameObjects" warning
        }

        private void Start()
        {
            EconomyService.Instance.InitializePlayer(0, new System.Collections.Generic.Dictionary<EResourceType, int>
            {
                { EResourceType.Wood, 500 },
                { EResourceType.Stone, 300 },
                { EResourceType.Food, 200 }
            });
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<GameManager>();
            }
            EventBus.Clear();
        }
    }
}