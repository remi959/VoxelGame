using Assets.Scripts.Core;
using UnityEngine;

namespace Assets.Scripts.World
{
    /// <summary>
    /// Abstract base for any world object that can be worked on over time.
    ///
    /// Tracks work progress and signals completion. The work duration is a
    /// property of the object (configurable in the Inspector), not the job —
    /// a granite boulder takes longer to mine than sandstone, regardless of
    /// which pawn is working it.
    ///
    /// ## How It Connects to Jobs
    ///
    /// JobDrivers call ApplyWork() each frame during their OnWork() phase.
    /// When WorkApplied reaches WorkRequired, IsFinished becomes true and
    /// OnWorkCompleted() fires. The driver checks IsFinished and sets
    /// IsComplete on itself.
    ///
    /// WorkGivers skip objects where IsFinished is true, so completed objects
    /// are never assigned again.
    ///
    /// ## Subclass Contract
    ///
    /// Override OnWorkCompleted() to define what "finished" means for this
    /// object type: destroy itself, drop resources, enter a regrow timer, etc.
    /// </summary>
    public abstract class Workable : MonoBehaviour
    {
        [Header("Work")]
        [Tooltip("Total work units required to complete this object.")]
        [SerializeField] private float workRequired = 3f;

        public float WorkRequired => workRequired;
        public float WorkApplied { get; private set; }
        public bool IsFinished { get; private set; }

        /// <summary>
        /// Apply work to this object. Called by JobDrivers each frame.
        /// When enough work has been applied, IsFinished becomes true
        /// and OnWorkCompleted() is called.
        /// </summary>
        public void ApplyWork(float amount)
        {
            if (IsFinished) return;

            WorkApplied += amount;

            if (WorkApplied >= workRequired)
            {
                IsFinished = true;
                DebugManager.LogJob($"{gameObject.name} work completed ({workRequired:F1}s)");
                OnWorkCompleted();
            }
        }

        /// <summary>
        /// Called once when work is fully applied. Override to define
        /// what happens: destroy, drop loot, regrow, etc.
        /// </summary>
        protected abstract void OnWorkCompleted();
    }
}
