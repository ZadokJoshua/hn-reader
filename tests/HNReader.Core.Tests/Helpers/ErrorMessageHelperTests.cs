using System.Net;
using System.Text.Json;
using HNReader.Core.Helpers;
using HNReader.Core.Services;
using LiteDB;

namespace HNReader.Core.Tests.Helpers;

public class ErrorMessageHelperTests
{
    [Fact]
    public void HttpRequestException_WithoutStatus_IsOffline()
    {
        var error = ErrorMessageHelper.Describe(new HttpRequestException("No such host is known"));
        Assert.Equal(UserErrorKind.Offline, error.Kind);
        Assert.True(error.IsOffline);
        Assert.True(error.IsRetryable);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void ServerBusyStatuses_AreServerBusy(HttpStatusCode status)
    {
        var error = ErrorMessageHelper.Describe(new HttpRequestException("x", null, status));
        Assert.Equal(UserErrorKind.ServerBusy, error.Kind);
    }

    [Fact]
    public void NotFound_IsNotAvailable()
    {
        var error = ErrorMessageHelper.Describe(new HttpRequestException("x", null, HttpStatusCode.NotFound));
        Assert.Equal(UserErrorKind.NotAvailable, error.Kind);
    }

    [Fact]
    public void HttpClientTimeout_IsTimeout_NotCancelled()
    {
        // HttpClient reports its own timeout as TaskCanceledException wrapping TimeoutException.
        var ex = new TaskCanceledException("The request was canceled", new TimeoutException());
        Assert.Equal(UserErrorKind.Timeout, ErrorMessageHelper.Describe(ex).Kind);
    }

    [Fact]
    public void PlainCancellation_IsCancelled()
    {
        Assert.Equal(UserErrorKind.Cancelled, ErrorMessageHelper.Describe(new OperationCanceledException()).Kind);
    }

    [Fact]
    public void JsonException_IsBadData()
    {
        Assert.Equal(UserErrorKind.BadData, ErrorMessageHelper.Describe(new JsonException("x")).Kind);
    }

    [Fact]
    public void MissingFile_IsFileMissing_NotGenericIo()
    {
        Assert.Equal(UserErrorKind.FileMissing, ErrorMessageHelper.Describe(new FileNotFoundException()).Kind);
        Assert.Equal(UserErrorKind.FileMissing, ErrorMessageHelper.Describe(new DirectoryNotFoundException()).Kind);
    }

    [Fact]
    public void SharingViolation_IsFileInUse()
    {
        var ex = new IOException("in use", unchecked((int)0x80070020));
        Assert.Equal(UserErrorKind.FileInUse, ErrorMessageHelper.Describe(ex).Kind);
    }

    [Fact]
    public void DiskFull_IsDiskFull()
    {
        var ex = new IOException("full", unchecked((int)0x80070070));
        Assert.Equal(UserErrorKind.DiskFull, ErrorMessageHelper.Describe(ex).Kind);
    }

    [Fact]
    public void UnauthorizedAccess_IsNoPermission()
    {
        Assert.Equal(UserErrorKind.NoPermission, ErrorMessageHelper.Describe(new UnauthorizedAccessException()).Kind);
    }

    [Fact]
    public void LiteException_IsStorage()
    {
        Assert.Equal(UserErrorKind.Storage, ErrorMessageHelper.Describe(new LiteException(0, "db broke")).Kind);
    }

    [Fact]
    public void AggregateWithSingleInner_IsUnwrapped()
    {
        var ex = new AggregateException(new HttpRequestException("x"));
        Assert.Equal(UserErrorKind.Offline, ErrorMessageHelper.Describe(ex).Kind);
    }

    [Fact]
    public void ImportFailure_MapsToImportKinds()
    {
        Assert.Equal(UserErrorKind.NotFavouritesFile,
            ErrorMessageHelper.Describe(new FavoritesImportException(FavoritesImportFailure.NotFavouritesFile)).Kind);
        Assert.Equal(UserErrorKind.EmptyFile,
            ErrorMessageHelper.Describe(new FavoritesImportException(FavoritesImportFailure.EmptyFile)).Kind);
    }

    [Fact]
    public void UserMessage_NeverLeaksExceptionText()
    {
        Exception[] samples =
        [
            new JsonException("The JSON value could not be converted to System.Collections.Generic.List`1[HNReader.Core.Models.Story]. Path: $ | LineNumber: 0"),
            new IOException(@"The process cannot access the file 'C:\Users\x\hnreader.log' because it is being used by another process."),
            new InvalidOperationException("secret internal detail"),
            new ArgumentException("bad", "storyId"),
        ];

        foreach (var ex in samples)
        {
            var message = ErrorMessageHelper.Describe(ex).Message;
            Assert.DoesNotContain(ex.Message, message);
            Assert.DoesNotContain("System.", message);
            Assert.DoesNotContain("Path:", message);
            Assert.DoesNotContain(@"C:\", message);
        }
    }

    [Fact]
    public void DetailedMessage_KeepsTechnicalDetailForLogs()
    {
        var detailed = ErrorMessageHelper.GetDetailedMessage(new JsonException("boom"), "import");
        Assert.Contains("boom", detailed);
        Assert.Contains("JsonException", detailed);
    }
}
