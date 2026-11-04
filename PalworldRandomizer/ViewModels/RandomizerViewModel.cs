using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PalworldRandomizer.Localization;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;

namespace PalworldRandomizer.ViewModels;

/// <summary>One catalog category and whether this bounded build exposes its real settings page.</summary>
/// <param name="Key">The stable category identifier.</param>
/// <param name="Title">The exact English category label.</param>
/// <param name="IsAvailable">Whether this build provides a real page for the category.</param>
public sealed record RandomizerCategory(string Key, string Title, bool IsAvailable);

/// <summary>Owns the first functional Randomizer category and the shared Default template session.</summary>
/// <remarks>
/// The same instance is shared by every tab, so edits cannot diverge between tab copies. Values remain the complete
/// original FormData contract. Labels and descriptions come from SettingsCatalog English text, not new authored text.
/// Numeric bounds are parsed from the catalog source evidence without evaluating WPF expressions. The session never
/// changes FormData or the original serializer algorithms and never writes while merely loading or restoring.
/// </remarks>
public sealed class RandomizerViewModel : ObservableObject {
    private readonly SettingsCatalog _catalog;
    private readonly TemplateStore _store;
    private readonly Dictionary<string, SettingDefinition> _definitions;
    private readonly IReadOnlyDictionary<string, string> _english;
    private string _selectedCategory = RandomizerState.AreaGroupType;
    private bool _dirty;
    private bool _saveRunning;
    private bool _savePending;
    private bool _suppressSave;
    private int _revision;

    private RandomizerViewModel(SettingsCatalog catalog, TemplateStore store) {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _definitions = catalog.Definitions.ToDictionary(definition => definition.Name, StringComparer.Ordinal);
        _english = catalog.English;
        Values = new FormData();
        SpawnListSizeText = Values.SpawnListSize.ToString(CultureInfo.InvariantCulture);
        VanillaPlusChanceText = Values.VanillaPlusChance.ToString(CultureInfo.InvariantCulture);
        Categories = BuildCategories();
        AreaSelections = BuildAreaSelections();
    }

    /// <summary>Reads and validates the Default template before any edit or automatic save can start.</summary>
    public static async Task<RandomizerViewModel> CreateAsync(SettingsCatalog catalog, TemplateStore store) {
        var session = new RandomizerViewModel(catalog, store);
        await session.InitializeAsync().ConfigureAwait(true);
        return session;
    }

    /// <summary>Gets the complete live FormData values shared across tabs.</summary>
    public FormData Values { get; private set; }

    /// <summary>Gets the stable page title.</summary>
    public string PageTitle => EnglishMetadata.Get("Shell.Page.Randomizer");

    /// <summary>Gets the exact English title of the selected category.</summary>
    public string CategoryTitle => Categories.FirstOrDefault(category =>
        string.Equals(category.Key, _selectedCategory, StringComparison.Ordinal))?.Title
        ?? EnglishText("category.AreaGroupType", "Area Group Type");

    /// <summary>Gets the truthful template file name.</summary>
    public string TemplateName => _store.Name;

    /// <summary>Gets the complete catalog category list, including unavailable categories.</summary>
    public IReadOnlyList<RandomizerCategory> Categories { get; }

    /// <summary>Gets whether the Area Group Type category is selected.</summary>
    public bool IsAreaGroupTypeSelected => string.Equals(_selectedCategory,
        RandomizerState.AreaGroupType, StringComparison.Ordinal);

    /// <summary>Gets whether the Area Selections category is selected.</summary>
    public bool IsAreaSelectionsSelected => string.Equals(_selectedCategory,
        RandomizerState.AreaSelections, StringComparison.Ordinal);

    /// <summary>Gets the real Area Selections rows in original catalog order.</summary>
    public IReadOnlyList<AreaSelectionViewModel> AreaSelections { get; }

