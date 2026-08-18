using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Societies.SnowGlobe;

namespace Societies.SnowGlobe.BenchmarkCli;

internal sealed record WindowsTcpOwnerRow(
    IPAddress LocalAddress,
    int LocalPort,
    IPAddress RemoteAddress,
    int RemotePort,
    int State,
    int OwningProcessId);

internal interface IWindowsTcpOwnerTable
{
    ValueTask<IReadOnlyList<WindowsTcpOwnerRow>> ReadRowsAsync(CancellationToken cancellationToken);
}

internal sealed class WindowsOllamaLoopbackConnectionOwnerResolver : IOllamaLoopbackConnectionOwnerResolver
{
    private const int EstablishedState = 5;
    private const int MaximumRows = 16_384;
    private static readonly IPAddress PinnedServerAddress = IPAddress.Parse("127.0.0.1");
    private readonly int _processId;
    private readonly string _runtimeExecutablePath;
    private readonly long _processStartTimeUtcTicks;
    private readonly IPinnedProcessInspector _processInspector;
    private readonly IWindowsTcpOwnerTable _ownerTable;

    private WindowsOllamaLoopbackConnectionOwnerResolver(
        int processId,
        string runtimeExecutablePath,
        long processStartTimeUtcTicks,
        IPinnedProcessInspector processInspector,
        IWindowsTcpOwnerTable ownerTable)
    {
        _processId = processId;
        _runtimeExecutablePath = Path.GetFullPath(runtimeExecutablePath);
        _processStartTimeUtcTicks = processStartTimeUtcTicks;
        _processInspector = processInspector;
        _ownerTable = ownerTable;
    }

    internal static WindowsOllamaLoopbackConnectionOwnerResolver Create(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new LocalModelBenchmarkException("tcp_owner_lookup_platform_unsupported");
        }
        if (processId <= 0)
        {
            throw new LocalModelBenchmarkException("ollama_process_identity_unavailable");
        }
        if (!File.Exists(PinnedBenchmarkContract.RuntimeExecutablePath)
            || !string.Equals(
                PinnedBenchmarkContract.Sha256File(PinnedBenchmarkContract.RuntimeExecutablePath),
                PinnedBenchmarkContract.RuntimeExecutableSha256,
                StringComparison.Ordinal))
        {
            throw new LocalModelBenchmarkException("runtime_executable_hash_mismatch");
        }

        SystemPinnedProcessInspector inspector = new();
        PinnedProcessSnapshot snapshot = inspector.Read(processId);
        ValidateProcessSnapshot(
            snapshot,
            processId,
            PinnedBenchmarkContract.RuntimeExecutablePath,
            expectedStartTimeUtcTicks: null);
        return new WindowsOllamaLoopbackConnectionOwnerResolver(
            processId,
            PinnedBenchmarkContract.RuntimeExecutablePath,
            snapshot.StartTimeUtcTicks,
            inspector,
            new SystemWindowsTcpOwnerTable());
    }

    internal static WindowsOllamaLoopbackConnectionOwnerResolver CreateForTesting(
        int processId,
        string runtimeExecutablePath,
        long processStartTimeUtcTicks,
        IPinnedProcessInspector processInspector,
        IWindowsTcpOwnerTable ownerTable) =>
        new(
            processId,
            runtimeExecutablePath,
            processStartTimeUtcTicks,
            processInspector,
            ownerTable);

    public async ValueTask<int> ResolveServerProcessIdAsync(
        OllamaLoopbackConnection connection,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateConnection(connection);
        ValidatePinnedProcess();

        IReadOnlyList<WindowsTcpOwnerRow> rows =
            await _ownerTable.ReadRowsAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        ValidatePinnedProcess();
        if (rows.Count > MaximumRows)
        {
            throw new LocalModelBenchmarkException("tcp_owner_table_too_large");
        }

        WindowsTcpOwnerRow? match = null;
        int matchCount = 0;
        foreach (WindowsTcpOwnerRow row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.State != EstablishedState
                || !row.LocalAddress.Equals(connection.ServerAddress)
                || row.LocalPort != connection.ServerPort
                || !row.RemoteAddress.Equals(connection.ClientAddress)
                || row.RemotePort != connection.ClientPort)
            {
                continue;
            }

            match = row;
            matchCount++;
            if (matchCount > 1)
            {
                throw new LocalModelBenchmarkException("ollama_connection_owner_cardinality_invalid");
            }
        }

        if (match is null)
        {
            throw new LocalModelBenchmarkException("ollama_connection_owner_missing");
        }
        if (match.OwningProcessId != _processId)
        {
            throw new LocalModelBenchmarkException("ollama_connection_owner_mismatch");
        }

        return match.OwningProcessId;
    }

    private static void ValidateConnection(OllamaLoopbackConnection connection)
    {
        if (connection.ClientAddress.AddressFamily != AddressFamily.InterNetwork
            || connection.ServerAddress.AddressFamily != AddressFamily.InterNetwork
            || !IPAddress.IsLoopback(connection.ClientAddress)
            || !connection.ServerAddress.Equals(PinnedServerAddress)
            || connection.ServerPort != 11435
            || connection.ClientPort is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new LocalModelBenchmarkException("ollama_connection_tuple_invalid");
        }
    }

    private void ValidatePinnedProcess()
    {
        PinnedProcessSnapshot snapshot = _processInspector.Read(_processId);
        ValidateProcessSnapshot(
            snapshot,
            _processId,
            _runtimeExecutablePath,
            _processStartTimeUtcTicks);
    }

    private static void ValidateProcessSnapshot(
        PinnedProcessSnapshot snapshot,
        int expectedProcessId,
        string expectedExecutablePath,
        long? expectedStartTimeUtcTicks)
    {
        if (snapshot.HasExited
            || snapshot.ProcessId != expectedProcessId
            || !string.Equals(
                Path.GetFullPath(snapshot.ExecutablePath),
                Path.GetFullPath(expectedExecutablePath),
                StringComparison.OrdinalIgnoreCase)
            || expectedStartTimeUtcTicks.HasValue
                && snapshot.StartTimeUtcTicks != expectedStartTimeUtcTicks.Value)
        {
            throw new LocalModelBenchmarkException("ollama_process_identity_changed");
        }
    }
}

