using Aranet4Monitor.Application.History;

namespace Aranet4Monitor.Abstractions;

public interface IHistoryClient
{
    Task<HistoryDownloadResult> DownloadAsync(
        ulong bluetoothAddress,
        DateTime? after,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
