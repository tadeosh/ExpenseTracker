using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.Services;

public sealed class MauiTemporaryDirectoryProvider : ITemporaryDirectoryProvider
{
    public string TemporaryDirectory => FileSystem.CacheDirectory;
}
