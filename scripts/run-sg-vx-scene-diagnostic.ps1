param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [string]$GodotPath = $env:GODOT_BIN,
    [ValidateRange(30, 600)]
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public sealed class HiddenDesktopRunResult
{
    public int ProcessId { get; set; }
    public int ExitCode { get; set; }
    public string DesktopName { get; set; }
    public string ActiveDesktopName { get; set; }
    public bool WindowObservedOnDesktop { get; set; }
    public bool DescendantCustodyVerified { get; set; }
}

public static class HiddenDesktopProcess
{
    private const uint DesktopAllAccess = 0x01FF;
    private const int UoiName = 2;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 258;
    private const uint StillActive = 259;
    private const uint CreateSuspended = 0x00000004;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const int JobObjectBasicAccountingInformation = 1;
    private const int JobObjectExtendedLimitInformation = 9;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
        public int dwX; public int dwY; public int dwXSize; public int dwYSize;
        public int dwXCountChars; public int dwYCountChars; public int dwFillAttribute; public int dwFlags;
        public short wShowWindow; public short cbReserved2; public IntPtr lpReserved2;
        public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess; public IntPtr hThread; public uint dwProcessId; public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit; public long PerJobUserTimeLimit; public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize; public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass; public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount; public ulong WriteOperationCount; public ulong OtherOperationCount;
        public ulong ReadTransferCount; public ulong WriteTransferCount; public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit; public UIntPtr JobMemoryLimit; public UIntPtr PeakProcessMemoryUsed; public UIntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION
    {
        public long TotalUserTime; public long TotalKernelTime; public long ThisPeriodTotalUserTime; public long ThisPeriodTotalKernelTime;
        public uint TotalPageFaultCount; public uint TotalProcesses; public uint ActiveProcesses; public uint TotalTerminatedProcesses;
    }

