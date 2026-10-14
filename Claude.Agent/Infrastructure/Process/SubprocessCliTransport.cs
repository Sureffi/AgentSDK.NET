using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SysProcess = System.Diagnostics.Process;

namespace Claude.Agent;

internal sealed partial class SubprocessCliTransport : ITransport
{
    private const int DefaultMaxMessageSize = 1024 * 1024;
    private const int BufferSize = 65536;
    // The first version the recordings show writing command_lifecycle frames, by which a session's
    // runs take their turns.
    private const string MinimumVersion = "2.1.276";
    private static readonly int[] MinimumVersionParts = [2, 1, 276];

    private readonly ClaudeAgentClientOptions _options;
    private readonly ClaudeAgentSessionOptions _session;
    private readonly TransportNeeds _needs;
    private readonly FrameCodec _codec;
    private readonly CliLocator _locator;
    private readonly Func<string, string?> _readFile;
    private readonly ILogger<SubprocessCliTransport> _logger;
    private readonly TransportTap? _tap;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly int _maxMessageSize;
    private readonly TimeSpan _grace;
    private readonly string? _cwd;
    private readonly ConfigurationDirectories _directories;

    private SysProcess? _process;
    private SysProcess? _probe;
    private Stream? _stdout;
    private StreamWriter? _stdin;
    private Stream? _stderr;
    private Task? _stderrTask;
    private CancellationTokenSource? _stderrCancellation;
    private Error? _exitError;
    private bool _forced;

    public SubprocessCliTransport(
        ClaudeAgentClientOptions options,
        ClaudeAgentSessionOptions session,
        TransportNeeds needs,
        FrameCodec codec,
        CliLocator locator,
        Func<string, string?> readFile,
        ILogger<SubprocessCliTransport>? logger = null,
        TransportTap? tap = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(readFile);
        _directories = options.Directories
            ?? throw new ArgumentException("The options are not a client's: their directories were never resolved.", nameof(options));
        _options = options;
        _session = session;
        _needs = needs;
        _codec = codec;
        _locator = locator;
        _readFile = readFile;
        _logger = logger ?? NullLogger<SubprocessCliTransport>.Instance;
        _tap = tap;
        _maxMessageSize = options.MaxMessageSize ?? DefaultMaxMessageSize;
        _grace = options.ProcessGracePeriod;
        _cwd = string.IsNullOrEmpty(session.WorkingDirectory) ? null : session.WorkingDirectory;
    }

    public bool IsReady { get; private set; }

    public async ValueTask ConnectAsync(CancellationToken ct)
    {
        if (_process is not null)
        {
            return;
        }

        if (_cwd is not null && !Directory.Exists(_cwd))
        {
            throw new WorkingDirectoryNotFoundError($"The working directory {_cwd} does not exist.", _cwd).ToException();
        }

        var cliPath = _locator.Find(_options.ExecutablePath, _directories.User).OrThrow();

        CreateConfigurationDirectory();

        LiveTransports.Add(this);

        await CheckVersionAsync(cliPath).ConfigureAwait(false);

        var arguments = CommandLine.Build(_session, _needs, _readFile);
        _tap?.Arguments(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = cliPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = _options.StandardError is not null,
            StandardInputEncoding = new UTF8Encoding(false),
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (_cwd is not null)
        {
            startInfo.WorkingDirectory = _cwd;
        }

        ApplyEnvironment(startInfo);

        SysProcess process;
        try
        {
            process = new SysProcess { StartInfo = startInfo };
            process.Start();
        }
        catch (Exception exception)
        {
            var error = Failure(exception, cliPath);
            _exitError = error;
            throw error.ToException();
        }

        _process = process;
        // Read as bytes, as they arrive, and decoded as they are read: stdout by StandardOutputLines,
        // stderr by CopyStandardErrorAsync. A StreamReader's ReadAsync keeps reading
        // while each read fills its buffer, and so waits on a pipe that holds exactly that much with a
        // whole line in hand.
        _stdout = process.StandardOutput.BaseStream;
        _stdin = process.StandardInput;

        if (_options.StandardError is { } writer)
        {
            var stderr = process.StandardError.BaseStream;
            var cancellation = new CancellationTokenSource();
            _stderr = stderr;
            _stderrCancellation = cancellation;
            _stderrTask = Task.Run(
                () => CopyStandardErrorAsync(stderr, writer, cancellation.Token),
                CancellationToken.None);
        }

        IsReady = true;
    }

    public async IAsyncEnumerable<Result<InboundFrame, ParseError>> ReadAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var process = _process;
        var stdout = _stdout;
        if (process is null || stdout is null)
        {
            throw new TransportException(new ConnectionError("Not connected"));
        }

        // Every line goes to the tap and then to the codec as the binary wrote it; one that does not
        // decode comes back as its ParseError, in its place in the stream, and the stream goes on.
        await foreach (var line in StandardOutputLines.ReadAsync(stdout, _maxMessageSize, ct).ConfigureAwait(false))
        {
            _tap?.StdoutLine(line);
            yield return _codec.Decode(line);
        }

        int exitCode;
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            exitCode = process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            exitCode = -1;
        }

