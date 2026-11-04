namespace PalworldRandomizer.ViewModels;

/// <summary>Tracks scheduled startup and gates UI initialization through one error route.</summary>
public sealed class StartupCompletion(Task<Task> operation) {
    /// <summary>The full startup task, including asynchronous provider disposal and image saves.</summary>
    public Task Completion { get; } = operation.Unwrap();

    /// <summary>Initializes controls only after startup succeeds, retaining the caller's UI context.</summary>
    public async Task<bool> InitializeAsync(Action initializeControls, Action<Exception> reportError) {
        try {
            await Completion;
            initializeControls();
            return true;
        } catch (Exception exception) {
            reportError(exception);
            return false;
        }
    }
}