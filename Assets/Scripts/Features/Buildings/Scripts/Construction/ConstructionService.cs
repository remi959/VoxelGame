// ============================================================================
// ConstructionService.cs - Central construction management
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Economy;
using Assets.Scripts.Events;
using UnityEngine;

namespace Assets.Scripts.Buildings.Construction
{
    /// <summary>
    /// Central service for managing all building construction.
    /// 
    /// Responsibilities:
    /// - Create and track building sites
    /// - Manage construction state transitions
    /// - Broadcast progress events
    /// - Handle completion and cancellation
    /// 
    /// Does NOT:
    /// - Handle placement validation (that's ValidationService)
    /// - Handle worker AI (that's WorkQueueService + states)
    /// - Render anything (that's the MonoBehaviours)
    /// 
    /// PERSISTENCE:
    /// This component should be placed as a child of the [Services] GameObject.
    /// PersistentServices handles DontDestroyOnLoad for the entire hierarchy.
    /// </summary>
    public class ConstructionService : MonoBehaviour
    {
        public static ConstructionService Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject defaultBlueprintPrefab;

        // All active building sites
        private readonly Dictionary<string, BuildingSite> activeSites = new();

        // Cached lists for non-allocating queries (reused each frame)
        private readonly List<BuildingSite> cachedSitesNeedingWorkers = new();
        private readonly List<BuildingSite> cachedPlayerSites = new();
        private bool sitesNeedingWorkersDirty = true;

        // Site ID generator
        private int nextSiteId = 1;