    private delegate bool EnumDesktopWindowsDelegate(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr devmode, uint flags, uint access, IntPtr attributes);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder information, uint length, out uint needed);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDesktopWindows(IntPtr desktop, EnumDesktopWindowsDelegate callback, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string applicationName, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint creationFlags, IntPtr environment, string currentDirectory,
        ref STARTUPINFO startupInfo, out PROCESS_INFORMATION processInformation);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int informationClass, IntPtr information, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool QueryInformationJobObject(IntPtr job, int informationClass, IntPtr information, uint length, out uint returnedLength);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(IntPtr job, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);

    public static string GetActiveDesktopName()
    {
        IntPtr desktop = OpenInputDesktop(0, false, 0x0001);
        if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenInputDesktop failed.");
        try { return GetName(desktop); }
        finally { if (!CloseDesktop(desktop)) throw new Win32Exception(Marshal.GetLastWin32Error(), "CloseDesktop(input) failed."); }
    }

    public static HiddenDesktopRunResult Run(string executable, string[] arguments, string workingDirectory,
        string desktopName, string expectedActiveDesktopName, int timeoutMilliseconds)
    {
        string activeDesktopName = GetActiveDesktopName();
        if (!String.Equals(activeDesktopName, expectedActiveDesktopName, StringComparison.Ordinal))
            throw new InvalidOperationException("Active input desktop changed before alternate-desktop process creation.");
        IntPtr desktop = CreateDesktop(desktopName, IntPtr.Zero, IntPtr.Zero, 0, DesktopAllAccess, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDesktop failed.");
        IntPtr job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            if (!CloseDesktop(desktop)) throw new Win32Exception(Marshal.GetLastWin32Error(), "CloseDesktop after CreateJobObject failure failed.");
            throw new Win32Exception(error, "CreateJobObject failed.");
        }

        PROCESS_INFORMATION process = new PROCESS_INFORMATION();
        bool processCreated = false;
        bool assignedToJob = false;
        bool custodyCleaned = false;
        try
        {
            ConfigureKillOnClose(job);
            STARTUPINFO startup = new STARTUPINFO { cb = Marshal.SizeOf(typeof(STARTUPINFO)), lpDesktop = desktopName };
            StringBuilder commandLine = new StringBuilder(Quote(executable));
            foreach (string argument in arguments) commandLine.Append(' ').Append(Quote(argument));
            if (!CreateProcess(executable, commandLine, IntPtr.Zero, IntPtr.Zero, false, CreateSuspended, IntPtr.Zero,
                workingDirectory, ref startup, out process))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess on alternate desktop failed.");
            processCreated = true;
            if (!AssignProcessToJobObject(job, process.hProcess))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "AssignProcessToJobObject failed before child resume.");
            assignedToJob = true;
            if (ResumeThread(process.hThread) == UInt32.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ResumeThread failed after job assignment.");

            bool windowObserved = false;
            Stopwatch timeout = Stopwatch.StartNew();
            while (true)
            {
                windowObserved = windowObserved || HasWindowForProcess(desktop, process.dwProcessId);
                uint wait = WaitForSingleObject(process.hProcess, 50);
                if (wait == WaitObject0) break;
                if (wait != WaitTimeout) throw new Win32Exception(Marshal.GetLastWin32Error(), "WaitForSingleObject failed.");
                if (timeout.ElapsedMilliseconds > timeoutMilliseconds)
                {
                    if (!TerminateJobObject(job, 124)) throw new Win32Exception(Marshal.GetLastWin32Error(), "TerminateJobObject on timeout failed.");
                    RequireProcessExit(process.hProcess, 5000, "timeout cleanup");
                    RequireJobEmpty(job, 5000, "timeout cleanup");
                    custodyCleaned = true;
                    throw new TimeoutException("Alternate-desktop diagnostic timed out; its exact PID was terminated.");
                }
            }

            uint exitCode;
            if (!GetExitCodeProcess(process.hProcess, out exitCode) || exitCode == StillActive)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not obtain process exit code.");
            RequireJobEmpty(job, 5000, "normal completion");
            return new HiddenDesktopRunResult {
                ProcessId = checked((int)process.dwProcessId), ExitCode = checked((int)exitCode),
                DesktopName = desktopName, ActiveDesktopName = activeDesktopName,
                WindowObservedOnDesktop = windowObserved, DescendantCustodyVerified = true
            };
        }
        catch
        {
            if (processCreated && !custodyCleaned)
            {
                if (assignedToJob)
                {
                    if (!TerminateJobObject(job, 125)) throw new Win32Exception(Marshal.GetLastWin32Error(), "TerminateJobObject during exception cleanup failed.");
                }
                else if (!TerminateProcess(process.hProcess, 125))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "TerminateProcess before job assignment failed.");
                }
                RequireProcessExit(process.hProcess, 5000, "exception cleanup");
                if (assignedToJob) RequireJobEmpty(job, 5000, "exception cleanup");
            }
            throw;
        }
        finally
        {
            int cleanupError = 0; string cleanupStep = null;
            if (process.hThread != IntPtr.Zero && !CloseHandle(process.hThread)) { cleanupError = Marshal.GetLastWin32Error(); cleanupStep = "CloseHandle(thread)"; }
            if (process.hProcess != IntPtr.Zero && !CloseHandle(process.hProcess) && cleanupError == 0) { cleanupError = Marshal.GetLastWin32Error(); cleanupStep = "CloseHandle(process)"; }
            if (!CloseHandle(job) && cleanupError == 0) { cleanupError = Marshal.GetLastWin32Error(); cleanupStep = "CloseHandle(job)"; }
            if (!CloseDesktop(desktop) && cleanupError == 0) { cleanupError = Marshal.GetLastWin32Error(); cleanupStep = "CloseDesktop"; }
            if (cleanupError != 0) throw new Win32Exception(cleanupError, cleanupStep + " failed.");
        }
    }

    private static void ConfigureKillOnClose(IntPtr job)
    {
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        limits.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
        int size = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, buffer, false);
            if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, buffer, checked((uint)size)))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetInformationJobObject(KILL_ON_JOB_CLOSE) failed.");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static void RequireProcessExit(IntPtr process, uint timeoutMilliseconds, string phase)
    {
        uint wait = WaitForSingleObject(process, timeoutMilliseconds);
        if (wait != WaitObject0) throw new InvalidOperationException("Root process did not exit during " + phase + ".");
        uint exitCode;
        if (!GetExitCodeProcess(process, out exitCode) || exitCode == StillActive)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Root process exit verification failed during " + phase + ".");
    }

    private static void RequireJobEmpty(IntPtr job, int timeoutMilliseconds, string phase)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (GetActiveJobProcessCount(job) != 0)
        {
            if (timeout.ElapsedMilliseconds >= timeoutMilliseconds)
                throw new InvalidOperationException("Job descendants remained active during " + phase + ".");
            System.Threading.Thread.Sleep(20);
        }
    }

    private static uint GetActiveJobProcessCount(IntPtr job)
    {
        int size = Marshal.SizeOf(typeof(JOBOBJECT_BASIC_ACCOUNTING_INFORMATION));
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            uint returned;
            if (!QueryInformationJobObject(job, JobObjectBasicAccountingInformation, buffer, checked((uint)size), out returned))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "QueryInformationJobObject failed.");
            return ((JOBOBJECT_BASIC_ACCOUNTING_INFORMATION)Marshal.PtrToStructure(buffer, typeof(JOBOBJECT_BASIC_ACCOUNTING_INFORMATION))).ActiveProcesses;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static bool HasWindowForProcess(IntPtr desktop, uint processId)
    {
        bool found = false;
        int callbackError = 0;
        bool enumerated = EnumDesktopWindows(desktop, delegate(IntPtr window, IntPtr ignored) {
            uint owner; uint thread = GetWindowThreadProcessId(window, out owner);
            if (thread == 0) { callbackError = Marshal.GetLastWin32Error(); return false; }
            if (owner == processId) found = true;
            return !found;
        }, IntPtr.Zero);
        int error = Marshal.GetLastWin32Error();
        if (callbackError != 0) throw new Win32Exception(callbackError, "GetWindowThreadProcessId failed.");
        if (!enumerated && !found && error != 0) throw new Win32Exception(error, "EnumDesktopWindows failed.");
        return found;
    }

    private static string GetName(IntPtr handle)
    {
        uint needed; GetUserObjectInformation(handle, UoiName, null, 0, out needed);
        if (needed == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "GetUserObjectInformation sizing failed.");
        StringBuilder value = new StringBuilder(checked((int)(needed / 2)));
        if (!GetUserObjectInformation(handle, UoiName, value, needed, out needed))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetUserObjectInformation failed.");
        return value.ToString();
    }

    private static string Quote(string value)
    {
        if (value.Length > 0 && value.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0) return value;
        StringBuilder result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value) {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') result.Append('\\', (slashes * 2) + 1).Append(c);
            else result.Append('\\', slashes).Append(c);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
'@

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectRoot = Join-Path $repositoryRoot 'src\societies'
$launcherScriptPath = [System.IO.Path]::GetFullPath($PSCommandPath)
$runnerSourcePath = Join-Path $projectRoot 'tests\VoxelSceneDiagnosticRunner.cs'
$runnerScenePath = Join-Path $projectRoot 'tests\VoxelSceneDiagnosticRunner.tscn'
$societiesAssemblyPath = Join-Path $projectRoot '.godot\mono\temp\bin\Debug\Societies.dll'

function Get-DiagnosticSourceDigests {
    $paths = [ordered]@{
        launcherScript = $launcherScriptPath
        runnerSource = $runnerSourcePath
        runnerScene = $runnerScenePath
        societiesAssembly = $societiesAssemblyPath
    }
    $digests = [ordered]@{}
    foreach ($entry in $paths.GetEnumerator()) {
        if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
            throw "Diagnostic source artifact does not exist: $($entry.Value)"
        }
        $digests[$entry.Key] = (Get-FileHash -LiteralPath $entry.Value -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    return $digests
}
if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $wingetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    $GodotPath = Get-ChildItem -LiteralPath $wingetRoot -Recurse -Filter 'Godot*.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike '*console*' } |
        Select-Object -First 1 -ExpandProperty FullName
}
if ([string]::IsNullOrWhiteSpace($GodotPath)) { $GodotPath = (Get-Command godot -ErrorAction Stop).Source }
$GodotPath = [System.IO.Path]::GetFullPath($GodotPath)
if ($GodotPath -like '*_console.exe') {
    $windowedGodotPath = $GodotPath -replace '_console\.exe$', '.exe'
    if (Test-Path -LiteralPath $windowedGodotPath) { $GodotPath = $windowedGodotPath }
}
if (-not (Test-Path -LiteralPath $GodotPath -PathType Leaf)) { throw "Godot executable does not exist: $GodotPath" }

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$desktopName = 'SocietiesVoxelDiagnostic_' + [Guid]::NewGuid().ToString('N')
$activeDesktopName = [HiddenDesktopProcess]::GetActiveDesktopName()
$marker = [Guid]::NewGuid().ToString('N')
$markerPath = Join-Path $outputRoot ('.isolation-' + $marker + '.marker')
$logPath = Join-Path $outputRoot 'godot-hidden-desktop.log'
$sourceDigests = Get-DiagnosticSourceDigests
[System.IO.File]::WriteAllText($markerPath, $marker, [System.Text.UTF8Encoding]::new($false))

$arguments = @(
    '--rendering-driver', 'opengl3', '--audio-driver', 'Dummy', '--windowed',
    '--resolution', '960x540', '--log-file', $logPath, '--path', $projectRoot,
    'res://tests/VoxelSceneDiagnosticRunner.tscn', '--', '--output-dir', $outputRoot,
    '--isolation-marker', $marker, '--isolation-marker-file', $markerPath,
    '--isolation-desktop', $desktopName, '--active-desktop', $activeDesktopName
)

try {
    $result = [HiddenDesktopProcess]::Run($GodotPath, $arguments, $projectRoot, $desktopName, $activeDesktopName, $TimeoutSeconds * 1000)
    $launcherEvidencePath = Join-Path $outputRoot 'launcher-evidence.json'
    [System.IO.File]::WriteAllText($launcherEvidencePath, ([pscustomobject]@{
        schema = 'societies_sg_vx_hidden_desktop_launcher_evidence/v2'
        capturedUtc = [DateTime]::UtcNow.ToString('O')
        sourceSha256 = $sourceDigests
        processId = $result.ProcessId
        processDesktop = $result.DesktopName
        activeInputDesktop = $result.ActiveDesktopName
        windowObservedOnAlternateDesktop = $result.WindowObservedOnDesktop
        descendantCustodyVerified = $result.DescendantCustodyVerified
        exitCode = $result.ExitCode
    } | ConvertTo-Json -Depth 3), [System.Text.UTF8Encoding]::new($false))
    if ($result.ExitCode -ne 0) { throw "Hidden-desktop Godot diagnostic exited $($result.ExitCode). See $logPath" }
    if (-not $result.WindowObservedOnDesktop) { throw 'Godot process never published a window on the alternate desktop.' }
    if (-not $result.DescendantCustodyVerified) { throw 'Godot process tree custody was not verified empty.' }
    if ($result.DesktopName -eq $result.ActiveDesktopName) { throw 'Alternate desktop unexpectedly matched active input desktop.' }

    $evidencePath = Join-Path $outputRoot 'isolation-evidence.json'
    if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) { throw 'Runner did not publish isolation evidence.' }
    $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
    if ($evidence.processId -ne $result.ProcessId -or $evidence.processDesktop -ne $desktopName -or
        $evidence.activeInputDesktop -ne $result.ActiveDesktopName -or $evidence.activeDesktopTargeted -ne $false) {
        throw 'Runner isolation evidence does not match the native launcher boundary.'
    }
    $verifiedCaptureHashes = [ordered]@{}
    foreach ($name in @('launch-player-view.png', 'spawn.png', 'settlement-terrain-wide.png', 'side-surface-diagnostic.png', 'after-physics-traversal.png')) {
        $capture = Get-Item -LiteralPath (Join-Path $outputRoot $name) -ErrorAction Stop
        if ($capture.Length -le 1024) { throw "Rendered capture is empty or truncated: $name" }
        $expectedHashProperty = $evidence.captures.psobject.Properties[$name]
        if ($null -eq $expectedHashProperty) { throw "Runner evidence omitted encoded PNG hash: $name" }
        $actualHash = (Get-FileHash -LiteralPath $capture.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne [string]$expectedHashProperty.Value) { throw "Encoded PNG hash mismatch: $name" }
        $verifiedCaptureHashes[$name] = $actualHash
    }
    $postRunSourceDigests = Get-DiagnosticSourceDigests
    foreach ($name in $sourceDigests.Keys) {
        if ($postRunSourceDigests[$name] -ne $sourceDigests[$name]) {
            throw "Diagnostic source artifact changed during capture: $name"
        }
    }
    [System.IO.File]::WriteAllText($launcherEvidencePath, ([pscustomobject]@{
        schema = 'societies_sg_vx_hidden_desktop_launcher_evidence/v2'
        capturedUtc = [DateTime]::UtcNow.ToString('O')
        sourceSha256 = $sourceDigests
        processId = $result.ProcessId
        processDesktop = $result.DesktopName
        activeInputDesktop = $result.ActiveDesktopName
        windowObservedOnAlternateDesktop = $result.WindowObservedOnDesktop
        descendantCustodyVerified = $result.DescendantCustodyVerified
        exitCode = $result.ExitCode
        encodedPngSha256 = $verifiedCaptureHashes
    } | ConvertTo-Json -Depth 5), [System.Text.UTF8Encoding]::new($false))
    [pscustomobject]@{
        ProcessId = $result.ProcessId
        ProcessDesktop = $result.DesktopName
        ActiveInputDesktop = $result.ActiveDesktopName
        WindowObservedOnAlternateDesktop = $result.WindowObservedOnDesktop
        LauncherEvidencePath = $launcherEvidencePath
        EvidencePath = $evidencePath
        LogPath = $logPath
        CaptureCount = 5
        CameraPoseCount = @($evidence.cameraPoses.psobject.Properties).Count
    } | ConvertTo-Json -Depth 4
}
finally {
    if (Test-Path -LiteralPath $markerPath) { Remove-Item -LiteralPath $markerPath -Force }
}
