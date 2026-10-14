using System.Text;
using System.Text.RegularExpressions;

namespace Claude.Agent;

internal static partial class TranscriptPaths
{
    public const int MaxSanitizedLength = 200;

    // The most symbolic links Canonicalize follows in one path, as many as Linux follows in resolving
    // one.
    public const int MaxLinksFollowed = 40;

    private const string Base36Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

    // The loop is over UTF-16 code units: the CLI that writes these directory names is JavaScript,
    // where charCodeAt and a non-/u regex both work in code units, so a non-BMP character in a cwd
    // sanitizes here as it does there. Measured: /tmp/<U+1F41D> sanitizes to -tmp---.
    public static string Sanitize(string name)
    {
        var sanitized = string.Create(name.Length, name, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') ? c : '-';
            }
        });

        return sanitized.Length <= MaxSanitizedLength
            ? sanitized
            : $"{sanitized[..MaxSanitizedLength]}-{NameHash(name)}";
    }

    // The hash the binary appends to a name it cut: over the UTF-16 code units, h = h * 31 + unit in
    // 32 bits, its absolute value in base 36.
    public static string NameHash(string value)
    {
        var hash = 0;
        unchecked
        {
            foreach (var c in value)
            {
                hash = (hash << 5) - hash + c;
            }
        }

        var n = Math.Abs((long)hash);
        if (n == 0)
        {
            return "0";
        }

        var digits = new StringBuilder();
        while (n > 0)
        {
            digits.Insert(0, Base36Digits[(int)(n % 36)]);
            n /= 36;
        }

        return digits.ToString();
    }

    // realpath + NFC, as the binary names the directory it runs in: every component of the path is
    // resolved, a symbolic link followed wherever it stands, an ancestor included, and a ".." taken
    // from where the links before it led. A path realpath refuses is taken as spelled, made full, as
    // the binary takes its own when realpath throws: one with a component that is not there, a file
    // with more after it, one that cannot be read, or one that needs more than MaxLinksFollowed links.
    public static string Canonicalize(string path)
    {
        try
        {
            return (Resolve(path) ?? Path.GetFullPath(path)).Normalize(NormalizationForm.FormC);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ArgumentException or NotSupportedException)
        {
            return Normalize(path);
        }
    }

    // The path as realpath resolves it, a component at a time off the root: a link's target takes the
    // link's place, from the root when it is absolute and from the link's directory when not. Null
    // where realpath throws.
    private static string? Resolve(string path)
    {
        var absolute = Path.IsPathFullyQualified(path)
            ? path
            : Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.Join(Directory.GetCurrentDirectory(), path);
        var resolved = Path.GetPathRoot(absolute)!;
        var pending = new Stack<string>();
        Push(pending, absolute[resolved.Length..]);
        var followed = 0;
        try
        {
            while (pending.TryPop(out var name))
            {
                if (name == ".")
                {
                    continue;
                }

                if (name == "..")
                {
                    resolved = Path.GetDirectoryName(resolved) ?? resolved;
                    continue;
                }

                var next = Path.Join(resolved, name);
                if (new FileInfo(next).LinkTarget is { } target)
                {
                    if (++followed > MaxLinksFollowed)
                    {
                        return null;
                    }

                    if (Path.IsPathRooted(target))
                    {
                        resolved = Path.GetPathRoot(target)!;
                        target = target[resolved.Length..];
                    }

                    Push(pending, target);
                    continue;
                }

                if (!Directory.Exists(next) && (pending.Count != 0 || !File.Exists(next)))
                {
                    return null;
                }

                resolved = next;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return resolved;
    }

    // The path's components onto the stack, its first on top.
    private static void Push(Stack<string> pending, string path)
    {
        var names = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        for (var index = names.Length - 1; index >= 0; index--)
        {
            pending.Push(names[index]);
        }
    }

    public static string Normalize(string value)
    {
        try
        {
            return value.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return value;
        }
    }

    public static string? FindProjectDirectory(string projectsDirectory, string sanitized)
    {
        var exact = Path.Combine(projectsDirectory, sanitized);
        if (Directory.Exists(exact))
        {
            return exact;
        }

        // Only a name the sanitiser cut is looked for under its cut and a dash, as the binary looks for
        // one when the exact directory is not there.
        if (sanitized.Length <= MaxSanitizedLength)
        {
            return null;
        }

        // Two directories can share the cut and differ only in their hash, and readdir order is the
        // filesystem's business: ordinal order is the one answer every caller gets, so the editor
        // verbs and the reader land in the same directory.
        var prefix = sanitized[..MaxSanitizedLength] + "-";
        try
        {
            var children = Directory.GetDirectories(projectsDirectory);
            Array.Sort(children, StringComparer.Ordinal);
            foreach (var entry in children)
            {
                if (Path.GetFileName(entry).StartsWith(prefix, StringComparison.Ordinal))
                {
                    return entry;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    public static bool IsUuid(string value) => UuidPattern().IsMatch(value);

    [GeneratedRegex(
        "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase)]
    public static partial Regex UuidPattern();
}
