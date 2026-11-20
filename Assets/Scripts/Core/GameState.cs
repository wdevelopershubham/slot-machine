namespace SlotGame.Core
{
    /// <summary>High-level phases of the game loop. Input is only accepted in <see cref="Idle"/>.</summary>
    public enum GameState
    {
        Idle,
        Spinning,
        Resolving,
        GameOver
    }
}
