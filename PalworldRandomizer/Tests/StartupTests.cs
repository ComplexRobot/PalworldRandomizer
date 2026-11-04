using System.Reflection;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.ViewModels;

// Calls the production completion/consumer gate with a real provider's pending image-save disposal.
internal static class StartupTests {
    internal static async Task<int> RunAsync() {
        (string Name, Func<Task> Run)[] tests = [
            ("Pending archive opening prevents all consumer access and reveal", () => PendingBoundary(true)),
            ("Pending image saving prevents all consumer access and reveal", () => PendingBoundary(false)),
            ("Archive opening failure stays faulted and reaches the error route once", () => Failure(false)),
            ("Image saving failure stays faulted and reaches the error route once", () => Failure(true)),
            ("Control initialization failure reaches the same error route once", ControlFailure),
            ("Cached synchronous startup initializes the consumer once", CachedStartup),
        ];
        int failures = 0;
        foreach (var (name, run) in tests) {
            try {
                await run();
                Console.WriteLine($"PASS {name}");
            } catch (Exception exception) {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} startup checks passed.");
        return failures;
    }

    private static async Task PendingBoundary(bool pendingOpen) {
        var open = new TaskCompletionSource<VfsFileProvider>();
        var images = new TaskCompletionSource();
        var provider = new VfsFileProvider();
        QueueImageSave(provider, images.Task);
        bool dataLoaded = false;
        async Task LoadAsync() {
            await using var owned = await open.Task;
            dataLoaded = true;
        }

        Task work = LoadAsync();
        // The scheduler has returned the callback's Task, but that Task is deliberately still pending.
        var startup = new StartupCompletion(Task.FromResult(work));
        int reads = 0;
        bool visible = false;
        int errors = 0;
        Task<bool> consumer = startup.InitializeAsync(() => {
            Require(dataLoaded && images.Task.IsCompletedSuccessfully, "Consumer read data/images too early.");
            reads++;
            visible = true;
        }, _ => errors++);
        try {
            if (pendingOpen) {
                Require(!startup.Completion.IsCompleted && !consumer.IsCompleted && !dataLoaded,
                    "Startup completed while archive opening was pending.");
                Require(reads == 0 && !visible && errors == 0, "Pending open reached the consumer/error route.");
            }

            open.SetResult(provider);
            Require(dataLoaded && !work.IsCompleted, "Fixture must reach the real pending provider disposal.");
            Require(!startup.Completion.IsCompleted && !consumer.IsCompleted,
                "Startup completed while image saving was pending.");
            Require(reads == 0 && !visible && errors == 0, "Pending images reached the consumer/error route.");
            images.SetResult();
            Require(await consumer.WaitAsync(TimeSpan.FromSeconds(5)),
                "Completed startup did not initialize controls.");
            await startup.Completion;
            Require(reads == 1 && visible && errors == 0, "Successful completion must initialize/reveal exactly once.");
        } finally {
            open.TrySetResult(provider);
            images.TrySetResult();
            await work;
        }
    }

    private static async Task Failure(bool imageFailure) {
        var expected = new IOException(imageFailure ? "image save failed" : "archive open failed");
        var open = new TaskCompletionSource<VfsFileProvider>();
        var images = new TaskCompletionSource();
        var provider = new VfsFileProvider();
        QueueImageSave(provider, images.Task);
        async Task LoadAsync() {
            await using var owned = await open.Task;
        }

        Task work = LoadAsync();
        var startup = new StartupCompletion(Task.FromResult(work));
        int reads = 0;
        bool visible = false;
        List<Exception> errors = [];
        Task<bool> consumer = startup.InitializeAsync(() => {
            reads++;
            visible = true;
        }, errors.Add);
        try {
            if (imageFailure) {
                open.SetResult(provider);
                images.SetException(expected);
            } else {
                open.SetException(expected);
                images.SetResult();
            }

            Require(!await consumer.WaitAsync(TimeSpan.FromSeconds(5)), "Failure was reported as ready.");
            Require(startup.Completion.IsFaulted, "Startup failure must remain faulted after reporting.");
            Require(reads == 0 && !visible, "Failed startup accessed game data or revealed controls.");
            Require(errors.Count == 1 && ReferenceEquals(errors[0], expected),
                "The original failure must reach the existing error route exactly once.");
        } finally {
            open.TrySetResult(provider);
            images.TrySetResult();
            try {
                await work;
            } catch (IOException exception) when (ReferenceEquals(exception, expected)) { }
        }
    }

    private static async Task ControlFailure() {
        var expected = new InvalidOperationException("template restore failed");
        var startup = new StartupCompletion(Task.FromResult(Task.CompletedTask));
        List<Exception> errors = [];
        bool ready = await startup.InitializeAsync(() => throw expected, errors.Add);
        Require(!ready && errors.Count == 1 && ReferenceEquals(errors[0], expected),
            "UI restoration failures must report once, without marking the page ready.");
    }

    private static async Task CachedStartup() {
        Require(typeof(StartupCompletion).Assembly == typeof(VfsFileProvider).Assembly,
            "Tests must exercise the application's real completion gate, not a test-local copy.");
        var startup = new StartupCompletion(Task.FromResult(Task.CompletedTask));
        int reads = 0;
        int errors = 0;
        Require(await startup.InitializeAsync(() => reads++, _ => errors++), "Cached startup did not become ready.");
        Require(startup.Completion.IsCompletedSuccessfully && reads == 1 && errors == 0,
            "Cached startup must keep the same initialization path and single read.");
    }

    private static void QueueImageSave(VfsFileProvider provider, Task imageSave) =>
        ((List<Task>)typeof(VfsFileProvider).GetField("_fileSaveTasks", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(provider)!).Add(imageSave);

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }
}