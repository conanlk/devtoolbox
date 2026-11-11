using System.Globalization;
using DevToolBox.Core.Localization;

namespace DevToolBox.App.Localization;

/// <summary>Something whose user-facing text must be rebuilt when the UI language changes.</summary>
public interface ILanguageAware
{
    void OnLanguageChanged();
}

/// <summary>
/// The current UI language. Switching it re-points the generated <see cref="Strings"/> (App) and Core resources
/// at the new culture, then tells every registered <see cref="ILanguageAware"/> so live text updates in place.
/// Registrations are weak: a view model or binding that goes away is never kept alive by the localizer.
/// Switch the language on the UI thread; registering is thread-safe (view models may be built anywhere).
/// </summary>
public static class Localizer
{
    private static readonly List<WeakReference<ILanguageAware>> Subscribers = [];
    private static readonly Lock SubscribersLock = new();

    static Localizer() => ApplyCulture(AppLanguage.English);

    public static AppLanguage Language { get; private set; } = AppLanguage.English;

    public static void SetLanguage(AppLanguage language)
    {
        if (language == Language)
        {
            return;
        }

        Language = language;
        ApplyCulture(language);
        foreach (var subscriber in LiveSubscribers())
        {
            subscriber.OnLanguageChanged();
        }
    }

    /// <summary>Calls <see cref="ILanguageAware.OnLanguageChanged"/> on every language switch while the target is alive.</summary>
    public static void Register(ILanguageAware target)
    {
        ArgumentNullException.ThrowIfNull(target);
        lock (SubscribersLock)
        {
            Subscribers.Add(new WeakReference<ILanguageAware>(target));
        }
    }

    public static void Unregister(ILanguageAware target)
    {
        lock (SubscribersLock)
        {
            Subscribers.RemoveAll(r => !r.TryGetTarget(out var t) || ReferenceEquals(t, target));
        }
    }

    /// <summary>The localized string for a resource key (the key itself if it's missing, so gaps are visible).</summary>
    public static string Get(string key) => Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;

    /// <summary>Fills a localized pattern; arguments are formatted invariantly so numbers read the same in every language.</summary>
    public static string Format(string pattern, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, pattern, args);

    /// <summary>A localized string that emits again whenever the language changes (backs <c>{l:Tr Key}</c>).</summary>
    public static IObservable<string> Observe(string key) => new LocalizedText(key);

    private static void ApplyCulture(AppLanguage language)
    {
        var culture = language.Culture();
        Strings.Culture = culture;
        CoreLocalization.Culture = culture;
    }

    /// <summary>Live subscribers, pruning the ones that have been collected.</summary>
    private static List<ILanguageAware> LiveSubscribers()
    {
        lock (SubscribersLock)
        {
            var live = new List<ILanguageAware>(Subscribers.Count);
            Subscribers.RemoveAll(reference =>
            {
                if (!reference.TryGetTarget(out var target))
                {
                    return true;
                }

                live.Add(target);
                return false;
            });
            return live;
        }
    }

    private sealed class LocalizedText(string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new Subscription(key, observer);
            observer.OnNext(Get(key));
            Register(subscription);
            return subscription;
        }
    }

    /// <summary>Held by the binding that subscribed; only weakly by the localizer.</summary>
    private sealed class Subscription(string key, IObserver<string> observer) : ILanguageAware, IDisposable
    {
        private bool _disposed;

        public void OnLanguageChanged()
        {
            if (!_disposed)
            {
                observer.OnNext(Get(key));
            }
        }

        public void Dispose()
        {
            _disposed = true;
            Unregister(this);
        }
    }
}
