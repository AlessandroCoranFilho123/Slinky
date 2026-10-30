using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace Slinky.Services
{
    /// <summary>
    /// Single source of the app's local state folder. Production resolves
    /// ApplicationData.Current (requires package identity) lazily; integration tests
    /// override LocalFolderProvider with a real temporary folder so the services run
    /// against isolated state without touching user data. Static by design — the
    /// services that consume it are static.
    /// </summary>
    public static class StateFolders
    {
        public static Func<Task<StorageFolder>> LocalFolderProvider { get; set; } =
            () => Task.FromResult(ApplicationData.Current.LocalFolder);
    }
}
