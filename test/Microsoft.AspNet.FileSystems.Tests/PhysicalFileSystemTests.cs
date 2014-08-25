﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Infrastructure;
using Shouldly;
using Xunit;

namespace Microsoft.AspNet.FileSystems
{
    public class PhysicalFileSystemTests
    {
        [Fact]
        public void ExistingFilesReturnTrue()
        {
            var provider = new PhysicalFileSystem(".");
            IFileInfo info;
            provider.TryGetFileInfo("File.txt", out info).ShouldBe(true);
            info.ShouldNotBe(null);
        }

        [Fact]
        public void MissingFilesReturnFalse()
        {
            var provider = new PhysicalFileSystem(".");
            IFileInfo info;
            provider.TryGetFileInfo("File5.txt", out info).ShouldBe(false);
            info.ShouldBe(null);
        }

        [Fact]
        public void SubPathActsAsRoot()
        {
            var provider = new PhysicalFileSystem("sub");
            IFileInfo info;
            provider.TryGetFileInfo("File2.txt", out info).ShouldBe(true);
            info.ShouldNotBe(null);
        }

        [Fact]
        public void RelativeOrAbsolutePastRootNotAllowed()
        {
            var serviceProvider = CallContextServiceLocator.Locator.ServiceProvider;
            var appEnvironment = (IApplicationEnvironment)serviceProvider.GetService(typeof(IApplicationEnvironment));

            var provider = new PhysicalFileSystem("sub");
            IFileInfo info;

            provider.TryGetFileInfo("..\\File.txt", out info).ShouldBe(false);
            info.ShouldBe(null);

            provider.TryGetFileInfo(".\\..\\File.txt", out info).ShouldBe(false);
            info.ShouldBe(null);

            var applicationBase = appEnvironment.ApplicationBasePath;
            var file1 = Path.Combine(applicationBase, "File.txt");
            var file2 = Path.Combine(applicationBase, "sub", "File2.txt");
            provider.TryGetFileInfo(file1, out info).ShouldBe(false);
            info.ShouldBe(null);

            provider.TryGetFileInfo(file2, out info).ShouldBe(true);
            info.ShouldNotBe(null);
            info.PhysicalPath.ShouldBe(file2);

            provider.TryGetFileInfo("/File2.txt", out info).ShouldBe(true);
            info.ShouldNotBe(null);
            info.PhysicalPath.ShouldBe(file2);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TryGetParentPath_ReturnsFalseIfPathIsNullOrEmpty(string subpath)
        {
            // Arrange
            var provider = new PhysicalFileSystem("sub");

            // Act and Assert
            provider.TryGetParentPath(subpath, out var parentPath).ShouldBe(false);
        }


        public static IEnumerable<object[]> TryGetParentPath_ReturnsFalseIfPathIsNotSubDirectoryOfRootData
        {
            get
            {
                yield return new[] { Directory.GetCurrentDirectory() };
                yield return new[] { @"x:\fake\test" };
            }
        }

        [Theory]
        [MemberData("TryGetParentPath_ReturnsFalseIfPathIsNotSubDirectoryOfRootData")]
        public void TryGetParentPath_ReturnsFalseIfPathIsNotSubDirectoryOfRoot(string subpath)
        {
            // Arrange
            var provider = new PhysicalFileSystem("sub");

            // Act and Assert
            provider.TryGetParentPath(subpath, out var parentPath).ShouldBe(false);
        }

        [Theory]
        [InlineData("", "sub", "")]
        [InlineData("", "/sub", "")]
        [InlineData("", @"sub/File2.txt", @"sub")]
        [InlineData("", @"/sub/dir/File3.txt", @"sub/dir")]
        [InlineData("sub", @"File2.txt", "")]
        public void TryGetParentPath_ReturnsParentPath(string root, string subpath, string expected)
        {
            // Arrange
            var provider = new PhysicalFileSystem(root);

            // Act and Assert
            provider.TryGetParentPath(subpath, out var parentPath).ShouldBe(true);
            // Convert backslash paths to forward slash so we can test with the same test data on Windows and *nix.
            expected.ShouldBe(parentPath.Replace('\\', '/'));
        }

        [Theory]
        [InlineData("sub/File2.txt")]
        [InlineData(@"/sub/File2.txt")]
        [InlineData(@"sub/dir")]
        public void TryGetParentPath_AllowsTraversingToTheRoot(string input)
        {
            // Arrange
            var provider = new PhysicalFileSystem("");

            // Act and Assert - 1
            provider.TryGetParentPath(input, out var path1).ShouldBe(true);
            path1.ShouldBe(@"sub");

            // Act and Assert - 2
            provider.TryGetDirectoryContents(path1, out var contents).ShouldBe(true);
            contents.Count().ShouldBe(2);
            contents = contents.OrderBy(f => f.Name);
            contents.First().Name.ShouldBe("dir");
            contents.Last().Name.ShouldBe("File2.txt");

            // Act and Assert - 3
            provider.TryGetParentPath(path1, out var path2).ShouldBe(true);
            path2.ShouldBe("");

            // Act and Assert - 4
            provider.TryGetParentPath(path2, out var path3).ShouldBe(false);
        }
    }
}
