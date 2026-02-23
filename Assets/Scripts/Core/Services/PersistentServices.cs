// ============================================================================
// PersistentServices.cs - Centralized persistent service lifecycle management
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core.Services
{
    /// <summary>
    /// Centralized manager for all persistent services that survive scene loads.
    /// 
    /// WHY THIS EXISTS:
    /// Unity's DontDestroyOnLoad only works correctly on ROOT GameObjects.
    /// Calling it on child objects causes warnings and undefined behavior.
    /// This class provides a single root that persists, with all services as children.
    /// 
    /// HOW IT WORKS:
    /// 1. PersistentServices is the ONLY object that calls DontDestroyOnLoad
    /// 2. All service GameObjects are created as children of this root
    /// 3. Services register with ServiceLocator for access
    /// 4. Duplicate PersistentServices instances are destroyed (singleton pattern)
    /// 
    /// SCENE SETUP:
    /// Create a single GameObject named "[Services]" with this component.
    /// Add child GameObjects for each service (GameManager, EconomyService, etc.)
    /// This root object handles DontDestroyOnLoad for all children.
    /// 
    /// ALTERNATIVE - RUNTIME SETUP:
    /// Call PersistentServices.Initialize() from a bootstrap script.
    /// Pass the service types you want to create.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // Execute before all other scripts
    public class PersistentServices : MonoBehaviour
    {
        // ========== Singleton ==========
        
        private static PersistentServices instance;
        
        /// <summary>
        /// The singleton instance. Use ServiceLocator for service access instead.
        /// </summary>
        public static PersistentServices Instance => instance;

        /// <summary>
        /// Whether the services system has been initialized.
        /// </summary>
        public static bool IsInitialized => instance != null;

        // ========== Configuration ==========

        [Header("Debug")]
        [Tooltip("Enable verbose logging of service lifecycle events.")]
        [SerializeField]
        private bool enableLogging = false;

        // ========== Runtime State ==========

        // Track registered services for reference
        private readonly Dictionary<Type, MonoBehaviour> registeredServices = new();

        // ========== Lifecycle ==========

        private void Awake()
        {
            // Singleton check - destroy duplicates
            if (instance != null && instance != this)
            {
                Log($"Duplicate PersistentServices detected, destroying this instance");
                Destroy(gameObject);
                return;
            }

            instance = this;
            
            // This is the ONLY DontDestroyOnLoad call in the entire service system
            // All children are automatically included in the persistent scene
            DontDestroyOnLoad(gameObject);
            
            // Ensure clear naming for hierarchy
            if (!gameObject.name.StartsWith("["))
            {
                gameObject.name = "[Services]";
            }
            
            Log("PersistentServices initialized");
            
            // Register any existing child services
            RegisterExistingChildren();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                Log("PersistentServices destroyed, clearing ServiceLocator");
                instance = null;
                ServiceLocator.Clear();
            }
        }

        // ========== Service Registration ==========

        /// <summary>
        /// Register all existing child MonoBehaviours as services.
        /// Called on Awake to pick up scene-placed services.
        /// </summary>
        private void RegisterExistingChildren()
        {
            // Get all MonoBehaviour components on children (not this object)
            foreach (Transform child in transform)
            {
                var components = child.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    if (component == null) continue;
                    if (component is PersistentServices) continue;
                    
                    RegisterService(component);
                }
            }
            
            Log($"Registered {registeredServices.Count} existing services");
        }

        /// <summary>
        /// Register a service with ServiceLocator.
        /// </summary>
        private void RegisterService(MonoBehaviour service)
        {
            var type = service.GetType();
            
            if (registeredServices.ContainsKey(type))
            {
                Log($"Service {type.Name} already registered, skipping");
                return;
            }
            
            registeredServices[type] = service;
            
            // Use reflection to register with ServiceLocator
            // This allows any MonoBehaviour to be registered without interface requirements
            var registerMethod = typeof(ServiceLocator)
                .GetMethod("Register", new[] { type })
                ?.MakeGenericMethod(type);
            
            // Fallback: register directly
            typeof(ServiceLocator)
                .GetMethod("Register", 1, new[] { Type.MakeGenericMethodParameter(0) })
                ?.MakeGenericMethod(type)
                .Invoke(null, new object[] { service });
            
            Log($"Registered service: {type.Name}");
        }

        // ========== Public API ==========

        /// <summary>
        /// Create and add a new service at runtime.
        /// </summary>
        public T AddService<T>(string gameObjectName = null) where T : MonoBehaviour
        {
            var type = typeof(T);
            
            // Check if already exists
            if (registeredServices.TryGetValue(type, out var existing))
            {
                return existing as T;
            }
            
            // Create new child GameObject
            var serviceGO = new GameObject(gameObjectName ?? type.Name);
            serviceGO.transform.SetParent(transform);
            
            // Add component
            var service = serviceGO.AddComponent<T>();
            
            RegisterService(service);
            
            return service;
        }

        /// <summary>
        /// Adopt an existing service into the persistent hierarchy.
        /// Use this to move scene-placed services under the persistent root.
        /// </summary>
        public void AdoptService(MonoBehaviour service)
        {
            if (service == null) return;
            if (service.transform.parent == transform) return;
            
            // Move to this hierarchy
            service.transform.SetParent(transform);
            
            RegisterService(service);
            
            Log($"Adopted service: {service.GetType().Name}");
        }

        /// <summary>
        /// Check if a service type is registered.
        /// </summary>
        public bool HasService<T>() where T : MonoBehaviour
        {
            return registeredServices.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Get a registered service by type.
        /// Prefer ServiceLocator.Get&lt;T&gt;() for consistency.
        /// </summary>
        public T GetService<T>() where T : MonoBehaviour
        {
            return registeredServices.TryGetValue(typeof(T), out var service) 
                ? service as T 
                : null;
        }

        /// <summary>
        /// Get all registered service types.
        /// </summary>
        public IEnumerable<Type> GetRegisteredServiceTypes()
        {
            return registeredServices.Keys;
        }

        // ========== Static Factory ==========

        /// <summary>
        /// Initialize the PersistentServices system.
        /// Creates the root if needed, optionally with specified service types.
        /// </summary>
        /// <param name="serviceTypes">Optional service types to create</param>
        public static PersistentServices Initialize(params Type[] serviceTypes)
        {
            if (instance != null) return instance;

            // Check if one exists in scene
            instance = FindAnyObjectByType<PersistentServices>();
            
            if (instance == null)
            {
                // Create new instance
                var go = new GameObject("[Services]");
                instance = go.AddComponent<PersistentServices>();
            }
            
            // Create requested services
            foreach (var type in serviceTypes)
            {
                if (type == null) continue;
                if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    Debug.LogWarning($"PersistentServices: {type.Name} is not a MonoBehaviour");
                    continue;
                }
                
                instance.CreateServiceOfType(type);
            }
            
            return instance;
        }

        /// <summary>
        /// Ensure PersistentServices exists without creating services.
        /// </summary>
        public static void EnsureExists()
        {
            Initialize();
        }

        /// <summary>
        /// Create a service by Type (runtime).
        /// </summary>
        private void CreateServiceOfType(Type serviceType)
        {
            if (registeredServices.ContainsKey(serviceType)) return;
            
            var serviceGO = new GameObject(serviceType.Name);
            serviceGO.transform.SetParent(transform);
            
            var service = serviceGO.AddComponent(serviceType) as MonoBehaviour;
            if (service != null)
            {
                RegisterService(service);
            }
        }

        // ========== Logging ==========

        private void Log(string message)
        {
            if (enableLogging)
            {
                Debug.Log($"[Services] {message}");
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor utility: List all services in the hierarchy.
        /// </summary>
        [ContextMenu("List Registered Services")]
        private void ListServices()
        {
            Debug.Log($"=== Persistent Services ({registeredServices.Count}) ===");
            foreach (var kvp in registeredServices)
            {
                Debug.Log($"  - {kvp.Key.Name}: {(kvp.Value != null ? "Active" : "Null")}");
            }
        }
#endif
    }
}
