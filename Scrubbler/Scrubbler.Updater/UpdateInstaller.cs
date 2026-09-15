using System.IO.Compression;

namespace Scrubbler.Updater;

internal static class UpdateInstaller
{
    internal static string Install(string appDir, string package)
    {
        appDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDir));
        var parentDir = Directory.GetParent(appDir)!.FullName;
        var stagingDir = Path.Combine(parentDir, $".scrubbler_staging_{Guid.NewGuid():N}");
        var backupDir = Path.Combine(parentDir, $".scrubbler_backup_{Guid.NewGuid():N}");

        try
        {
            ZipFile.ExtractToDirectory(package, stagingDir);

            // Plugins are installed locally and are not part of the application ZIP.
            // Finish copying before touching the current installation, so a failed
            // copy leaves both the app and its plugins intact.
            CopyDirectory(Path.Combine(appDir, "Plugins"), Path.Combine(stagingDir, "Plugins"));

            Directory.Move(appDir, backupDir);
            try
            {
                Directory.Move(stagingDir, appDir);
            }
            catch
            {
                Directory.Move(backupDir, appDir);
                throw;
            }

            return backupDir;
        }
        finally
        {
            // After a successful swap stagingDir no longer exists. On failure,
            // clean up only staging; never remove the original or its backup.
            try
            {
                if (Directory.Exists(stagingDir))
                    Directory.Delete(stagingDir, recursive: true);
            }
            catch
            {
                // A leftover staging folder must not hide the installation error.
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
