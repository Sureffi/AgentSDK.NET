using System.Runtime.InteropServices;
using System.Text;

namespace Claude.Agent;

internal static class Posix
{
    public const int Enoent = 2;
    public const int Enotdir = 20;

    private const int SigTerm = 15;
    private const int OWronly = 0x1;
    private const int SeekEnd = 2;
    private const int Eintr = 4;

    // O_APPEND is 0x400 on Linux and 0x0008 on the BSDs, macOS included.
    private static readonly int OAppend = OperatingSystem.IsMacOS() ? 0x0008 : 0x400;

    public enum AppendOutcome
    {
        Written,
        NotHere,
        Failed,
    }

    public static bool Terminate(int pid) => !OperatingSystem.IsWindows() && Kill(pid, SigTerm) == 0;

    // open(2) with O_WRONLY|O_APPEND and deliberately no O_CREAT: a missing file is "not here, keep
    // searching", not a file to create, and the kernel's seek-to-EOF per write is what keeps the SDK
    // from interleaving with the CLI writing the same transcript. A zero-byte file is also "not here"
    // and is left untouched. The write goes through write(2) on the raw descriptor rather than a
    // FileStream because .NET routes a seekable handle's writes through pwrite(2), which on the BSDs
    // ignores O_APPEND and would write at the descriptor's own offset.
    public static AppendOutcome Append(string path, byte[] bytes, out int errno)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        errno = 0;
        var name = new byte[Encoding.UTF8.GetByteCount(path) + 1];
        Encoding.UTF8.GetBytes(path, name);

        var fd = Open(name, OWronly | OAppend);
        if (fd < 0)
        {
            errno = Marshal.GetLastPInvokeError();
            return errno is Enoent or Enotdir ? AppendOutcome.NotHere : AppendOutcome.Failed;
        }

        try
        {
            var size = Seek(fd, 0, SeekEnd);
            if (size < 0)
            {
                errno = Marshal.GetLastPInvokeError();
                return AppendOutcome.Failed;
            }

            if (size == 0)
            {
                return AppendOutcome.NotHere;
            }

            var remaining = bytes;
            while (remaining.Length > 0)
            {
                var step = Write(fd, remaining, (nuint)remaining.Length);
                if (step > 0)
                {
                    remaining = step == remaining.Length ? [] : remaining[(int)step..];
                    continue;
                }

                errno = Marshal.GetLastPInvokeError();
                if (errno != Eintr)
                {
                    return AppendOutcome.Failed;
                }
            }

            return AppendOutcome.Written;
        }
        finally
        {
            _ = Close(fd);
        }
    }

#pragma warning disable SYSLIB1054 // LibraryImport wants unsafe code on for the whole assembly; these signatures marshal a blittable array and ints
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(byte[] path, int flags);

    [DllImport("libc", EntryPoint = "lseek", SetLastError = true)]
    private static extern long Seek(int fd, long offset, int whence);

    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    private static extern nint Write(int fd, byte[] buffer, nuint count);

    [DllImport("libc", EntryPoint = "close")]
    private static extern int Close(int fd);
#pragma warning restore SYSLIB1054
}
