using System.Diagnostics;
using System.Globalization;
using Scrubbler.Updater;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Starting Scrubbler Updater");

        var pid = GetArgInt(args, "--pid");
        var appDir = GetArg(args, "--appDir");
        var package = GetArg(args, "--package");
        var entry = GetArg(args, "--entry");

        Console.WriteLine("Waiting for the Scrubbler application to close...");
        WaitForExit(pid, TimeSpan.FromSeconds(30));
        Console.WriteLine("Scrubbler was closed successfully");

        var backupDir = UpdateInstaller.Install(appDir, package);

        Console.WriteLine("Restarting the Scrubbler");

        // restart
        Process.Start(new ProcessStartInfo
        {
            FileName = entry,
            WorkingDirectory = appDir,
            UseShellExecute = false
        });

        // cleanup best effort
        TryDeleteDirectory(backupDir);

        return 0;
    }

    static string GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Length)
                throw new ArgumentException($"missing value for {name}");

            return args[i + 1];
        }

        throw new ArgumentException($"missing required argument {name}");
    }

    static int GetArgInt(string[] args, string name)
    {
        var value = GetArg(args, name);

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new ArgumentException($"invalid integer for {name}: '{value}'");

        if (parsed <= 0)
            throw new ArgumentException($"{name} must be > 0");

        return parsed;
    }

    static void WaitForExit(int pid, TimeSpan timeout)
    {
        try
        {
            using var p = Process.GetProcessById(pid);

            // wait for clean shutdown
            if (p.WaitForExit((int)Math.Max(0, timeout.TotalMilliseconds)))
                return;

            // timed out -> try to re-check existence (process may have exited between calls)
            try
            {
                _ = Process.GetProcessById(pid);
                throw new TimeoutException($"process {pid} did not exit within {timeout}");
            }
            catch (ArgumentException)
            {
                // process already exited
            }
        }
        catch (ArgumentException)
        {
            // process not found -> already exited
        }
    }

    static void TryDeleteDirectory(string path)
    {
        const int maxAttempts = 10;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                if (!Directory.Exists(path))
                    return;

                // remove readonly attributes just in case
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                Directory.Delete(path, recursive: true);
                return;
            }
            catch
            {
                // best effort retries (windows AV/file locks, etc.)
                Thread.Sleep(250);
            }
        }
    }
}