        if (exitCode != 0 && Volatile.Read(ref _forced))
        {
            // The close sequence ended it; its close logged that, and it is no failure of the run's.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("The process the close sequence ended exited with {ExitCode}.", exitCode);
            }
        }
        else if (exitCode != 0)
        {
            var error = new ProcessError(
                string.Create(CultureInfo.InvariantCulture, $"The binary exited with code {exitCode}"),
                exitCode);
            _exitError = error;
            throw new TransportException(error);
        }
    }

    public async ValueTask WriteAsync(OutboundFrame frame, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!IsReady || _stdin is null)
            {
                throw new ConnectionError("The binary's input is not open: it was never started, or it has been closed.").ToException();
            }

            if (_process is { HasExited: true } exited)
            {
                throw new ConnectionError(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The binary has exited, with code {exited.ExitCode}, and takes no more input.")).ToException();
            }

            if (_exitError is { } exitError)
            {
                throw new ConnectionError($"The binary takes no more input: {exitError.Message}").ToException();
            }

            try
            {
                var line = _codec.Encode(frame);
                _tap?.StdinLine(line);
                await _stdin.WriteAsync((line + "\n").AsMemory(), ct).ConfigureAwait(false);
                await _stdin.FlushAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                IsReady = false;
                var error = new ConnectionError($"Writing to the binary's input failed: {exception.Message}");
                _exitError = error;
                throw error.ToException();
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask EndInputAsync(CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CloseStdin();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // The close sequence: stdin EOF, then the process's exit within the grace, else SIGTERM and the
    // grace again, else SIGKILL; then stderr read to its end, within the grace, so what the process
    // wrote there before it exited reaches the writer. Stdout is left to the reader, which reads it to
    // its end; the process and its streams are released by DisposeAsync.
    public async ValueTask CloseAsync(CancellationToken ct)
    {
        var process = _process;
        if (process is null)
        {
            IsReady = false;
            return;
        }

        var held = await _writeLock.WaitAsync(_grace, CancellationToken.None).ConfigureAwait(false);
        try
        {
            IsReady = false;
            CloseStdin();
        }
        finally
        {
            if (held)
            {
                _writeLock.Release();
            }
        }

        if (await TerminateAsync(process, _grace, () => Volatile.Write(ref _forced, true)).ConfigureAwait(false))
        {
            _logger.LogWarning(
                "Claude Code did not exit within {Grace} of its stdin closing; the close sequence ended it.",
                _grace);
        }

        if (_stderrTask is { } stderrTask)
        {
            try
            {
                await stderrTask.WaitAsync(_grace, CancellationToken.None).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Something the process left behind holds stderr open: the read is stopped.
                if (_stderrCancellation is { } cancellation)
                {
                    await cancellation.CancelAsync().ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // The reader task never surfaces its failures to the caller.
            }
        }

        LiveTransports.Remove(this);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);

        _stderrCancellation?.Dispose();
        _stderrCancellation = null;
        _stderrTask = null;
        _stderr?.Dispose();
        _stderr = null;

        _process?.Dispose();
        _process = null;
        _stdout = null;
        _exitError = null;
        _writeLock.Dispose();
    }

    internal void Reap()
    {
        if (_probe is { } probe)
        {
            Terminate(probe, _grace);
        }

        var process = _process;
        if (process is null)
        {
            return;
        }

        IsReady = false;
        CloseStdin();
        Terminate(process, _grace);
    }

    [GeneratedRegex(@"^([0-9]+\.[0-9]+\.[0-9]+)")]
    private static partial Regex VersionPattern();

    private static void Terminate(SysProcess process, TimeSpan grace)
    {
        if (Wait(process, grace))
        {
            return;
        }

        if (!Signal(process) || !Wait(process, grace))
        {
            Kill(process);
            Wait(process, grace);
        }
    }

    private static bool Wait(SysProcess process, TimeSpan grace)
    {
        try
        {
            return process.HasExited || process.WaitForExit(grace);
        }
        catch (Exception)
        {
            return true;
        }
    }

    // True when the process did not exit within the grace and had to be ended; `forcing` is told
    // before the first signal goes.
    private static async ValueTask<bool> TerminateAsync(SysProcess process, TimeSpan grace, Action? forcing = null)
    {
        if (await WaitAsync(process, grace).ConfigureAwait(false))
        {
            return false;
        }

        forcing?.Invoke();
        if (!Signal(process) || !await WaitAsync(process, grace).ConfigureAwait(false))
        {
            Kill(process);
            await WaitAsync(process, grace).ConfigureAwait(false);
        }

        return true;
    }

    private static bool Signal(SysProcess process)
    {
        try
        {
            return Posix.Terminate(process.Id);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Kill(SysProcess process)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                process.Kill(entireProcessTree: true);
            }
            else
            {
                process.Kill();
            }
        }
        catch (Exception)
        {
            // The process is already gone.
        }
    }

    private static async ValueTask<bool> WaitAsync(SysProcess process, TimeSpan grace)
    {
        if (process.HasExited)
        {
            return true;
        }

        using var deadline = new CancellationTokenSource(grace);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return process.HasExited;
        }
    }

    private static Error Failure(Exception exception, string cliPath)
    {
        if (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new CliNotFoundError($"There is no binary to start at {cliPath}.");
        }

        return new ConnectionError($"The binary at {cliPath} did not start: {exception.Message}");
    }

    private static string SdkVersion()
    {
        var assembly = typeof(SubprocessCliTransport).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }

    private void ApplyEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.Environment.Remove("CLAUDECODE");
        startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"] = "sdk-dotnet";
        foreach (var (key, value) in _options.EnvironmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        startInfo.Environment["CLAUDE_AGENT_SDK_VERSION"] = SdkVersion();

        ApplyDirectories(startInfo);

        ApplyTraceContext(startInfo);

        if (_session.EnableFileCheckpointing is true)
        {
            startInfo.Environment["CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING"] = "true";
        }

        if (_cwd is not null)
        {
            startInfo.Environment["PWD"] = _cwd;
        }
    }

    // Over whatever this process inherited: the binary keeps everything in the client's directory,
    // and finds the login where the user's own claude keeps it. The secure-storage value may be set to
    // nothing, and the child's environment carries it so.
    private void ApplyDirectories(ProcessStartInfo startInfo)
    {
        startInfo.Environment[ConfigurationDirectories.ConfigurationVariable] = _directories.Configuration;
        startInfo.Environment[ConfigurationDirectories.SecureStorageVariable] = _directories.SecureStorageValue;
    }

    // Readable by its owner alone where the platform has modes: the binary keeps state there, and a
    // resumed session's whole transcript.
    private void CreateConfigurationDirectory()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(_directories.Configuration);
            }
            else
            {
                Directory.CreateDirectory(
                    _directories.Configuration,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var error = new ConnectionError(
                $"The configuration directory {_directories.Configuration} could not be created: {exception.Message}");
            _exitError = error;
            throw error.ToException();
        }
    }

    private void ApplyTraceContext(ProcessStartInfo startInfo)
    {
        if (Activity.Current is not { IdFormat: ActivityIdFormat.W3C } activity || activity.Id is not { } traceParent)
        {
            return;
        }

        if (!_options.EnvironmentVariables.ContainsKey("TRACEPARENT"))
        {
            startInfo.Environment["TRACEPARENT"] = traceParent;
        }

        if (_options.EnvironmentVariables.ContainsKey("TRACESTATE"))
        {
            return;
        }

        startInfo.Environment.Remove("TRACESTATE");
        if (activity.TraceStateString is { Length: > 0 } traceState)
        {
            startInfo.Environment["TRACESTATE"] = traceState;
        }
    }

    private void CloseStdin()
    {
        if (_stdin is null)
        {
            return;
        }

        try
        {
            _stdin.Dispose();
        }
        catch (Exception)
        {
            // A stdin already broken by the exited process needs no reporting.
        }

        _stdin = null;
    }

    private async Task CheckVersionAsync(string cliPath)
    {
        SysProcess? probe = null;
        try
        {
            using var deadline = new CancellationTokenSource(_grace);
            var startInfo = new ProcessStartInfo
            {
                FileName = cliPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };
            startInfo.ArgumentList.Add("-v");
            ApplyDirectories(startInfo);

            probe = new SysProcess { StartInfo = startInfo };
            probe.Start();
            _probe = probe;

            var buffer = new char[256];
            var read = await probe.StandardOutput.ReadAsync(buffer.AsMemory(), deadline.Token).ConfigureAwait(false);
            var output = new string(buffer, 0, read).Trim();

            var match = VersionPattern().Match(output);
            if (match.Success)
            {
                var parts = match.Groups[1].Value.Split('.').Select(int.Parse).ToArray();
                if (IsBelowMinimum(parts))
                {
                    _logger.LogWarning(
                        "The binary at {Path} is version {Version}. A version before {Minimum} does not name " +
                        "the prompts it takes, and a session writes it none after its first.",
                        cliPath,
                        match.Groups[1].Value,
                        MinimumVersion);
                }
            }
        }
        catch (Exception)
        {
            // The version probe is advisory: any failure leaves the connect path untouched.
        }
        finally
        {
            if (probe is not null)
            {
                await TerminateAsync(probe, _grace).ConfigureAwait(false);
                _probe = null;
                probe.Dispose();
            }
        }
    }

    private static bool IsBelowMinimum(int[] parts)
    {
        for (var i = 0; i < MinimumVersionParts.Length && i < parts.Length; i++)
        {
            if (parts[i] != MinimumVersionParts[i])
            {
                return parts[i] < MinimumVersionParts[i];
            }
        }

        return parts.Length < MinimumVersionParts.Length;
    }

    // The binary's stderr is free text: it writes there with process.stderr.write and console.error,
    // in pieces of any length, and nothing bounds a piece. It goes to the writer as it arrives,
    // decoded as UTF-8 across reads so a character split between two reads is whole, neither cut
    // into lines nor held. The pipe is read to its end whatever the writer does, so the binary never
    // waits on a full pipe.
    private async Task CopyStandardErrorAsync(Stream stderr, TextWriter writer, CancellationToken ct)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var decoder = encoding.GetDecoder();
        var bytes = new byte[BufferSize];
        var text = new char[encoding.GetMaxCharCount(BufferSize)];
        int read;
        do
        {
            try
            {
                read = await stderr.ReadAsync(bytes, ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException)
            {
                // The pipe broke, or the close stopped the read: something the binary left behind
                // held stderr open past the grace.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("The binary's stderr was not read to its end: {Reason}", exception.Message);
                }

                read = 0;
            }

            var length = decoder.GetChars(bytes, 0, read, text, 0, flush: read == 0);
            if (length == 0)
            {
                continue;
            }

            try
            {
                writer.Write(text, 0, length);
            }
            catch (Exception exception)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "StandardError threw on {Length} characters of the binary's stderr, which are lost; stderr is read on: {Reason}",
                        length,
                        exception.Message);
                }
            }
        }
        while (read != 0);
    }
}
