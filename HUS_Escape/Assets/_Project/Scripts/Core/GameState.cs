namespace HUSEscape.Core
{
    /// <summary>Describes the application lifecycle without implementing gameplay behavior.</summary>
    public enum GameState
    {
        /// <summary>Core systems have not finished initialization.</summary>
        Initializing = 0,

        /// <summary>Core is ready, without assuming a menu or gameplay scene exists.</summary>
        Ready = 1,

        /// <summary>A scene transition is in progress.</summary>
        Loading = 2,

        /// <summary>The application is in its gameplay state.</summary>
        Playing = 3,

        /// <summary>The application has requested a gameplay pause.</summary>
        Paused = 4
    }
}
