// ============================================================================
// Events/ReservationEvent.cs - Events for resource reservations
// ============================================================================
namespace Assets.Scripts.Economy.Events
{
    /// <summary>
    /// Fired when a resource reservation is created.
    /// UI can show "pending" costs.
    /// </summary>
    public struct ReservationCreatedEvent
    {
        public string ReservationId;
        public int PlayerId;
        public float ExpiresAt;
    }

    /// <summary>
    /// Fired when a reservation is confirmed (resources actually spent).
    /// </summary>
    public struct ReservationConfirmedEvent
    {
        public string ReservationId;
        public int PlayerId;
    }

    /// <summary>
    /// Fired when a reservation is cancelled (resources released).
    /// </summary>
    public struct ReservationCancelledEvent
    {
        public string ReservationId;
        public int PlayerId;
        public ReservationCancelReason Reason;
    }

    /// <summary>
    /// Why the reservation was cancelled.
    /// </summary>
    public enum ReservationCancelReason
    {
        PlayerCancelled,    // Player cancelled the action
        Expired,            // Reservation timed out
        InvalidState,       // Game state made reservation invalid
        SystemError         // Something went wrong
    }
}