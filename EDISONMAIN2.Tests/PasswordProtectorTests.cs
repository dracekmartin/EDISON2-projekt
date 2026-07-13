using EDISONMAIN2;

namespace EDISONMAIN2.Tests;

public class PasswordProtectorTests
{
    [Fact]
    public void RoundTrip_SameUser_ReturnsPassword()
    {
        string blob = PasswordProtector.Protect("alice", "s3cret!");
        Assert.Equal("s3cret!", PasswordProtector.Unprotect(blob, "alice"));
    }

    [Fact]
    public void Unprotect_DifferentUser_ReturnsNull()
    {
        string blob = PasswordProtector.Protect("alice", "s3cret!");
        Assert.Null(PasswordProtector.Unprotect(blob, "bob"));
    }

    [Fact]
    public void Unprotect_NullOrEmptyBlob_ReturnsNull()
    {
        Assert.Null(PasswordProtector.Unprotect(null, "alice"));
        Assert.Null(PasswordProtector.Unprotect("", "alice"));
    }

    [Fact]
    public void Unprotect_GarbageBlob_ReturnsNull()
    {
        Assert.Null(PasswordProtector.Unprotect("not-a-valid-dpapi-blob!!!", "alice"));
    }

    [Fact]
    public void RoundTrip_EmptyPassword_Works()
    {
        string blob = PasswordProtector.Protect("alice", "");
        Assert.Equal("", PasswordProtector.Unprotect(blob, "alice"));
    }
}
