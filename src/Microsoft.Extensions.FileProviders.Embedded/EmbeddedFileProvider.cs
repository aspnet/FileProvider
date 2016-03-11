// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.FileProviders.Embedded;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders
{
    /// <summary>
    /// Looks up files using embedded resources in the specified assembly.
    /// This file provider is case sensitive.
    /// </summary>
    public class EmbeddedFileProvider : IFileProvider
    {
        private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars()
            .Where(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar).ToArray();
        private readonly Assembly _assembly;
        private readonly string _baseNamespace;
        private readonly DateTimeOffset _lastModified;
        private readonly string _root;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFileProvider" /> class using the specified
        /// assembly and empty base namespace.
        /// </summary>
        /// <param name="assembly"></param>
        public EmbeddedFileProvider(Assembly assembly)
            : this(assembly, string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFileProvider" /> class using the specified
        /// assembly, base namespace and empty root.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="baseNamespace"></param>
        public EmbeddedFileProvider(Assembly assembly, string baseNamespace)
            : this(assembly, baseNamespace, string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFileProvider" /> class using the specified
        /// assembly, base namespace, and root.
        /// </summary>
        /// <param name="assembly">The assembly that contains the embedded resources.</param>
        /// <param name="baseNamespace">The base namespace that contains the embedded resources.</param>
        /// <param name="root">The path that will be prepended to all requests.</param>
        public EmbeddedFileProvider(Assembly assembly, string baseNamespace, string root)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException("assembly");
            }

            if (!string.IsNullOrEmpty(root) && root.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                throw new FormatException(nameof(root));
            }

            _baseNamespace = string.IsNullOrEmpty(baseNamespace) ? string.Empty : baseNamespace + ".";
            _assembly = assembly;
            _root = string.IsNullOrEmpty(root) ? string.Empty : NormalizeRoot(root);

            // REVIEW: Does this even make sense?
            _lastModified = DateTimeOffset.MaxValue;
        }

        private string GetCombinedPath(string subpath)
        {
            var normalizedSubpath = subpath;

            if (!string.IsNullOrEmpty(_root))
            {
                normalizedSubpath = subpath.StartsWith("/")
                    ? subpath.Substring(1)
                    : subpath;
            }

            return Path.Combine(_root, normalizedSubpath);
        }

        /// <summary>
        /// Locates a file at the given path.
        /// </summary>
        /// <param name="subpath">The path that identifies the file. </param>
        /// <returns>The file information. Caller must check Exists property.</returns>
        public IFileInfo GetFileInfo(string subpath)
        {
            if (string.IsNullOrEmpty(subpath))
            {
                return new NotFoundFileInfo(subpath);
            }

            subpath = GetCombinedPath(subpath);

            var builder = new StringBuilder(_baseNamespace.Length + subpath.Length);
            builder.Append(_baseNamespace);

            // Relative paths starting with a leading slash okay
            if (subpath.StartsWith("/", StringComparison.Ordinal))
            {
                builder.Append(subpath, 1, subpath.Length - 1);
            }
            else
            {
                builder.Append(subpath);
            }

            for (var i = _baseNamespace.Length; i < builder.Length; i++)
            {
                if (builder[i] == '/' || builder[i] == '\\')
                {
                    builder[i] = '.';
                }
            }

            var resourcePath = builder.ToString();
            if (HasInvalidPathChars(resourcePath))
            {
                return new NotFoundFileInfo(resourcePath);
            }

            var name = Path.GetFileName(subpath);
            if (_assembly.GetManifestResourceInfo(resourcePath) == null)
            {
                return new NotFoundFileInfo(name);
            }

            return new EmbeddedResourceFileInfo(_assembly, resourcePath, name, _lastModified);
        }

        /// <summary>
        /// Enumerate a directory at the given path, if any.
        /// This file provider uses a flat directory structure. Everything under the base namespace is considered to be one directory.
        /// </summary>
        /// <param name="subpath">The path that identifies the directory</param>
        /// <returns>Contents of the directory. Caller must check Exists property.</returns>
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            // The file name is assumed to be the remainder of the resource name.
            if (subpath == null)
            {
                return new NotFoundDirectoryContents();
            }

            subpath = GetCombinedPath(subpath);

            // Relative paths starting with a leading slash okay
            if (subpath.StartsWith("/", StringComparison.Ordinal))
            {
                subpath = subpath.Substring(1);
            }

            // Non-hierarchal.
            if (!subpath.Equals(string.Empty))
            {
                return new NotFoundDirectoryContents();
            }

            var entries = new List<IFileInfo>();

            // TODO: The list of resources in an assembly isn't going to change. Consider caching.
            var resources = _assembly.GetManifestResourceNames();
            for (var i = 0; i < resources.Length; i++)
            {
                var resourceName = resources[i];
                if (resourceName.StartsWith(_baseNamespace))
                {
                    entries.Add(new EmbeddedResourceFileInfo(
                        _assembly,
                        resourceName,
                        resourceName.Substring(_baseNamespace.Length),
                        _lastModified));
                }
            }

            return new EnumerableDirectoryContents(entries);
        }

        private string NormalizeRoot(string root)
        {
            string normalizedRoot = root;

            if (!root.StartsWith("/") && !root.StartsWith("\\"))
            {
                normalizedRoot = $"/{normalizedRoot}";
            }

            if (!root.EndsWith("/") && !root.EndsWith("\\"))
            {
                normalizedRoot = $"{normalizedRoot}/";
            }

            return normalizedRoot;
        }

        public IChangeToken Watch(string pattern)
        {
            return NoopChangeToken.Singleton;
        }

        private static bool HasInvalidPathChars(string path)
        {
            return path.IndexOfAny(_invalidFileNameChars) != -1;
        }
    }
}
