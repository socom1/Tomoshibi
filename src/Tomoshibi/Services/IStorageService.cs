using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Reads and writes the whole app state. Kept as an interface so the rest of
/// the app depends on the contract, not the JSON-on-disk implementation.
/// </summary>
public interface IStorageService
{
    AppState Load();
    void Save(AppState state);
}
