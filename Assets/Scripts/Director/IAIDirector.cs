namespace ZombieFPS
{
    /// <summary>
    /// Entry point of the AI Director.
    /// Implement this interface on a MonoBehaviour and add it to the "AI Director" GameObject
    /// (the one holding the DirectorRunner). The first enabled implementation found is used.
    /// </summary>
    public interface IAIDirector
    {
        /// <summary>
        /// Called once when the run starts.
        /// Good place to initialise state and subscribe to <see cref="DirectorContext.Events"/>.
        /// </summary>
        void OnGameStart(DirectorContext context, DirectorActions actions);

        /// <summary>
        /// Called every <see cref="DirectorRunner.TickInterval"/> seconds while the game is running.
        /// The context is refreshed right before this call.
        /// </summary>
        /// <param name="deltaTime">Seconds elapsed since the previous Tick.</param>
        void Tick(DirectorContext context, DirectorActions actions, float deltaTime);
    }
}