        // Events
        public event Action<BuildingSite> OnSiteCreated;
        public event Action<BuildingSite> OnSiteStateChanged;
        public event Action<BuildingSite> OnSiteProgressChanged;
        public event Action<BuildingSite, GameObject> OnSiteCompleted;
        public event Action<BuildingSite> OnSiteCancelled;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<ConstructionService>();
            }
        }

        // ========== Site Creation ==========

        /// <summary>
        /// Create a new building site.
        /// Called by PlacementService after validation and resource reservation.
        /// </summary>
        public BuildingSite CreateBuildingSite(
            BuildingDefinitionSO definition,
            Vector3 position,
            Quaternion rotation,
            int playerId,
            ResourceReservation reservation)
        {
            if (definition == null || !definition.IsValid)
            {
                Debug.LogError("ConstructionService: Invalid building definition");
                return null;
            }

            // Generate unique ID
            string siteId = $"site_{nextSiteId++}_{definition.buildingId}";

            // Create site data
            var site = new BuildingSite(
                siteId,
                definition,
                playerId,
                position,
                rotation,
                reservation
            );

            // Create scene object
            GameObject sceneObject = CreateBlueprintObject(site);
            site.SceneObject = sceneObject;

            // Initialize part count from the blueprint
            if (sceneObject.TryGetComponent<BuildingBlueprint>(out var blueprint))
            {
                site.SetTotalParts(blueprint.TotalParts);
            }

            // Subscribe to site events
            site.OnStateChanged += (oldState, newState) => HandleSiteStateChanged(site, oldState, newState);
            site.OnProgressChanged += (progress) => HandleSiteProgressChanged(site, progress);

            // If reservation exists, confirm it and transition state
            // Reservation will be confirmed when first worker arrives
            if (reservation != null) site.SetState(BuildingSiteState.ResourcesReserved);

            // Register site
            activeSites[siteId] = site;

            // Broadcast creation
            OnSiteCreated?.Invoke(site);
            EventBus.Publish(new BuildingSiteCreatedEvent
            {
                SiteId = siteId,
                Definition = definition,
                Position = position,
                PlayerId = playerId
            });

            Debug.Log($"ConstructionService: Created site {siteId} for {definition.displayName}");

            return site;
        }

        private GameObject CreateBlueprintObject(BuildingSite site)
        {
            // Create the blueprint object
            GameObject blueprintObj = new($"Blueprint_{site.Definition.displayName}_{site.SiteId}");
            blueprintObj.transform.SetPositionAndRotation(site.Position, site.Rotation);
            blueprintObj.layer = LayerMask.NameToLayer("Interactable");

            // Add collider
            var collider = blueprintObj.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                site.Definition.Footprint.x,
                site.Definition.height,
                site.Definition.Footprint.y
            );
            collider.center = Vector3.up * (site.Definition.height / 2f);

            // Add BuildingBlueprint component
            var blueprint = blueprintObj.AddComponent<BuildingBlueprint>();

            // Initialize with the definition's prefab data
            blueprint.Initialize(site.Definition);

            // Link blueprint to site for unified tracking
            blueprint.LinkToSite(site.SiteId);

            return blueprintObj;
        }

        // ========== Site Queries ==========

        /// <summary>
        /// Get a site by its ID.
        /// </summary>
        public BuildingSite GetSite(string siteId) => activeSites.TryGetValue(siteId, out var site) ? site : null;

        /// <summary>
        /// Get all active sites for a player.
        /// Returns cached list - do not store reference!
        /// </summary>
        public IReadOnlyList<BuildingSite> GetSitesForPlayer(int playerId)
        {
            cachedPlayerSites.Clear();
            foreach (var site in activeSites.Values)
                if (site.OwnerPlayerId == playerId && !site.IsComplete)
                    cachedPlayerSites.Add(site);

            return cachedPlayerSites;
        }

        /// <summary>
        /// Get all sites under construction (for work queue).
        /// Returns cached list - do not store reference!
        /// Non-allocating: reuses internal list.
        /// </summary>
        public IReadOnlyList<BuildingSite> GetSitesNeedingWorkers()
        {
            // Only rebuild if marked dirty
            if (sitesNeedingWorkersDirty) RebuildSitesNeedingWorkers();

            return cachedSitesNeedingWorkers;
        }

        /// <summary>
        /// Fill provided list with sites needing workers.
        /// Zero allocation version for frequent queries.
        /// </summary>
        public void GetSitesNeedingWorkers(List<BuildingSite> results)
        {
            results.Clear();
            foreach (var site in activeSites.Values)
            {
                if ((site.State == BuildingSiteState.ResourcesReserved ||
                     site.State == BuildingSiteState.UnderConstruction) &&
                    site.CanAssignWorker)
                {
                    results.Add(site);
                }
            }
        }

        /// <summary>
        /// Mark the cached sites list as needing rebuild.
        /// Called when sites are added, removed, or state changes.
        /// </summary>
        public void InvalidateSitesCache() => sitesNeedingWorkersDirty = true;

        private void RebuildSitesNeedingWorkers()
        {
            cachedSitesNeedingWorkers.Clear();
            foreach (var site in activeSites.Values)
            {
                if ((site.State == BuildingSiteState.ResourcesReserved ||
                     site.State == BuildingSiteState.UnderConstruction) &&
                    site.CanAssignWorker)
                {
                    cachedSitesNeedingWorkers.Add(site);
                }
            }
            sitesNeedingWorkersDirty = false;
        }

        /// <summary>
        /// Find nearest site needing workers.
        /// </summary>
        public BuildingSite FindNearestSiteNeedingWorkers(Vector3 position, int playerId)
        {
            BuildingSite nearest = null;
            float nearestDistSqr = float.MaxValue;

            foreach (var site in GetSitesNeedingWorkers())
            {
                if (site.OwnerPlayerId != playerId) continue;

                float distSqr = (site.Position - position).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestDistSqr = distSqr;
                    nearest = site;
                }
            }

            return nearest;
        }

        // ========== Site Actions ==========

        /// <summary>
        /// Cancel a building site (before or during construction).
        /// </summary>
        public bool CancelSite(string siteId)
        {
            if (!activeSites.TryGetValue(siteId, out var site)) return false;

            if (site.State == BuildingSiteState.Complete || site.State == BuildingSiteState.Cancelled) return false;

            // Cancel resource reservation if not yet confirmed
            if (site.Reservation != null && !site.Reservation.IsConfirmed) EconomyService.Instance.CancelReservation(site.Reservation.Id);

            // TODO: If resources were already confirmed, calculate partial refund

            site.SetState(BuildingSiteState.Cancelled);

            // Destroy scene object
            if (site.SceneObject != null) Destroy(site.SceneObject);

            // Remove from active sites
            activeSites.Remove(siteId);

            // Broadcast cancellation
            OnSiteCancelled?.Invoke(site);
            EventBus.Publish(new BuildingCancelledEvent
            {
                SiteId = siteId,
                Definition = site.Definition,
                PlayerId = site.OwnerPlayerId,
                ResourcesRefunded = site.Reservation != null && !site.Reservation.IsConfirmed,
                RefundPercentage = site.Reservation != null && !site.Reservation.IsConfirmed ? 1.0f : 0f
            });

            Debug.Log($"ConstructionService: Cancelled site {siteId}");

            return true;
        }

        /// <summary>
        /// Cancel a building site with full resource refund.
        /// This is the preferred method for user-initiated cancellation.
        /// 
        /// Refund Logic:
        /// - If reservation not confirmed: Full refund (reservation cancelled)
        /// - If reservation confirmed: Refund resources that were delivered to site
        /// 
        /// Also handles:
        /// - Notifying workers to stop their current tasks
        /// - Cleaning up the scene object
        /// - Broadcasting events
        /// </summary>
        public bool CancelSiteWithRefund(string siteId)
        {
            if (!activeSites.TryGetValue(siteId, out var site)) return false;

            if (site.State == BuildingSiteState.Complete || site.State == BuildingSiteState.Cancelled) return false;

            Debug.Log($"ConstructionService: Cancelling site {siteId} with refund");

            // Calculate refund
            var refundedResources = new Dictionary<Shared.Enums.EResourceType, int>();

            if (site.Reservation != null)
            {
                if (!site.Reservation.IsConfirmed)
                {
                    // Reservation not confirmed - full refund via cancellation
                    EconomyService.Instance.CancelReservation(site.Reservation.Id);
                    
                    // Track what was refunded
                    foreach (var cost in site.Reservation.Costs)
                    {
                        refundedResources[cost.resourceType] = cost.amount;
                    }
                }
                else
                {
                    // Reservation confirmed - refund delivered resources back to storage
                    RefundDeliveredResources(site, refundedResources);
                }
            }

            // Notify workers to stop their tasks
            NotifyWorkersOfCancellation(site);

            // Update state
            site.SetState(BuildingSiteState.Cancelled);

            // Destroy scene object
            if (site.SceneObject != null) Destroy(site.SceneObject);

            // Remove from active sites
            activeSites.Remove(siteId);
            InvalidateSitesCache();

            // Broadcast events
            OnSiteCancelled?.Invoke(site);
            
            EventBus.Publish(new BuildingCancelledEvent
            {
                SiteId = siteId,
                Definition = site.Definition,
                PlayerId = site.OwnerPlayerId,
                ResourcesRefunded = refundedResources.Count > 0,
                RefundPercentage = CalculateRefundPercentage(site, refundedResources)
            });

            EventBus.Publish(new ConstructionCancelledWithRefundEvent
            {
                SiteId = siteId,
                Definition = site.Definition,
                PlayerId = site.OwnerPlayerId,
                RefundedResources = refundedResources
            });

            Debug.Log($"ConstructionService: Cancelled site {siteId} with refund of {refundedResources.Count} resource types");

            return true;
        }

        /// <summary>
        /// Refund resources that were delivered to the construction site back to storage.
        /// </summary>
        private void RefundDeliveredResources(BuildingSite site, Dictionary<Shared.Enums.EResourceType, int> refundedResources)
        {
            foreach (var kvp in site.ResourcesDelivered)
            {
                if (kvp.Value > 0)
                {
                    // Find storage point for this resource type
                    var storage = Resources.StoragePoint.FindNearest(site.Position, kvp.Key, site.OwnerPlayerId);
                    if (storage != null)
                    {
                        storage.Deposit(kvp.Key, kvp.Value);
                        refundedResources[kvp.Key] = kvp.Value;
                        Debug.Log($"ConstructionService: Refunded {kvp.Value} {kvp.Key} to storage");
                    }
                    else
                    {
                        Debug.LogWarning($"ConstructionService: No storage found for {kvp.Key}, {kvp.Value} lost");
                    }
                }
            }
        }

        /// <summary>
        /// Notify all workers assigned to this site to stop their tasks.
        /// Workers will transition to idle state.
        /// </summary>
        private void NotifyWorkersOfCancellation(BuildingSite site)
        {
            // Publish event for workers to handle
            foreach (int workerId in site.AssignedWorkerIds)
            {
                EventBus.Publish(new ConstructionSiteCancelledForWorkerEvent
                {
                    SiteId = site.SiteId,
                    WorkerId = workerId
                });
            }
        }

        /// <summary>
        /// Calculate the refund percentage based on what was delivered vs required.
        /// </summary>
        private float CalculateRefundPercentage(BuildingSite site, Dictionary<Shared.Enums.EResourceType, int> refunded)
        {
            int totalRequired = 0;
            int totalRefunded = 0;

            foreach (var kvp in site.ResourcesRequired)
            {
                totalRequired += kvp.Value;
            }

            foreach (var kvp in refunded)
            {
                totalRefunded += kvp.Value;
            }

            return totalRequired > 0 ? (float)totalRefunded / totalRequired : 0f;
        }

        /// <summary>
        /// Pause construction on a site.
        /// </summary>
        public bool PauseSite(string siteId)
        {
            if (!activeSites.TryGetValue(siteId, out var site)) return false;

            if (site.State != BuildingSiteState.UnderConstruction) return false;

            site.SetState(BuildingSiteState.Paused);
            return true;
        }

        /// <summary>
        /// Resume construction on a paused site.
        /// </summary>
        public bool ResumeSite(string siteId)
        {
            if (!activeSites.TryGetValue(siteId, out var site)) return false;

            if (site.State != BuildingSiteState.Paused) return false;

            site.SetState(BuildingSiteState.UnderConstruction);
            return true;
        }

        // ========== Construction Progress ==========

        /// <summary>
        /// Called when a worker starts working on a site.
        /// Confirms the resource reservation.
        /// </summary>
        public void OnWorkerStartedConstruction(string siteId, int workerId)
        {
            if (!activeSites.TryGetValue(siteId, out var site)) return;

            // Confirm reservation on first worker
            if (site.Reservation != null && !site.Reservation.IsConfirmed)
            {
                EconomyService.Instance.ConfirmReservation(site.Reservation.Id);
                Debug.Log($"ConstructionService: Confirmed resources for {siteId}");
            }

            site.AssignWorker(workerId);
        }

        /// <summary>
        /// Called when a part is placed on a building.
        /// </summary>
        public void OnPartPlaced(string siteId)
        {
            if (!activeSites.TryGetValue(siteId, out var site)) return;

            site.IncrementPartsCompleted();

            EventBus.Publish(new BuildingConstructionProgressEvent
            {
                SiteId = siteId,
                Progress = site.Progress,
                CurrentPhase = site.CurrentPhaseIndex,
                TotalPhases = 1  // TODO: Multi-phase support
            });
        }

        // ========== Event Handlers ==========

        private void HandleSiteStateChanged(BuildingSite site, BuildingSiteState oldState, BuildingSiteState newState)
        {
            OnSiteStateChanged?.Invoke(site);

            EventBus.Publish(new BuildingSiteStateChangedEvent
            {
                SiteId = site.SiteId,
                OldState = oldState,
                NewState = newState
            });

            // Handle completion
            if (newState == BuildingSiteState.Complete) HandleSiteCompleted(site);
        }

        private void HandleSiteProgressChanged(BuildingSite site, float progress)
        {
            OnSiteProgressChanged?.Invoke(site);
        }

        private void HandleSiteCompleted(BuildingSite site)
        {
            Debug.Log($"ConstructionService: {site.SiteId} completed!");

            // Calculate construction duration
            float constructionDuration = Time.time - site.PlacedAt;

            // Spawn completed building (if different from construction)
            GameObject completedBuilding = site.SceneObject;

            if (site.Definition.completedPrefab != null)
            {
                // Destroy blueprint, spawn completed building
                if (site.SceneObject != null)
                {
                    Destroy(site.SceneObject);
                }

                completedBuilding = Instantiate(
                    site.Definition.completedPrefab,
                    site.Position,
                    site.Rotation
                );
                completedBuilding.name = $"Building_{site.Definition.displayName}";
            }

            // Ensure CompletedBuilding component exists for hover UI support
            EnsureCompletedBuildingComponent(completedBuilding, site);

            // Broadcast completion
            OnSiteCompleted?.Invoke(site, completedBuilding);

            EventBus.Publish(new BuildingCompletedEvent
            {
                SiteId = site.SiteId,
                Definition = site.Definition,
                CompletedBuilding = completedBuilding,
                PlayerId = site.OwnerPlayerId,
                ConstructionDuration = constructionDuration
            });

            // Keep site in dictionary for reference (or remove after delay)
            // activeSites.Remove(site.SiteId);
        }

        /// <summary>
        /// Ensures the completed building has a CompletedBuilding component for hover UI.
        /// </summary>
        private void EnsureCompletedBuildingComponent(GameObject building, BuildingSite site)
        {
            if (building == null) return;

            var completedBuilding = building.GetComponent<CompletedBuilding>();
            if (completedBuilding == null)
            {
                completedBuilding = building.AddComponent<CompletedBuilding>();
            }

            // Initialize with site data
            completedBuilding.Initialize(site.Definition, site.OwnerPlayerId, site.Definition?.displayName);
        }
    }
}