using System;
using System.Collections.Generic;
using System.IO;

namespace SaveSync
{
    internal sealed class TransferResult
    {
        public int Files { get; set; }
        public string Backup { get; set; }
    }

    internal static class SaveTransfer
    {
        private sealed class Change
        {
            public string Target;
            public string Previous;
            public string RelativePath;
            public bool Added;
        }

        public static TransferResult Copy(GameProfile game, string appData, bool restore, Action checkGameClosed)
        {
            string source = restore ? game.Drive : game.Saves;
            string target = restore ? game.Saves : game.Drive;
            CheckDirectoryPath(source);
            CheckDirectoryPath(target);

            var folders = new List<string> { "" };
            var files = new List<string>();
            ReadTree(source, "", folders, files);
            if (files.Count == 0)
                throw new IOException("В исходной папке нет файлов: " + source);

            foreach (string relative in folders)
                CheckEntry(Path.Combine(target, relative), true);
            foreach (string relative in files)
                CheckEntry(Path.Combine(target, relative), false);

            string staging = Path.Combine(Path.GetDirectoryName(target), ".SaveSync-" + Guid.NewGuid().ToString("N"));
            string incoming = Path.Combine(staging, "new");
            string previous = Path.Combine(staging, "previous");
            var changes = new List<Change>();
            var createdFolders = new List<string>();
            bool keepStaging = false;
            try
            {
                Directory.CreateDirectory(incoming);
                foreach (string relative in files)
                {
                    string file = Path.Combine(incoming, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    CopyFile(Path.Combine(source, relative), file);
                }

                checkGameClosed();
                foreach (string relative in folders)
                {
                    string folder = Path.Combine(target, relative);
                    if (!CheckEntry(folder, true))
                    {
                        Directory.CreateDirectory(folder);
                        createdFolders.Add(folder);
                    }
                }
                foreach (string relative in files)
                {
                    var change = new Change
                    {
                        Target = Path.Combine(target, relative),
                        Previous = Path.Combine(previous, relative),
                        RelativePath = relative
                    };
                    changes.Add(change);
                    string file = Path.Combine(incoming, relative);
                    if (CheckEntry(change.Target, false))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(change.Previous));
                        File.Replace(file, change.Target, change.Previous, true);
                    }
                    else
                    {
                        File.Move(file, change.Target);
                        change.Added = true;
                    }
                }

                return new TransferResult
                {
                    Files = files.Count,
                    Backup = restore ? SaveBackup(appData, game, changes) : null
                };
            }
            catch (Exception error)
            {
                try
                {
                    RollBack(changes, createdFolders);
                }
                catch (Exception rollbackError)
                {
                    keepStaging = true;
                    throw new IOException("Не удалось полностью отменить копирование. Прежние файлы: "
                        + previous + Environment.NewLine + error.Message + Environment.NewLine + rollbackError.Message, error);
                }
                throw;
            }
            finally
            {
                if (!keepStaging && Directory.Exists(staging))
                    TryDelete(staging);
            }
        }

        private static void ReadTree(string source, string relative, List<string> folders, List<string> files)
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(Path.Combine(source, relative)))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                RejectLink(entry, attributes);
                string child = Path.Combine(relative, Path.GetFileName(entry));
                if ((attributes & FileAttributes.Directory) == 0)
                {
                    files.Add(child);
                    continue;
                }
                folders.Add(child);
                ReadTree(source, child, folders, files);
            }
        }

        private static void CheckDirectoryPath(string path)
        {
            string parent = Path.GetDirectoryName(path);
            if (parent != null)
                CheckDirectoryPath(parent);
            CheckEntry(path, true);
        }

        private static bool CheckEntry(string path, bool isDirectory)
        {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }

            RejectLink(path, attributes);
            if (((attributes & FileAttributes.Directory) != 0) != isDirectory)
                throw new IOException("По этому пути уже находится объект другого типа: " + path);
            return true;
        }

        private static void RejectLink(string path, FileAttributes attributes)
        {
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Нужна обычная локальная папка без ссылок и облачных заглушек: " + path);
        }

        private static void CopyFile(string source, string destination)
        {
            using (var input = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = File.Open(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                input.CopyTo(output);
            File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
        }

        private static string SaveBackup(string appData, GameProfile game, List<Change> changes)
        {
            if (changes.TrueForAll(change => change.Added))
                return null;

            string folder = Path.Combine(appData, "Backups", game.Id.ToString("N"),
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            try
            {
                foreach (Change change in changes)
                {
                    if (change.Added)
                        continue;
                    string backup = Path.Combine(folder, change.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    CopyFile(change.Previous, backup);
                }
                return folder;
            }
            catch
            {
                TryDelete(folder);
                throw;
            }
        }

        private static void RollBack(List<Change> changes, List<string> createdFolders)
        {
            for (int i = changes.Count - 1; i >= 0; i--)
            {
                Change change = changes[i];
                if (change.Added)
                    File.Delete(change.Target);
                else if (File.Exists(change.Previous))
                {
                    if (File.Exists(change.Target))
                        File.Replace(change.Previous, change.Target, null, true);
                    else
                        File.Move(change.Previous, change.Target);
                }
            }
            for (int i = createdFolders.Count - 1; i >= 0; i--)
                Directory.Delete(createdFolders[i]);
        }

        private static void TryDelete(string directory)
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
