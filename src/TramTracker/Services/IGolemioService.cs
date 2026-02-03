using TramTracker.Models;

namespace TramTracker.Services;

public interface IGolemioService
{
    TramState CurrentState { get; }
    event EventHandler<TramState>? StateChanged;
    Task FetchDeparturesAsync();
}
