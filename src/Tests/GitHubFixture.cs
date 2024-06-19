using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Devlooped.Sponsors;
using static Devlooped.Sponsors.Process;

namespace Devlooped.Tests;

/// <summary>
/// Allows serializing tests that require GitHub CLI to be installed and authenticated.
/// </summary>
[CollectionDefinition("GitHub")]
public sealed class GitHubCollection : ICollectionFixture<GitHubCollection.GitHubFixture>
{
    public class GitHubFixture : IDisposable
    {
        readonly string? existingToken;

        public GitHubFixture()
        {
            Assert.True(GitHub.IsInstalled, "Did not find GH CLI");
            if (TryExecute("gh", "auth status", out var _))
                TryExecute("gh", "auth token", out existingToken);
        }

        public void Dispose()
        {
            if (existingToken != null &&
                TryExecute("gh", "auth token", out var currentToken) && 
                existingToken != currentToken)
            {
                Assert.True(TryExecute("gh", $"auth login --with-token", existingToken, out _));
                Assert.True(TryExecute("gh", "auth status", out _), "Expected auth status");
                Assert.True(TryExecute("gh", "auth token", out var newToken));
                Assert.Equal(newToken, existingToken);
            }
        }
    }
}