// ============================================================================
// ServiceLocator.cs - Centralized service access with testability
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core.Services
{
    /// <summary>
    /// Centralized service locator pattern for consistent singleton access.
    /// 
    /// Why this exists:
    /// - Inconsistent singleton patterns across codebase (MonoBehaviour.Instance, static, EntityRegistry)
    /// - Hard to test with direct static access
    /// - No centralized cleanup on scene transitions
    /// - Difficult to mock services for unit tests
    /// 
    /// Usage:
    /// - Services register themselves: ServiceLocator.Register(this);
    /// - Access services: ServiceLocator.Get&lt;EconomyService&gt;();
    /// - Optional: services can be null-checked before use
    /// - On scene unload: ServiceLocator.Clear();
    /// 
    /// Benefits:
    /// - Single pattern for all services
    /// - Easy to mock for tests
    /// - Centralized cleanup
    /// - Optional logging of service access
    /// - Can detect missing service registrations
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> services = new();
        
        /// <summary>
        /// Enable to log all service registrations and access (debug only).
        /// </summary>
        public static bool EnableLogging { get; set; } = false;

        // ========== Registration ==========

        /// <summary>
        /// Register a service instance.
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            
            if (services.ContainsKey(type))
            {
                if (EnableLogging)
                    Debug.LogWarning($"ServiceLocator: Overwriting existing service {type.Name}");
            }
            
            services[type] = service;
            
            if (EnableLogging)
                Debug.Log($"ServiceLocator: Registered {type.Name}");
        }

        /// <summary>
        /// Register a service by interface type.
        /// Allows registering concrete type but accessing by interface.
        /// </summary>
        public static void Register<TInterface, TImplementation>(TImplementation service) 
            where TInterface : class 
            where TImplementation : class, TInterface
        {
            services[typeof(TInterface)] = service;
            
            if (EnableLogging)
                Debug.Log($"ServiceLocator: Registered {typeof(TImplementation).Name} as {typeof(TInterface).Name}");
        }

        // ========== Access ==========

        /// <summary>
        /// Get a registered service.
        /// Returns null if not registered (doesn't throw).
        /// </summary>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            
            if (services.TryGetValue(type, out var service))
            {
                return service as T;
            }
            
            if (EnableLogging)
                Debug.LogWarning($"ServiceLocator: Service {type.Name} not found");
            
            return null;
        }

        /// <summary>
        /// Get a service, throwing if not registered.
        /// Use when the service is required for the caller to function.
        /// </summary>
        public static T GetRequired<T>() where T : class
        {
            var service = Get<T>();
            
            if (service == null)
            {
                throw new InvalidOperationException(
                    $"Required service {typeof(T).Name} not registered. " +
                    "Ensure the service is initialized before access.");
            }
            
            return service;
        }

        /// <summary>
        /// Try to get a service.
        /// </summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }

        /// <summary>
        /// Check if a service is registered.
        /// </summary>
        public static bool IsRegistered<T>() where T : class
        {
            return services.ContainsKey(typeof(T));
        }

        // ========== Unregistration ==========

        /// <summary>
        /// Unregister a service.
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            var type = typeof(T);
            
            if (services.Remove(type))
            {
                if (EnableLogging)
                    Debug.Log($"ServiceLocator: Unregistered {type.Name}");
            }
        }

        /// <summary>
        /// Clear all registered services.
        /// Call on scene unload or game shutdown.
        /// </summary>
        public static void Clear()
        {
            if (EnableLogging)
                Debug.Log($"ServiceLocator: Clearing {services.Count} services");
            
            services.Clear();
        }

        // ========== Debugging ==========

        /// <summary>
        /// Get count of registered services.
        /// </summary>
        public static int ServiceCount => services.Count;

        /// <summary>
        /// Log all registered services (debug utility).
        /// </summary>
        public static void LogRegisteredServices()
        {
            Debug.Log($"=== ServiceLocator: {services.Count} registered services ===");
            foreach (var kvp in services)
            {
                Debug.Log($"  - {kvp.Key.Name}: {kvp.Value?.GetType().Name ?? "null"}");
            }
        }
    }

    /// <summary>
    /// Base class for MonoBehaviour services that auto-register with ServiceLocator.
    /// 
    /// IMPORTANT: This class does NOT call DontDestroyOnLoad.
    /// For persistence, place service GameObjects as children of PersistentServices.
    /// The PersistentServices root handles DontDestroyOnLoad for the entire hierarchy.
    /// 
    /// Usage:
    /// 1. Create your service class inheriting from ServiceBase&lt;YourService&gt;
    /// 2. Place the GameObject as a child of [Services] in the hierarchy
    /// 3. Access via ServiceLocator.Get&lt;YourService&gt;() or YourService.Instance
    /// </summary>
    public abstract class ServiceBase<T> : MonoBehaviour where T : ServiceBase<T>
    {
        /// <summary>
        /// Legacy singleton access (for backwards compatibility).
        /// Prefer using ServiceLocator.Get&lt;T&gt;() for new code.
        /// </summary>
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{typeof(T).Name}: Duplicate instance destroyed");
                Destroy(gameObject);
                return;
            }
            
            Instance = (T)this;
            ServiceLocator.Register<T>((T)this);
            
            // NOTE: We intentionally do NOT call DontDestroyOnLoad here.
            // Persistence is managed by the parent PersistentServices object.
            // This prevents the "DontDestroyOnLoad only works for root GameObjects" warning.
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<T>();
            }
        }
    }
}
