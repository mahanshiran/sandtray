namespace Sandplay.Core
{
    // Sand tool events
    public struct SandModifiedEvent
    {
        public int CenterX;
        public int CenterZ;
        public int Radius;
    }

    /// <summary>Fired by SandMaterialController.PaintAt() after modifying the splatmap.</summary>
    public struct SplatPaintedEvent
    {
        public int MinX;   // pixel column start (0 – SplatResolution-1)
        public int MinY;   // pixel row start
        public int Width;
        public int Height;
    }

    // Object events
    public struct ObjectPlacedEvent
    {
        public Objects.PlacedObject PlacedObject;
    }

    public struct ObjectSelectedEvent
    {
        public Objects.PlacedObject PlacedObject; // null = deselected
    }

    public struct ObjectRemovedEvent
    {
        public Objects.PlacedObject PlacedObject;
    }

    public struct ObjectTransformedEvent
    {
        public Objects.PlacedObject PlacedObject;
    }

    // Session events
    public struct SessionSavedEvent
    {
        public string SessionName;
    }

    public struct SessionLoadedEvent
    {
        public string SessionName;
    }

    // Tool mode events
    public enum ToolMode
    {
        None,
        SandRaise,
        SandDig,
        SandSmooth,
        SandFlatten,
        SandPaint,
        ObjectPlace,
        ObjectSelect,
        ObjectMove,
        ObjectRotate,
        ObjectScale,
        WalkMode,
        SandDraw // Append to preserve existing serialized tool values.
    }

    public struct ToolModeChangedEvent
    {
        public ToolMode NewMode;
        public ToolMode PreviousMode;
    }

    // UI events
    public struct CatalogObjectSelectedEvent
    {
        public Objects.SandplayObject ObjectData;
    }

    // Analysis events
    public struct AnalysisRequestedEvent { }
    public struct AnalysisCompletedEvent
    {
        public string ResultJson;
    }

    // Network roles
    public enum PlayerRole
    {
        Patient,      // Full sandbox control
        Psychologist, // Can select objects only (for therapy discussion)
        Observer      // View only
    }

    // Network events
    public struct NetworkRoleAssignedEvent
    {
        public PlayerRole Role;
    }

    public struct NetworkConnectedEvent { }
    public struct NetworkDisconnectedEvent { }
}
