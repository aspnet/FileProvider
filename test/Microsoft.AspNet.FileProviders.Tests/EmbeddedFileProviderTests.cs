﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace Microsoft.AspNet.FileProviders
{
    public class EmbeddedFileProviderTests
    {
        [Fact]
        public void When_GetFileInfo_and_resource_does_not_exist_then_should_not_get_file_info()
        {
            var provider = new EmbeddedFileProvider(this.GetType().Assembly, "");

            var fileInfo = provider.GetFileInfo("DoesNotExist.Txt");
            fileInfo.ShouldNotBe(null);
            fileInfo.Exists.ShouldBe(false);
        }

        [Fact]
        public void When_GetFileInfo_and_resource_exists_in_root_then_should_get_file_info()
        {
            var provider = new EmbeddedFileProvider(this.GetType().Assembly, "");
            var expectedFileLength = new FileInfo("File.txt").Length;
            var fileInfo = provider.GetFileInfo("File.txt");
            fileInfo.ShouldNotBe(null);
            fileInfo.Exists.ShouldBe(true);
            fileInfo.LastModified.ShouldNotBe(default(DateTimeOffset));
            fileInfo.Length.ShouldBe(expectedFileLength);
            fileInfo.IsDirectory.ShouldBe(false);
            fileInfo.PhysicalPath.ShouldBe(null);
            fileInfo.Name.ShouldBe("File.txt");

            //Passing in a leading slash
            fileInfo = provider.GetFileInfo("/File.txt");
            fileInfo.ShouldNotBe(null);
            fileInfo.Exists.ShouldBe(true);
            fileInfo.LastModified.ShouldNotBe(default(DateTimeOffset));
            fileInfo.Length.ShouldBe(expectedFileLength);
            fileInfo.IsDirectory.ShouldBe(false);
            fileInfo.PhysicalPath.ShouldBe(null);
            fileInfo.Name.ShouldBe("File.txt");
        }

        [Fact]
        public void When_GetFileInfo_and_resource_exists_in_subdirectory_then_should_get_file_info()
        {
            var provider = new EmbeddedFileProvider(this.GetType().Assembly, "Resources");

            var fileInfo = provider.GetFileInfo("ResourcesInSubdirectory/File3.txt");
            fileInfo.ShouldNotBe(null);
            fileInfo.Exists.ShouldBe(true);
            fileInfo.LastModified.ShouldNotBe(default(DateTimeOffset));
            fileInfo.Length.ShouldBeGreaterThan(0);
            fileInfo.IsDirectory.ShouldBe(false);
            fileInfo.PhysicalPath.ShouldBe(null);
            fileInfo.Name.ShouldBe("File3.txt");
        }

        [Fact]
        public void When_GetFileInfo_and_resources_in_path_then_should_get_file_infos()
        {
            var provider = new EmbeddedFileProvider(this.GetType().Assembly, "");

            var fileInfo = provider.GetFileInfo("Resources/File.txt");
            fileInfo.ShouldNotBe(null);
            fileInfo.Exists.ShouldBe(true);
            fileInfo.LastModified.ShouldNotBe(default(DateTimeOffset));
            fileInfo.Length.ShouldBeGreaterThan(0);
            fileInfo.IsDirectory.ShouldBe(false);
            fileInfo.PhysicalPath.ShouldBe(null);
            fileInfo.Name.ShouldBe("File.txt");
        }

        [Fact]
        public async void GetDirectoryContents()
        {
            var provider = new EmbeddedFileProvider(this.GetType().Assembly, "Resources");

            var files = await provider.GetDirectoryContentsAsync("");
            files.ShouldNotBe(null);
            files.Count().ShouldBe(2);
            (await provider.GetDirectoryContentsAsync("file")).Exists.ShouldBe(false);
			(await provider.GetDirectoryContentsAsync("file/")).Exists.ShouldBe(false);
			(await provider.GetDirectoryContentsAsync("file.txt")).Exists.ShouldBe(false);
			(await provider.GetDirectoryContentsAsync("file/txt")).Exists.ShouldBe(false);
        }

        [Fact]
        public async void GetDirInfo_with_no_matching_base_namespace()
        {
            var provider = new EmbeddedFileProvider(this.GetType().Assembly, "Unknown.Namespace");

            var files = await provider.GetDirectoryContentsAsync(string.Empty);
            files.ShouldNotBe(null);
            files.Exists.ShouldBe(true);
            files.Count().ShouldBe(0);
        }

        [Fact]
        public void Trigger_ShouldNot_Support_Registering_Callbacks()
        {
            var provider = new EmbeddedFileProvider(this.GetType().Assembly, "");
            var trigger = provider.Watch("Resources/File.txt");
            trigger.ShouldNotBe(null);
            trigger.ActiveExpirationCallbacks.ShouldBe(false);
            trigger.IsExpired.ShouldBe(false);
        }
    }
}