internal sealed class SystemWindowsTcpOwnerTable : IWindowsTcpOwnerTable
{
    private const int AddressFamilyInterNetwork = 2;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaximumTableBytes = 2 * 1024 * 1024;
    private const int TcpTableOwnerPidAll = 5;

    public ValueTask<IReadOnlyList<WindowsTcpOwnerRow>> ReadRowsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int tableSize = 0;
        int firstResult = GetExtendedTcpTable(
            IntPtr.Zero,
            ref tableSize,
            order: false,
            AddressFamilyInterNetwork,
            TcpTableOwnerPidAll,
            reserved: 0);
        if (firstResult != ErrorInsufficientBuffer
            || tableSize < sizeof(uint)
            || tableSize > MaximumTableBytes)
        {
            throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
        }

        cancellationToken.ThrowIfCancellationRequested();
        IntPtr table = Marshal.AllocHGlobal(tableSize);
        try
        {
            int secondResult = GetExtendedTcpTable(
                table,
                ref tableSize,
                order: false,
                AddressFamilyInterNetwork,
                TcpTableOwnerPidAll,
                reserved: 0);
            if (secondResult != 0
                || tableSize < sizeof(uint)
                || tableSize > MaximumTableBytes)
            {
                throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
            }

            cancellationToken.ThrowIfCancellationRequested();
            int rowCount = Marshal.ReadInt32(table);
            int rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            long requiredBytes = sizeof(uint) + (long)rowCount * rowSize;
            if (rowCount < 0
                || rowCount > 16_384
                || requiredBytes > tableSize)
            {
                throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
            }

            List<WindowsTcpOwnerRow> rows = new(rowCount);
            IntPtr rowPointer = IntPtr.Add(table, sizeof(uint));
            for (int index = 0; index < rowCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MibTcpRowOwnerPid row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                rows.Add(new WindowsTcpOwnerRow(
                    new IPAddress(row.LocalAddress),
                    ConvertPort(row.LocalPort),
                    new IPAddress(row.RemoteAddress),
                    ConvertPort(row.RemotePort),
                    checked((int)row.State),
                    checked((int)row.OwningProcessId)));
                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }

            return ValueTask.FromResult<IReadOnlyList<WindowsTcpOwnerRow>>(rows);
        }
        catch (OverflowException exception)
        {
            throw new LocalModelBenchmarkException("tcp_owner_table_query_failed", exception);
        }
        finally
        {
            Marshal.FreeHGlobal(table);
        }
    }

    private static int ConvertPort(uint networkOrderPort) =>
        unchecked((ushort)IPAddress.NetworkToHostOrder((short)(networkOrderPort & ushort.MaxValue)));

    [DllImport("iphlpapi.dll", SetLastError = false)]
    private static extern int GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        bool order,
        int addressFamily,
        int tableClass,
        int reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        internal uint State;
        internal uint LocalAddress;
        internal uint LocalPort;
        internal uint RemoteAddress;
        internal uint RemotePort;
        internal uint OwningProcessId;
    }
}