    /// <summary>Gets or selects an implemented category; unavailable categories are ignored.</summary>
    public string SelectedCategory {
        get => _selectedCategory;
        set {
            if (!Categories.Any(category => category.IsAvailable
                && string.Equals(category.Key, value, StringComparison.Ordinal))
                || string.Equals(_selectedCategory, value, StringComparison.Ordinal)) {
                return;
            }

            _selectedCategory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CategoryTitle));
            OnPropertyChanged(nameof(IsAreaGroupTypeSelected));
            OnPropertyChanged(nameof(IsAreaSelectionsSelected));
        }
    }

    /// <summary>Gets whether loading produced usable values and editing may begin.</summary>
    public bool IsEditingEnabled { get; private set; }

    /// <summary>Gets whether the page has a visible load or save failure.</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>Gets the exact failure detail shown without replacing live work.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets the truthful current save state.</summary>
    public string SaveStatus { get; private set; } = "Not loaded";

    /// <summary>Gets whether any valid edit is not yet durably saved.</summary>
    public bool HasPendingSave => _dirty || _saveRunning || _savePending;

    /// <summary>Gets the exact raw custom-group-count text, which may be temporarily invalid.</summary>
    public string SpawnListSizeText { get; private set; } = string.Empty;

    /// <summary>Gets the exact raw vanilla-add percentage text, which may be temporarily invalid.</summary>
    public string VanillaPlusChanceText { get; private set; } = string.Empty;

    /// <summary>Gets the source-evidenced custom-group-count lower bound.</summary>
    public int SpawnListSizeMinimum => NumericBound("SpawnListSize", "MinimumExpression", 1);

    /// <summary>Gets the source-evidenced custom-group-count invalid-input fallback.</summary>
    public int SpawnListSizeFallback => NumericBound("SpawnListSize", "FallbackExpression", 50);

    /// <summary>Gets the source-evidenced vanilla-add percentage lower bound.</summary>
    public int VanillaPlusChanceMinimum => NumericBound("VanillaPlusChance", "MinimumExpression", 1);

    /// <summary>Gets the source-evidenced vanilla-add percentage upper bound.</summary>
    public int VanillaPlusChanceMaximum => NumericBound("VanillaPlusChance", "MaximumExpression", 99);

    /// <summary>Gets the source-evidenced vanilla-add percentage invalid-input fallback.</summary>
    public int VanillaPlusChanceFallback => NumericBound("VanillaPlusChance", "FallbackExpression", 50);

    /// <summary>Gets or changes whether every permitted species appears in every area.</summary>
    public bool MethodFull {
        get => Values.MethodFull;
        set => SetMethod(value, nameof(MethodFull));
    }

    /// <summary>Gets or changes whether each area uses an exact custom group count.</summary>
    public bool MethodCustomSize {
        get => Values.MethodCustomSize;
        set => SetMethod(value, nameof(MethodCustomSize));
    }

    /// <summary>Gets or changes the custom group count committed to FormData.</summary>
    public int SpawnListSize {
        get => Values.SpawnListSize;
        set {
            if (Values.SpawnListSize == value) {
                return;
            }

            Values.SpawnListSize = value;
            SpawnListSizeText = value.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpawnListSizeText));
            QueueSave();
        }
    }

    /// <summary>Gets or changes whether generated areas retain their vanilla group count.</summary>
    public bool MethodLocalSwap {
        get => Values.MethodLocalSwap;
        set => SetMethod(value, nameof(MethodLocalSwap));
    }

    /// <summary>Gets or changes whether species replacements are shared across areas.</summary>
    public bool MethodGlobalSwap {
        get => Values.MethodGlobalSwap;
        set => SetMethod(value, nameof(MethodGlobalSwap));
    }

    /// <summary>Gets or changes whether vanilla groups remain unfiltered and unrandomized.</summary>
    public bool MethodNone {
        get => Values.MethodNone;
        set => SetMethod(value, nameof(MethodNone));
    }

    /// <summary>Gets or changes whether vanilla groups are appended with a probability.</summary>
    public bool VanillaPlus {
        get => Values.VanillaPlus;
        set {
            if (Values.VanillaPlus == value) {
                return;
            }

            Values.VanillaPlus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowVanillaPlusFilter));
            QueueSave();
        }
    }

    /// <summary>Gets or changes the appended-vanilla probability.</summary>
    public int VanillaPlusChance {
        get => Values.VanillaPlusChance;
        set {
            if (Values.VanillaPlusChance == value) {
                return;
            }

            Values.VanillaPlusChance = value;
            VanillaPlusChanceText = value.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged();
            OnPropertyChanged(nameof(VanillaPlusChanceText));
            QueueSave();
        }
    }

    /// <summary>Gets or changes whether matching vanilla and randomized entries are merged.</summary>
    public bool VanillaMerge {
        get => Values.VanillaMerge;
        set {
            if (Values.VanillaMerge == value) {
                return;
            }

            Values.VanillaMerge = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowVanillaMergeFilter));
            QueueSave();
        }
    }

    /// <summary>Gets or changes whether added vanilla groups are filtered first.</summary>
    public bool VanillaPlusFilter {
        get => Values.VanillaPlusFilter;
        set {
            if (Values.VanillaPlusFilter == value) {
                return;
            }

            Values.VanillaPlusFilter = value;
            OnPropertyChanged();
            QueueSave();
        }
    }

    /// <summary>Gets or changes whether merged vanilla groups are filtered first.</summary>
    public bool VanillaMergeFilter {
        get => Values.VanillaMergeFilter;
        set {
            if (Values.VanillaMergeFilter == value) {
                return;
            }

            Values.VanillaMergeFilter = value;
            OnPropertyChanged();
            QueueSave();
        }
    }

    /// <summary>Gets whether the appended-vanilla row is visible for the current method.</summary>
    public bool ShowVanillaPlus => !Values.MethodNone;

    /// <summary>Gets whether the appended-vanilla probability row is visible for the current method.</summary>
    public bool ShowVanillaPlusChance => !Values.MethodNone;

    /// <summary>Gets whether the appended-vanilla filter row is visible for the current toggles.</summary>
    public bool ShowVanillaPlusFilter => !Values.MethodNone && Values.VanillaPlus;

    /// <summary>Gets whether the merged-vanilla row is visible for the current method.</summary>
    public bool ShowVanillaMerge => !Values.MethodNone;

    /// <summary>Gets whether the merged-vanilla filter row is visible for the current toggles.</summary>
    public bool ShowVanillaMergeFilter => !Values.MethodNone && Values.VanillaMerge;

    /// <summary>Gets the exact catalog label for the full randomization method.</summary>
    public string MethodFullLabel => Label("MethodFull");

    /// <summary>Gets the exact catalog description for the full randomization method.</summary>
    public string MethodFullDescription => Description("MethodFull");

    /// <summary>Gets the exact catalog label for the custom group count method.</summary>
    public string MethodCustomSizeLabel => Label("MethodCustomSize");

    /// <summary>Gets the exact catalog description for the custom group count method.</summary>
    public string MethodCustomSizeDescription => Description("MethodCustomSize");

    /// <summary>Gets the exact catalog label for the custom group count value.</summary>
    public string SpawnListSizeLabel => Label("SpawnListSize");

    /// <summary>Gets the exact catalog description for the custom group count value.</summary>
    public string SpawnListSizeDescription => Description("SpawnListSize");

    /// <summary>Gets the exact catalog label for matching vanilla group counts.</summary>
    public string MethodLocalSwapLabel => Label("MethodLocalSwap");

    /// <summary>Gets the exact catalog description for matching vanilla group counts.</summary>
    public string MethodLocalSwapDescription => Description("MethodLocalSwap");

    /// <summary>Gets the exact catalog label for the global species swap.</summary>
    public string MethodGlobalSwapLabel => Label("MethodGlobalSwap");

    /// <summary>Gets the exact catalog description for the global species swap.</summary>
    public string MethodGlobalSwapDescription => Description("MethodGlobalSwap");

    /// <summary>Gets the exact catalog label for no randomization.</summary>
    public string MethodNoneLabel => Label("MethodNone");

    /// <summary>Gets the exact catalog description for no randomization.</summary>
    public string MethodNoneDescription => Description("MethodNone");

    /// <summary>Gets the exact catalog label for appending vanilla groups.</summary>
    public string VanillaPlusLabel => Label("VanillaPlus");

    /// <summary>Gets the exact catalog description for appending vanilla groups.</summary>
    public string VanillaPlusDescription => Description("VanillaPlus");

    /// <summary>Gets the exact catalog label for the appended-vanilla probability.</summary>
    public string VanillaPlusChanceLabel => Label("VanillaPlusChance");

    /// <summary>Gets the exact catalog description for the appended-vanilla probability.</summary>
    public string VanillaPlusChanceDescription => Description("VanillaPlusChance");

    /// <summary>Gets the exact catalog label for merging vanilla groups.</summary>
    public string VanillaMergeLabel => Label("VanillaMerge");

    /// <summary>Gets the exact catalog description for merging vanilla groups.</summary>
    public string VanillaMergeDescription => Description("VanillaMerge");

    /// <summary>Gets the exact catalog label for filtering added vanilla groups.</summary>
    public string VanillaPlusFilterLabel => Label("VanillaPlusFilter");

    /// <summary>Gets the exact catalog description for filtering added vanilla groups.</summary>
    public string VanillaPlusFilterDescription => Description("VanillaPlusFilter");

    /// <summary>Gets the exact catalog label for filtering merged vanilla groups.</summary>
    public string VanillaMergeFilterLabel => Label("VanillaMergeFilter");

    /// <summary>Gets the exact catalog description for filtering merged vanilla groups.</summary>
    public string VanillaMergeFilterDescription => Description("VanillaMergeFilter");

    /// <summary>Stores the raw custom-group-count text without changing committed FormData.</summary>
    public void SetSpawnListSizeText(string? text) {
        SpawnListSizeText = text ?? string.Empty;
        OnPropertyChanged(nameof(SpawnListSizeText));
    }

    /// <summary>Stores the raw vanilla-add percentage text without changing committed FormData.</summary>
    public void SetVanillaPlusChanceText(string? text) {
        VanillaPlusChanceText = text ?? string.Empty;
        OnPropertyChanged(nameof(VanillaPlusChanceText));
    }

    /// <summary>Applies the original focus side effect for the custom group count row.</summary>
    public void FocusSpawnListSize() => MethodCustomSize = true;

    /// <summary>Applies the original focus side effect for the vanilla-add probability row.</summary>
    public void FocusVanillaPlusChance() => VanillaPlus = true;

    /// <summary>Captures all current values and raw drafts only when no save is still pending.</summary>
    /// <exception cref="InvalidOperationException">The template is unavailable or a save is pending.</exception>
    public RandomizerState CaptureState() {
        if (!IsEditingEnabled) {
            throw new InvalidOperationException("The Default template is unavailable.");
        }

        if (HasPendingSave) {
            throw new InvalidOperationException("A Default template save is still pending.");
        }

        return new RandomizerState(RandomizerState.CurrentSchema, CloneValues(Values), SpawnListSizeText,
            VanillaPlusChanceText, _selectedCategory);
    }

    /// <summary>Validates, then applies a recovered session without triggering any automatic save.</summary>
    /// <exception cref="InvalidDataException">The checkpoint is malformed or unsupported.</exception>
    /// <exception cref="InvalidOperationException">The live Default template is unavailable.</exception>
    public void RestoreState(RandomizerState state) {
        RandomizerState.Validate(state);
        if (!IsEditingEnabled) {
            throw new InvalidOperationException("The Default template is unavailable.");
        }

        _suppressSave = true;
        try {
            Values = CloneValues(state.Values);
            _selectedCategory = state.SelectedCategory;
            SpawnListSizeText = state.SpawnListSizeText;
            VanillaPlusChanceText = state.VanillaPlusChanceText;
            _dirty = false;
            _savePending = false;
            ErrorMessage = null;
            SetSaveStatus("Restored");
            NotifyAllValues();
        } finally {
            _suppressSave = false;
        }
    }

    /// <summary>Waits until the current save loop has no queued or running write.</summary>
    public async Task WaitForSaveAsync() {
        while (_saveRunning || _savePending) {
            await Task.Delay(20).ConfigureAwait(true);
        }
    }

    private async Task InitializeAsync() {
        try {
            TemplateSnapshot snapshot = await _store.LoadAsync().ConfigureAwait(true);
            Values = snapshot.Values;
            SpawnListSizeText = Values.SpawnListSize.ToString(CultureInfo.InvariantCulture);
            VanillaPlusChanceText = Values.VanillaPlusChance.ToString(CultureInfo.InvariantCulture);
            IsEditingEnabled = true;
            ErrorMessage = null;
            SetSaveStatus(snapshot.Source switch {
                TemplateSource.Yaml => "Saved",
                TemplateSource.LegacyJson => "Loaded from Default.json; not saved as YAML yet",
                _ => "No saved Default template yet",
            });
        } catch (Exception exception) {
            Values = new FormData();
            IsEditingEnabled = false;
            _dirty = false;
            SetError(exception.Message);
            SetSaveStatus("Unavailable");
        }

        NotifyAllValues();
        OnPropertyChanged(nameof(IsEditingEnabled));
        OnPropertyChanged(nameof(HasPendingSave));
    }

    private IReadOnlyList<RandomizerCategory> BuildCategories() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var categories = new List<RandomizerCategory>();
        foreach (SettingDefinition definition in _catalog.Definitions) {
            if (seen.Add(definition.Category)) {
                bool available = string.Equals(definition.Category, RandomizerState.AreaGroupType,
                    StringComparison.Ordinal)
                    || string.Equals(definition.Category, RandomizerState.AreaSelections,
                    StringComparison.Ordinal);
                categories.Add(new RandomizerCategory(definition.Category, EnglishText(definition.CategoryKey,
                    definition.Category), available));
            }
        }

        return categories.AsReadOnly();
    }

    /// <summary>Builds the real Area Selections rows from the catalog's 13 Boolean definitions.</summary>
    private IReadOnlyList<AreaSelectionViewModel> BuildAreaSelections() {
        var items = new List<AreaSelectionViewModel>();
        foreach (SettingDefinition definition in _catalog.AreaSelections) {
            items.Add(new AreaSelectionViewModel(this, definition.Name,
                EnglishText(definition.LabelKey, definition.Name),
                EnglishText(definition.DescriptionKey, definition.DescriptionKey)));
        }

        return items.AsReadOnly();
    }

    private string Label(string name) => _definitions.TryGetValue(name, out SettingDefinition? definition)
        ? EnglishText(definition.LabelKey, name) : name;

    private string Description(string name) => _definitions.TryGetValue(name, out SettingDefinition? definition)
        ? EnglishText(definition.DescriptionKey, definition.DescriptionKey) : name;

    private string EnglishText(string key, string fallback) =>
        _english.TryGetValue(key, out string? value) ? value : fallback;

    private int NumericBound(string name, string part, int fallback) {
        if (!_definitions.TryGetValue(name, out SettingDefinition? definition)
            || definition.Controls.FirstOrDefault(control => control.Numeric is not null)?.Numeric is not
            SourceNumericConstraint numeric) {
            return fallback;
        }

        string? expression = part switch {
            "MinimumExpression" => numeric.MinimumExpression,
            "FallbackExpression" => numeric.FallbackExpression,
            "MaximumExpression" => numeric.MaximumExpression,
            _ => null,
        };
        return int.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value : fallback;
    }

    /// <summary>Reads one Area Selections Boolean from the shared FormData contract.</summary>
    internal bool GetAreaSelection(string name) => name switch {
        nameof(FormData.RandomizeField) => Values.RandomizeField,
        nameof(FormData.RandomizeDungeons) => Values.RandomizeDungeons,
        nameof(FormData.RandomizeDungeonBosses) => Values.RandomizeDungeonBosses,
        nameof(FormData.RandomizeFieldBosses) => Values.RandomizeFieldBosses,
        nameof(FormData.RandomizePredators) => Values.RandomizePredators,
        nameof(FormData.RandomizeCages) => Values.RandomizeCages,
        nameof(FormData.RandomizeEggs) => Values.RandomizeEggs,
        nameof(FormData.RandomizeQuests) => Values.RandomizeQuests,
        nameof(FormData.RandomizeMimics) => Values.RandomizeMimics,
        nameof(FormData.RandomizeAllArea) => Values.RandomizeAllArea,
        nameof(FormData.RandomizeSingleSpawns) => Values.RandomizeSingleSpawns,
        nameof(FormData.RandomizeHumanBosses) => Values.RandomizeHumanBosses,
        nameof(FormData.RandomizeHumanBossesPals) => Values.RandomizeHumanBossesPals,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>Writes one Area Selections Boolean to FormData and queues the normal save loop.</summary>
    internal void SetAreaSelection(string name, bool value) {
        if (GetAreaSelection(name) == value) {
            return;
        }

        switch (name) {
            case nameof(FormData.RandomizeField):
                Values.RandomizeField = value;
                break;
            case nameof(FormData.RandomizeDungeons):
                Values.RandomizeDungeons = value;
                break;
            case nameof(FormData.RandomizeDungeonBosses):
                Values.RandomizeDungeonBosses = value;
                break;
            case nameof(FormData.RandomizeFieldBosses):
                Values.RandomizeFieldBosses = value;
                break;
            case nameof(FormData.RandomizePredators):
                Values.RandomizePredators = value;
                break;
            case nameof(FormData.RandomizeCages):
                Values.RandomizeCages = value;
                break;
            case nameof(FormData.RandomizeEggs):
                Values.RandomizeEggs = value;
                break;
            case nameof(FormData.RandomizeQuests):
                Values.RandomizeQuests = value;
                break;
            case nameof(FormData.RandomizeMimics):
                Values.RandomizeMimics = value;
                break;
            case nameof(FormData.RandomizeAllArea):
                Values.RandomizeAllArea = value;
                break;
            case nameof(FormData.RandomizeSingleSpawns):
                Values.RandomizeSingleSpawns = value;
                break;
            case nameof(FormData.RandomizeHumanBosses):
                Values.RandomizeHumanBosses = value;
                break;
            case nameof(FormData.RandomizeHumanBossesPals):
                Values.RandomizeHumanBossesPals = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }

        OnPropertyChanged(name);
        QueueSave();
    }

    private void SetMethod(bool value, string name) {
        bool current = name switch {
            nameof(MethodFull) => Values.MethodFull,
            nameof(MethodCustomSize) => Values.MethodCustomSize,
            nameof(MethodLocalSwap) => Values.MethodLocalSwap,
            nameof(MethodGlobalSwap) => Values.MethodGlobalSwap,
            nameof(MethodNone) => Values.MethodNone,
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        if (current == value) {
            return;
        }

        SetMethodValue(name, value);
        if (value) {
            ClearOtherMethods(name);
        }

        OnPropertyChanged(name);
        NotifyMethodDependents();
        QueueSave();
    }

    private void SetMethodValue(string name, bool value) {
        switch (name) {
            case nameof(MethodFull):
                Values.MethodFull = value;
                break;
            case nameof(MethodCustomSize):
                Values.MethodCustomSize = value;
                break;
            case nameof(MethodLocalSwap):
                Values.MethodLocalSwap = value;
                break;
            case nameof(MethodGlobalSwap):
                Values.MethodGlobalSwap = value;
                break;
            case nameof(MethodNone):
                Values.MethodNone = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }
    }

    private void ClearOtherMethods(string keep) {
        string[] members = [nameof(MethodFull), nameof(MethodCustomSize), nameof(MethodLocalSwap),
            nameof(MethodGlobalSwap), nameof(MethodNone)];
        foreach (string member in members) {
            if (string.Equals(member, keep, StringComparison.Ordinal)) {
                continue;
            }

            bool current = member switch {
                nameof(MethodFull) => Values.MethodFull,
                nameof(MethodCustomSize) => Values.MethodCustomSize,
                nameof(MethodLocalSwap) => Values.MethodLocalSwap,
                nameof(MethodGlobalSwap) => Values.MethodGlobalSwap,
                _ => Values.MethodNone,
            };
            if (!current) {
                continue;
            }

            SetMethodValue(member, false);
            OnPropertyChanged(member);
        }
    }

    private void NotifyMethodDependents() {
        OnPropertyChanged(nameof(ShowVanillaPlus));
        OnPropertyChanged(nameof(ShowVanillaPlusChance));
        OnPropertyChanged(nameof(ShowVanillaPlusFilter));
        OnPropertyChanged(nameof(ShowVanillaMerge));
        OnPropertyChanged(nameof(ShowVanillaMergeFilter));
    }

    private void NotifyAllValues() {
        OnPropertyChanged(nameof(MethodFull));
        OnPropertyChanged(nameof(MethodCustomSize));
        OnPropertyChanged(nameof(SpawnListSize));
        OnPropertyChanged(nameof(MethodLocalSwap));
        OnPropertyChanged(nameof(MethodGlobalSwap));
        OnPropertyChanged(nameof(MethodNone));
        OnPropertyChanged(nameof(VanillaPlus));
        OnPropertyChanged(nameof(VanillaPlusChance));
        OnPropertyChanged(nameof(VanillaMerge));
        OnPropertyChanged(nameof(VanillaPlusFilter));
        OnPropertyChanged(nameof(VanillaMergeFilter));
        OnPropertyChanged(nameof(SpawnListSizeText));
        OnPropertyChanged(nameof(VanillaPlusChanceText));
        OnPropertyChanged(nameof(SelectedCategory));
        foreach (AreaSelectionViewModel selection in AreaSelections) {
            selection.Refresh();
        }

        OnPropertyChanged(nameof(IsAreaGroupTypeSelected));
        OnPropertyChanged(nameof(IsAreaSelectionsSelected));
        OnPropertyChanged(nameof(CategoryTitle));
        NotifyMethodDependents();
    }

    private void QueueSave() {
        if (_suppressSave || !IsEditingEnabled) {
            return;
        }

        _dirty = true;
        _revision++;
        _savePending = true;
        SetSaveStatus("Saving…");
        _ = FlushAsync();
    }

    private async Task FlushAsync() {
        if (_saveRunning) {
            return;
        }

        _saveRunning = true;
        try {
            while (_savePending) {
                _savePending = false;
                int revision = _revision;
                FormData snapshot = CloneValues(Values);
                try {
                    await _store.SaveAsync(snapshot).ConfigureAwait(true);
                    if (revision == _revision) {
                        _dirty = false;
                        SetError(null);
                        SetSaveStatus("Saved");
                        OnPropertyChanged(nameof(HasPendingSave));
                    }
                } catch (Exception exception) {
                    _dirty = true;
                    _savePending = false;
                    SetError(exception.Message);
                    SetSaveStatus("Save failed");
                    OnPropertyChanged(nameof(HasPendingSave));
                    break;
                }
            }
        } finally {
            _saveRunning = false;
            OnPropertyChanged(nameof(HasPendingSave));
        }
    }

    private void SetSaveStatus(string value) {
        if (string.Equals(SaveStatus, value, StringComparison.Ordinal)) {
            return;
        }

        SaveStatus = value;
        OnPropertyChanged(nameof(SaveStatus));
    }

    private void SetError(string? value) {
        if (string.Equals(ErrorMessage, value, StringComparison.Ordinal)) {
            return;
        }

        ErrorMessage = value;
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
    }

    private static FormData CloneValues(FormData values) =>
        TemplateYaml.Deserialize(TemplateYaml.Serialize(values));
}

