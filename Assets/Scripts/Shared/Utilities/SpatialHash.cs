// ============================================================================
// SpatialHash.cs - Efficient spatial queries without physics
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Shared.Utilities
{
    /// <summary>
    /// Spatial hash grid for efficient position-based queries.
    /// 
    /// Why this exists:
    /// - Physics.OverlapBox/Sphere allocates arrays every call
    /// - Physics queries involve broad-phase, narrow-phase, and callbacks
    /// - For simple "find nearby buildings" queries, spatial hashing is 10-100x faster
    /// 
    /// How it works:
    /// - World is divided into cells of configurable size
    /// - Each entity registers to cell(s) it occupies
    /// - Queries only check relevant cells
    /// 
    /// Performance:
    /// - Insert: O(1)
    /// - Remove: O(1)
    /// - Query: O(k) where k is items in nearby cells
    /// - Memory: O(n) where n is number of items
    /// 
    /// Usage:
    ///   var hash = new SpatialHash&lt;Building&gt;(10f);  // 10m cells
    ///   hash.Insert(building, building.transform.position);
    ///   var nearby = hash.Query(position, radius);
    /// </summary>
    public class SpatialHash<T> where T : class
    {
        private readonly float cellSize;
        private readonly float inverseCellSize;
        private readonly Dictionary<long, List<SpatialEntry>> cells = new();
        private readonly Dictionary<T, SpatialEntry> itemToEntry = new();
        
        // Reusable list for query results (avoids allocation)
        private readonly List<T> queryResults = new();
        
        /// <summary>
        /// Entry storing an item and its position.
        /// </summary>
        private class SpatialEntry
        {
            public T Item;
            public Vector3 Position;
            public long CellKey;
        }

        /// <summary>
        /// Create a new spatial hash.
        /// </summary>
        /// <param name="cellSize">Size of each cell. Smaller = more cells, faster queries. Larger = fewer cells, less memory.</param>
        public SpatialHash(float cellSize = 10f)
        {
            this.cellSize = cellSize;
            this.inverseCellSize = 1f / cellSize;
        }

        /// <summary>
        /// Number of items in the hash.
        /// </summary>
        public int Count => itemToEntry.Count;

        // ========== Insert/Update/Remove ==========

        /// <summary>
        /// Insert or update an item's position.
        /// </summary>
        public void Insert(T item, Vector3 position)
        {
            if (item == null) return;

            // If already exists, check if cell changed
            if (itemToEntry.TryGetValue(item, out var existingEntry))
            {
                long newKey = GetCellKey(position);
                if (existingEntry.CellKey == newKey)
                {
                    // Same cell, just update position
                    existingEntry.Position = position;
                    return;
                }
                
                // Cell changed, remove from old cell
                RemoveFromCell(existingEntry);
            }

            // Create new entry
            long cellKey = GetCellKey(position);
            var entry = new SpatialEntry
            {
                Item = item,
                Position = position,
                CellKey = cellKey
            };

            // Add to cell
            if (!cells.TryGetValue(cellKey, out var cell))
            {
                cell = new List<SpatialEntry>(4);
                cells[cellKey] = cell;
            }
            cell.Add(entry);

            // Track item
            itemToEntry[item] = entry;
        }

        /// <summary>
        /// Remove an item from the hash.
        /// </summary>
        public bool Remove(T item)
        {
            if (item == null) return false;

            if (itemToEntry.TryGetValue(item, out var entry))
            {
                RemoveFromCell(entry);
                itemToEntry.Remove(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if an item is in the hash.
        /// </summary>
        public bool Contains(T item)
        {
            return item != null && itemToEntry.ContainsKey(item);
        }

        /// <summary>
        /// Clear all items.
        /// </summary>
        public void Clear()
        {
            cells.Clear();
            itemToEntry.Clear();
        }

        // ========== Queries ==========

        /// <summary>
        /// Query all items within radius of position.
        /// Returns shared list - do not store reference!
        /// </summary>
        public List<T> Query(Vector3 position, float radius)
        {
            queryResults.Clear();
            
            float radiusSqr = radius * radius;
            
            // Calculate cell range to check
            int minX = Mathf.FloorToInt((position.x - radius) * inverseCellSize);
            int maxX = Mathf.FloorToInt((position.x + radius) * inverseCellSize);
            int minZ = Mathf.FloorToInt((position.z - radius) * inverseCellSize);
            int maxZ = Mathf.FloorToInt((position.z + radius) * inverseCellSize);

            // Check each cell in range
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    long key = GetCellKey(x, z);
                    if (cells.TryGetValue(key, out var cell))
                    {
                        foreach (var entry in cell)
                        {
                            // Use sqrMagnitude on XZ plane for 2D distance
                            float dx = entry.Position.x - position.x;
                            float dz = entry.Position.z - position.z;
                            float distSqr = dx * dx + dz * dz;
                            
                            if (distSqr <= radiusSqr)
                            {
                                queryResults.Add(entry.Item);
                            }
                        }
                    }
                }
            }

            return queryResults;
        }

        /// <summary>
        /// Query all items within radius, with distance.
        /// </summary>
        public void Query(Vector3 position, float radius, List<(T item, float distance)> results)
        {
            results.Clear();
            
            float radiusSqr = radius * radius;
            
            int minX = Mathf.FloorToInt((position.x - radius) * inverseCellSize);
            int maxX = Mathf.FloorToInt((position.x + radius) * inverseCellSize);
            int minZ = Mathf.FloorToInt((position.z - radius) * inverseCellSize);
            int maxZ = Mathf.FloorToInt((position.z + radius) * inverseCellSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    long key = GetCellKey(x, z);
                    if (cells.TryGetValue(key, out var cell))
                    {
                        foreach (var entry in cell)
                        {
                            float dx = entry.Position.x - position.x;
                            float dz = entry.Position.z - position.z;
                            float distSqr = dx * dx + dz * dz;
                            
                            if (distSqr <= radiusSqr)
                            {
                                results.Add((entry.Item, Mathf.Sqrt(distSqr)));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find nearest item to position within maxDistance.
        /// </summary>
        public T FindNearest(Vector3 position, float maxDistance = float.MaxValue)
        {
            return FindNearest(position, out _, maxDistance);
        }

        /// <summary>
        /// Find nearest item to position, also returning distance.
        /// </summary>
        public T FindNearest(Vector3 position, out float distance, float maxDistance = float.MaxValue)
        {
            distance = float.MaxValue;
            T nearest = null;
            float nearestDistSqr = maxDistance * maxDistance;

            // Start with current cell, expand outward
            int centerX = Mathf.FloorToInt(position.x * inverseCellSize);
            int centerZ = Mathf.FloorToInt(position.z * inverseCellSize);
            
            // How many cells to check (based on maxDistance)
            int cellRange = Mathf.CeilToInt(maxDistance * inverseCellSize);
            
            for (int dx = -cellRange; dx <= cellRange; dx++)
            {
                for (int dz = -cellRange; dz <= cellRange; dz++)
                {
                    long key = GetCellKey(centerX + dx, centerZ + dz);
                    if (cells.TryGetValue(key, out var cell))
                    {
                        foreach (var entry in cell)
                        {
                            float ex = entry.Position.x - position.x;
                            float ez = entry.Position.z - position.z;
                            float distSqr = ex * ex + ez * ez;
                            
                            if (distSqr < nearestDistSqr)
                            {
                                nearestDistSqr = distSqr;
                                nearest = entry.Item;
                            }
                        }
                    }
                }
            }

            if (nearest != null)
            {
                distance = Mathf.Sqrt(nearestDistSqr);
            }

            return nearest;
        }

        /// <summary>
        /// Check if any item exists within radius.
        /// More efficient than Query when you only need existence check.
        /// </summary>
        public bool AnyWithinRadius(Vector3 position, float radius)
        {
            float radiusSqr = radius * radius;
            
            int minX = Mathf.FloorToInt((position.x - radius) * inverseCellSize);
            int maxX = Mathf.FloorToInt((position.x + radius) * inverseCellSize);
            int minZ = Mathf.FloorToInt((position.z - radius) * inverseCellSize);
            int maxZ = Mathf.FloorToInt((position.z + radius) * inverseCellSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    long key = GetCellKey(x, z);
                    if (cells.TryGetValue(key, out var cell))
                    {
                        foreach (var entry in cell)
                        {
                            float dx = entry.Position.x - position.x;
                            float dz = entry.Position.z - position.z;
                            float distSqr = dx * dx + dz * dz;
                            
                            if (distSqr <= radiusSqr)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        // ========== Internal ==========

        private long GetCellKey(Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x * inverseCellSize);
            int z = Mathf.FloorToInt(position.z * inverseCellSize);
            return GetCellKey(x, z);
        }

        private long GetCellKey(int x, int z)
        {
            // Pack two ints into a long for dictionary key
            return ((long)x << 32) | (uint)z;
        }

        private void RemoveFromCell(SpatialEntry entry)
        {
            if (cells.TryGetValue(entry.CellKey, out var cell))
            {
                cell.Remove(entry);
                if (cell.Count == 0)
                {
                    cells.Remove(entry.CellKey);
                }
            }
        }
    }
}
