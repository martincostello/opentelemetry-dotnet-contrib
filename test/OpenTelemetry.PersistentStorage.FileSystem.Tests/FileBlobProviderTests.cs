// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using OpenTelemetry.PersistentStorage.Abstractions;

namespace OpenTelemetry.PersistentStorage.FileSystem.Tests;

public class FileBlobProviderTests
{
    [Fact]
    public void FileBlobProvider_CreatesSubDirectory()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        using var blobProvider = new FileBlobProvider(testDirectory.FullName);

        Assert.Equal(testDirectory.FullName, blobProvider.DirectoryPath);
        Directory.Exists(testDirectory.FullName);

        // clean up
        Directory.Delete(testDirectory.FullName, true);
    }

    [Fact]
    public void FileBlobProvider_E2E_Test()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        using var blobProvider = new FileBlobProvider(testDirectory.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        // Create blob.
        Assert.True(blobProvider.TryCreateBlob(data.AsSpan(), out var blob1));

        // Get blob.
        Assert.True(blobProvider.TryGetBlob(out var blob2));

        Assert.Single(blobProvider.GetBlobs());

        // Verify file name from both create blob and get blob are same.
        Assert.Equal(((FileBlob)blob1).FullPath, ((FileBlob)blob2).FullPath);

        // Validate if content in the blob is same as buffer data passed to create blob.
        Assert.True(blob1.TryRead(out var blobContent));
        Assert.Equal(data, blobContent);

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_CreateBlobReturnsNullIfBlobProviderIsFull()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        using var blobProvider = new FileBlobProvider(testDirectory.FullName, 100);

        // write a file to fill up the configured max space.
        Assert.True(blobProvider.TryCreateBlob(new byte[100].AsSpan(), out _));

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.False(blobProvider.TryCreateBlob(data.AsSpan(), out var blob));
        Assert.Null(blob);

        testDirectory.Delete(true);
    }

    [Fact]
    [Obsolete("Validates obsolete byte[] overload compatibility.")]
    public void FileBlobProvider_CreateBlobReturnsFalseForNullByteArray()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        using var blobProvider = new FileBlobProvider(testDirectory.FullName);
        byte[] buffer = null!;

        Assert.False(blobProvider.TryCreateBlob(buffer, out var blob));
        Assert.False(blobProvider.TryCreateBlob(buffer, 1000, out var leasedBlob));

        Assert.Null(blob);
        Assert.Null(leasedBlob);
        Assert.Empty(Directory.EnumerateFiles(blobProvider.DirectoryPath));

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_TestRetentionPeriod()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        long maxSizeInBytes = 100000;
        var maintenancePeriodInMilliseconds = 3000;
        var retentionPeriodInMilliseconds = 1000;
        var writeTimeOutInMilliseconds = 1000;
        using var blobProvider = new FileBlobProvider(
            testDirectory.FullName,
            maxSizeInBytes,
            maintenancePeriodInMilliseconds,
            retentionPeriodInMilliseconds,
            writeTimeOutInMilliseconds);

        var data = Encoding.UTF8.GetBytes("Hello, World!");
        Assert.True(blobProvider.TryCreateBlob(data.AsSpan(), out var blob));

        // Wait for retention deadline to expire
        Thread.Sleep(2000);
        var retentionDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(retentionPeriodInMilliseconds);
        PersistentStorageHelper.RemoveExpiredBlob(retentionDeadline, ((FileBlob)blob).FullPath);

        // Blob will be deleted as retention period is 1 sec
        Assert.False(File.Exists(((FileBlob)blob).FullPath));

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_TestWriteTimeoutPeriod()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        long maxSizeInBytes = 100000;
        var maintenancePeriodInMilliseconds = 3000;
        var retentionPeriodInMilliseconds = 2000;
        var writeTimeOutInMilliseconds = 1000;
        using var blobProvider = new FileBlobProvider(
            testDirectory.FullName,
            maxSizeInBytes,
            maintenancePeriodInMilliseconds,
            retentionPeriodInMilliseconds,
            writeTimeOutInMilliseconds);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blobProvider.TryCreateBlob(data.AsSpan(), out var blob));

        // Mock write
        File.Move(((FileBlob)blob).FullPath, ((FileBlob)blob).FullPath + ".tmp");

        // validate file moved successfully
        Assert.True(File.Exists(((FileBlob)blob).FullPath + ".tmp"));

        // wait for timeout period
        Thread.Sleep(2000);
        var timeoutDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(writeTimeOutInMilliseconds);
        PersistentStorageHelper.RemoveTimedOutTmpFiles(timeoutDeadline, ((FileBlob)blob).FullPath + ".tmp");

        // tmp file will be deleted as write timeout period is 1 sec
        Assert.False(File.Exists(((FileBlob)blob).FullPath + ".tmp"));
        Assert.False(File.Exists(((FileBlob)blob).FullPath));

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProviderTests_TestLeaseExpiration()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        long maxSizeInBytes = 100000;
        var maintenancePeriodInMilliseconds = 3000;
        var retentionPeriodInMilliseconds = 2000;
        var writeTimeOutInMilliseconds = 1000;
        using var blobProvider = new FileBlobProvider(
            testDirectory.FullName,
            maxSizeInBytes,
            maintenancePeriodInMilliseconds,
            retentionPeriodInMilliseconds,
            writeTimeOutInMilliseconds);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blobProvider.TryCreateBlob(data.AsSpan(), out var blob));
        var blobPath = ((FileBlob)blob).FullPath;

        blob.TryLease(1000);
        var leasePath = ((FileBlob)blob).FullPath;
        Assert.True(File.Exists(leasePath));

        // Wait for lease to expire
        Thread.Sleep(2000);
        PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow, leasePath);

        // File name will be change to .blob
        Assert.True(File.Exists(blobPath));
        Assert.False(File.Exists(leasePath));

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_TryGetBlobReturnsFalseIfNoBlobs()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        try
        {
            using var blobProvider = new FileBlobProvider(testDirectory.FullName);

            Assert.False(blobProvider.TryGetBlob(out var blob));
            Assert.Null(blob);
        }
        finally
        {
            testDirectory.Delete(true);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(250)]
    public void FileBlobProvider_TryGetBlobReturnsNewestBlob(int count)
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        try
        {
            using var blobProvider = new FileBlobProvider(testDirectory.FullName);

            var now = DateTime.UtcNow;
            var random = new Random(count);
            string? expected = null;

            // Create the blobs in a random order so that the newest blob is not the last one created
            foreach (var offset in Enumerable.Range(0, count).OrderBy(_ => random.Next()))
            {
                var path = CreateBlobFile(blobProvider.DirectoryPath, now.AddSeconds(-offset));

                if (offset == 0)
                {
                    expected = path;
                }
            }

            // Files that are not blobs should be ignored
            CreateFile(blobProvider.DirectoryPath, GetBlobFileName(now.AddSeconds(1)) + ".tmp");
            CreateFile(blobProvider.DirectoryPath, GetBlobFileName(now.AddSeconds(1)) + "@" + FormatTimestamp(now.AddMinutes(1)) + ".lock");

            Assert.True(blobProvider.TryGetBlob(out var blob));
            Assert.Equal(expected, ((FileBlob)blob).FullPath);
            AssertSameAsGetBlobs(blobProvider, blob);
        }
        finally
        {
            testDirectory.Delete(true);
        }
    }

    [Fact]
    public void FileBlobProvider_TryGetBlobReturnsNewestUnexpiredBlobIfNewestBlobNameIsNotValid()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        try
        {
            using var blobProvider = new FileBlobProvider(testDirectory.FullName);

            var now = DateTime.UtcNow;

            var older = CreateBlobFile(blobProvider.DirectoryPath, now.AddMinutes(-2));
            var newer = CreateBlobFile(blobProvider.DirectoryPath, now.AddMinutes(-1));

            // These names sort after the valid blob names but do not contain a valid timestamp
            CreateFile(blobProvider.DirectoryPath, "zzzz.blob");
            CreateFile(blobProvider.DirectoryPath, "9999-99-99T999999.9999999Z-" + Guid.NewGuid().ToString("N") + ".blob");

            Assert.True(blobProvider.TryGetBlob(out var blob));
            Assert.Equal(newer, ((FileBlob)blob).FullPath);
            AssertSameAsGetBlobs(blobProvider, blob);

            File.Delete(newer);

            Assert.True(blobProvider.TryGetBlob(out blob));
            Assert.Equal(older, ((FileBlob)blob).FullPath);
            AssertSameAsGetBlobs(blobProvider, blob);
        }
        finally
        {
            testDirectory.Delete(true);
        }
    }

    [Fact]
    public void FileBlobProvider_TryGetBlobReturnsFalseIfAllBlobsHaveExpired()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        try
        {
            var retentionPeriod = TimeSpan.FromHours(1);
            using var blobProvider = new FileBlobProvider(testDirectory.FullName, retentionPeriodInMilliseconds: (long)retentionPeriod.TotalMilliseconds);

            var now = DateTime.UtcNow;

            CreateBlobFile(blobProvider.DirectoryPath, now - retentionPeriod - TimeSpan.FromMinutes(2));
            CreateBlobFile(blobProvider.DirectoryPath, now - retentionPeriod - TimeSpan.FromMinutes(1));
            CreateFile(blobProvider.DirectoryPath, "not-a-blob.blob");

            Assert.False(blobProvider.TryGetBlob(out var blob));
            Assert.Null(blob);
            Assert.Empty(blobProvider.GetBlobs());
        }
        finally
        {
            testDirectory.Delete(true);
        }
    }

    private static void AssertSameAsGetBlobs(FileBlobProvider blobProvider, PersistentBlob blob)
        => Assert.Equal(((FileBlob)blobProvider.GetBlobs().First()).FullPath, ((FileBlob)blob).FullPath);

    private static string FormatTimestamp(DateTime timestamp)
        => timestamp.ToString("yyyy-MM-ddTHHmmss.fffffffZ", CultureInfo.InvariantCulture);

    private static string GetBlobFileName(DateTime timestamp)
        => $"{FormatTimestamp(timestamp)}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}.blob";

    private static string CreateBlobFile(string directory, DateTime timestamp)
        => CreateFile(directory, GetBlobFileName(timestamp));

    private static string CreateFile(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }
}
