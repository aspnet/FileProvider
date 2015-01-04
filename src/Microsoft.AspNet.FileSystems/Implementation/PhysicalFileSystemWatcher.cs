﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Framework.Expiration.Interfaces;

namespace Microsoft.AspNet.FileSystems
{
    internal class PhysicalFileSystemWatcher
    {
        private readonly ConcurrentDictionary<string, FileExpirationTriggerBase> _triggerCache =
            new ConcurrentDictionary<string, FileExpirationTriggerBase>(StringComparer.OrdinalIgnoreCase);

        private readonly FileSystemWatcher _fileWatcher;

        private readonly object _lockObject = new object();
        private readonly PhysicalFileSystem _fileSystem;

        internal PhysicalFileSystemWatcher(PhysicalFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            _fileWatcher = new FileSystemWatcher(fileSystem.Root);
            _fileWatcher.IncludeSubdirectories = true;
            _fileWatcher.Created += OnChanged;
            _fileWatcher.Changed += OnChanged;
            _fileWatcher.Renamed += OnChanged;
            _fileWatcher.Deleted += OnChanged;
        }

        internal IExpirationTrigger CreateFileChangeTrigger(string filter)
        {
            filter = NormalizeFilter(filter);
            var isWildCardSearch = IsWildCardSearch(filter);
            var pattern = isWildCardSearch ? WildcardToRegexPattern(filter) : filter;

            FileExpirationTriggerBase expirationTrigger;
            if (!_triggerCache.TryGetValue(pattern, out expirationTrigger))
            {
                expirationTrigger =  isWildCardSearch ? (FileExpirationTriggerBase)new WildcardFileChangeTrigger(pattern) :
                                                        new FileChangeTrigger(_fileSystem, pattern);
                
                expirationTrigger = _triggerCache.GetOrAdd(pattern, expirationTrigger);
                lock (_lockObject)
                {
                    if (_triggerCache.Count > 0 && !_fileWatcher.EnableRaisingEvents)
                    {
                        // Perf: Turn on the file monitoring if there is something to monitor.
                        _fileWatcher.EnableRaisingEvents = true;
                    }
                }
            }

            return expirationTrigger;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            var relativePath = e.FullPath.Replace(_fileSystem.Root, string.Empty);
            if (_triggerCache.ContainsKey(relativePath))
            {
                ReportChangeForMatchedEntries(relativePath);
            }
            else
            {
                var wildCardTriggers = _triggerCache.Values
                                                    .OfType<WildcardFileChangeTrigger>()
                                                    .Where(t => t.IsMatch(relativePath));

                foreach (var trigger in wildCardTriggers)
                {
                    ReportChangeForMatchedEntries(trigger.Pattern);
                }
            }
        }

        private void ReportChangeForMatchedEntries(string pattern)
        {
            FileExpirationTriggerBase expirationTrigger;
            if (_triggerCache.TryRemove(pattern, out expirationTrigger))
            {
                expirationTrigger.Changed();
                if (_triggerCache.Count == 0)
                {
                    lock (_lockObject)
                    {
                        if (_triggerCache.Count == 0 && _fileWatcher.EnableRaisingEvents)
                        {
                            // Perf: Turn off the file monitoring if no files to monitor.
                            _fileWatcher.EnableRaisingEvents = false;
                        }
                    }
                }
            }
        }

        private string NormalizeFilter(string filter)
        {
            // If the searchPath ends with \ or /, we treat searchPath as a directory,
            // and will include everything under it, recursively.
            if (IsDirectoryPath(filter))
            {
                filter = filter + "**" + Path.DirectorySeparatorChar + "*";
            }

            filter = Path.DirectorySeparatorChar == '/' ?
                filter.Replace('\\', Path.DirectorySeparatorChar) :
                filter.Replace('/', Path.DirectorySeparatorChar);

            return filter;
        }

        private bool IsDirectoryPath(string path)
        {
            return path != null && path.Length > 1 &&
                (path[path.Length - 1] == Path.DirectorySeparatorChar ||
                path[path.Length - 1] == Path.AltDirectorySeparatorChar);
        }

        private string WildcardToRegexPattern(string wildcard)
        {
            var regex = Regex.Escape(wildcard);

            if (Path.DirectorySeparatorChar == '/')
            {
                // regex wildcard adjustments for *nix-style file systems.
                regex = regex
                    .Replace(@"\*\*/", "(.*/)?") //For recursive wildcards /**/, include the current directory.
                    .Replace(@"\*\*", ".*") // For recursive wildcards that don't end in a slash e.g. **.txt would be treated as a .txt file at any depth
                    .Replace(@"\*\.\*", @"\*") // "*.*" is equivalent to "*"
                    .Replace(@"\*", @"[^/]*(/)?") // For non recursive searches, limit it any character that is not a directory separator
                    .Replace(@"\?", "."); // ? translates to a single any character
            }
            else
            {
                // regex wildcard adjustments for Windows-style file systems.
                regex = regex
                    .Replace("/", @"\\") // On Windows, / is treated the same as \.
                    .Replace(@"\*\*\\", @"(.*\\)?") //For recursive wildcards \**\, include the current directory.
                    .Replace(@"\*\*", ".*") // For recursive wildcards that don't end in a slash e.g. **.txt would be treated as a .txt file at any depth
                    .Replace(@"\*\.\*", @"\*") // "*.*" is equivalent to "*"
                    .Replace(@"\*", @"[^\\]*(\\)?") // For non recursive searches, limit it any character that is not a directory separator
                    .Replace(@"\?", "."); // ? translates to a single any character
            }

            return regex;
        }

        private static bool IsWildCardSearch(string pattern)
        {
            return pattern.IndexOf('*') != -1;
        }
    }
}