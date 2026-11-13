using System;
using Microsoft.AspNetCore.Identity;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

public class HashChecker
{
    private readonly ITestOutputHelper _output;
    public HashChecker(ITestOutputHelper output) => _output = output;

    [Fact]
    public void CheckHash()
    {
        var hasher = new PasswordHasher<object>();
        var hash = "AQAAAAIAAYagAAAAEA4paDBj1nA7wAq0pJjKvHYzrJSfFwVS6LLLKbXuZ1me7eWWHeg7pCERxjzCXtvSZA==";
        var res1 = hasher.VerifyHashedPassword(new object(), hash, "Manager@123456!");
        var res2 = hasher.VerifyHashedPassword(new object(), hash, "Manager@123456");
        var res3 = hasher.VerifyHashedPassword(new object(), hash, "manager123");
        var newHash = hasher.HashPassword(new object(), "Manager@123456!");
        _output.WriteLine($"Manager@123456! match: {res1}");
        _output.WriteLine($"Manager@123456 match: {res2}");
        _output.WriteLine($"manager123 match: {res3}");
        _output.WriteLine($"Generated New Hash: {newHash}");
        Assert.True(true);
    }
}
