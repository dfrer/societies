using System.Buffers.Binary;
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

internal interface IWindowsExtendedTcpTableApi
{
    int GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        bool order,
        int addressFamily,
        int tableClass,
        int reserved);
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
    private const int AddressFamilyInterNetworkV6 = 23;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaximumAggregateTableBytes = 2 * 1024 * 1024;
    private const int MaximumAggregateRows = 16_384;
    private const int MaximumNativeAlignmentTailBytes = 8;
    private const int TcpTableOwnerPidAll = 5;
    private static readonly int Ipv4RowsOffset = CheckedOffsetOf<MibTcpTableOwnerPidLayout>(
        nameof(MibTcpTableOwnerPidLayout.FirstRow));
    private static readonly int Ipv4RowBytes = Marshal.SizeOf<MibTcpRowOwnerPidLayout>();
    private static readonly int Ipv6RowsOffset = CheckedOffsetOf<MibTcp6TableOwnerPidLayout>(
        nameof(MibTcp6TableOwnerPidLayout.FirstRow));
    private static readonly int Ipv6RowBytes = Marshal.SizeOf<MibTcp6RowOwnerPidLayout>();
    private readonly IWindowsExtendedTcpTableApi _api;

    internal SystemWindowsTcpOwnerTable()
        : this(new SystemWindowsExtendedTcpTableApi())
    {
    }

    private SystemWindowsTcpOwnerTable(IWindowsExtendedTcpTableApi api) =>
        _api = api ?? throw new ArgumentNullException(nameof(api));

    internal static SystemWindowsTcpOwnerTable CreateForTesting(IWindowsExtendedTcpTableApi api) =>
        new(api);

    public ValueTask<IReadOnlyList<WindowsTcpOwnerRow>> ReadRowsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<WindowsTcpOwnerRow> rows = new();
        int aggregateBytes = 0;
        ReadFamily(
            AddressFamilyInterNetwork,
            Ipv4RowsOffset,
            Ipv4RowBytes,
            rows,
            ref aggregateBytes,
            cancellationToken);
        ReadFamily(
            AddressFamilyInterNetworkV6,
            Ipv6RowsOffset,
            Ipv6RowBytes,
            rows,
            ref aggregateBytes,
            cancellationToken);
        return ValueTask.FromResult<IReadOnlyList<WindowsTcpOwnerRow>>(rows);
    }

    private void ReadFamily(
        int addressFamily,
        int rowsOffset,
        int rowSize,
        List<WindowsTcpOwnerRow> aggregateRows,
        ref int aggregateBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int tableSize = 0;
        int firstResult = _api.GetExtendedTcpTable(
            IntPtr.Zero,
            ref tableSize,
            order: false,
            addressFamily,
            TcpTableOwnerPidAll,
            reserved: 0);
        if (firstResult != ErrorInsufficientBuffer
            || tableSize < rowsOffset
            || tableSize > MaximumAggregateTableBytes - aggregateBytes)
        {
            throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
        }

        cancellationToken.ThrowIfCancellationRequested();
        int allocatedSize = tableSize;
        IntPtr table = Marshal.AllocHGlobal(allocatedSize);
        try
        {
            int secondResult = _api.GetExtendedTcpTable(
                table,
                ref tableSize,
                order: false,
                addressFamily,
                TcpTableOwnerPidAll,
                reserved: 0);
            if (secondResult != 0
                || tableSize < rowsOffset
                || tableSize > allocatedSize
                || tableSize > MaximumAggregateTableBytes - aggregateBytes)
            {
                throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
            }

            cancellationToken.ThrowIfCancellationRequested();
            byte[] tableBytes = new byte[tableSize];
            Marshal.Copy(table, tableBytes, 0, tableBytes.Length);
            int remainingRows = MaximumAggregateRows - aggregateRows.Count;
            IReadOnlyList<WindowsTcpOwnerRow> familyRows = ParseFamily(
                tableBytes,
                addressFamily,
                rowsOffset,
                rowSize,
                remainingRows,
                cancellationToken);
            if (familyRows.Count > remainingRows)
            {
                throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
            }
            aggregateBytes = checked(aggregateBytes + tableSize);
            aggregateRows.AddRange(familyRows);
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

    private static IReadOnlyList<WindowsTcpOwnerRow> ParseFamily(
        byte[] tableBytes,
        int addressFamily,
        int rowsOffset,
        int rowSize,
        int maximumRows,
        CancellationToken cancellationToken)
    {
        if (rowsOffset < sizeof(uint)
            || rowSize <= 0
            || tableBytes.Length < rowsOffset
            || maximumRows < 0)
        {
            throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
        }

        uint unsignedRowCount = BinaryPrimitives.ReadUInt32LittleEndian(tableBytes);
        if (unsignedRowCount > (uint)maximumRows)
        {
            throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
        }
        int rowCount = checked((int)unsignedRowCount);
        long requiredBytes = rowsOffset + (long)rowCount * rowSize;
        long trailingBytes = tableBytes.Length - requiredBytes;
        if (trailingBytes < 0 || trailingBytes > MaximumNativeAlignmentTailBytes)
        {
            throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
        }

        List<WindowsTcpOwnerRow> rows = new(rowCount);
        int offset = rowsOffset;
        for (int index = 0; index < rowCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<byte> row = tableBytes.AsSpan(offset, rowSize);
            rows.Add(addressFamily switch
            {
                AddressFamilyInterNetwork => ParseIpv4Row(row),
                AddressFamilyInterNetworkV6 => ParseIpv6Row(row),
                _ => throw new LocalModelBenchmarkException("tcp_owner_table_query_failed")
            });
            offset = checked(offset + rowSize);
        }
        return rows;
    }

    private static WindowsTcpOwnerRow ParseIpv4Row(ReadOnlySpan<byte> row)
    {
        int state = ParseState(BinaryPrimitives.ReadUInt32LittleEndian(row));
        IPAddress localAddress = new(row.Slice(4, 4));
        int localPort = ParsePort(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(8, 4)));
        IPAddress remoteAddress = new(row.Slice(12, 4));
        int remotePort = ParsePort(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(16, 4)));
        int owningProcessId = ParseProcessId(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(20, 4)));
        return new WindowsTcpOwnerRow(
            localAddress,
            localPort,
            remoteAddress,
            remotePort,
            state,
            owningProcessId);
    }

    private static WindowsTcpOwnerRow ParseIpv6Row(ReadOnlySpan<byte> row)
    {
        IPAddress localAddress = new(
            row[..16],
            ParseScopeId(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(16, 4))));
        int localPort = ParsePort(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(20, 4)));
        IPAddress remoteAddress = new(
            row.Slice(24, 16),
            ParseScopeId(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(40, 4))));
        int remotePort = ParsePort(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(44, 4)));
        int state = ParseState(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(48, 4)));
        int owningProcessId = ParseProcessId(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(52, 4)));
        return new WindowsTcpOwnerRow(
            localAddress,
            localPort,
            remoteAddress,
            remotePort,
            state,
            owningProcessId);
    }

    // IP Helper defines only the low 16 bits of each DWORD port; the high bits may be uninitialized.
    private static int ParsePort(uint networkOrderPort) =>
        unchecked((ushort)IPAddress.NetworkToHostOrder(
            (short)(networkOrderPort & ushort.MaxValue)));

    private static long ParseScopeId(uint networkOrderScopeId) =>
        unchecked((uint)IPAddress.NetworkToHostOrder((int)networkOrderScopeId));

    private static int ParseState(uint state)
    {
        if (state is not (>= 1 and <= 12) and not 100)
        {
            throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
        }
        return checked((int)state);
    }

    private static int ParseProcessId(uint processId)
    {
        if (processId > int.MaxValue)
        {
            throw new LocalModelBenchmarkException("tcp_owner_table_query_failed");
        }
        return (int)processId;
    }

    private static int CheckedOffsetOf<T>(string fieldName) where T : struct =>
        checked((int)Marshal.OffsetOf<T>(fieldName).ToInt64());

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPidLayout
    {
        internal uint State;
        internal uint LocalAddress;
        internal uint LocalPort;
        internal uint RemoteAddress;
        internal uint RemotePort;
        internal uint OwningProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpTableOwnerPidLayout
    {
        internal uint EntryCount;
        internal MibTcpRowOwnerPidLayout FirstRow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcp6RowOwnerPidLayout
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal byte[] LocalAddress;
        internal uint LocalScopeId;
        internal uint LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal byte[] RemoteAddress;
        internal uint RemoteScopeId;
        internal uint RemotePort;
        internal uint State;
        internal uint OwningProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcp6TableOwnerPidLayout
    {
        internal uint EntryCount;
        internal MibTcp6RowOwnerPidLayout FirstRow;
    }
}

internal sealed class SystemWindowsExtendedTcpTableApi : IWindowsExtendedTcpTableApi
{
    public int GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        bool order,
        int addressFamily,
        int tableClass,
        int reserved) =>
        GetExtendedTcpTableNative(
            tcpTable,
            ref size,
            order,
            addressFamily,
            tableClass,
            reserved);

    [DllImport(
        "iphlpapi.dll",
        EntryPoint = "GetExtendedTcpTable",
        SetLastError = false)]
    private static extern int GetExtendedTcpTableNative(
        IntPtr tcpTable,
        ref int size,
        bool order,
        int addressFamily,
        int tableClass,
        int reserved);
}
