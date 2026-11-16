// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class ExactScopeChildCursorTests
{
    [Fact]
    public void CursorBindsExactProjectRevisionParentPageAndChild()
    {
        string cursor = ExactScopeChildCursor.Create("Project", "r1", "root", 25, "child");
        Assert.Equal("child", ExactScopeChildCursor.Read(cursor, "Project", "r1", "root", 25));
        Assert.Throws<ArgumentException>(() =>
            ExactScopeChildCursor.Read(cursor, "OtherProject", "r1", "root", 25));
        Assert.Throws<ArgumentException>(() =>
            ExactScopeChildCursor.Read(cursor, "Project", "r2", "root", 25));
        Assert.Throws<ArgumentException>(() =>
            ExactScopeChildCursor.Read(cursor, "Project", "r1", "otherParent", 25));
        Assert.Throws<ArgumentException>(() =>
            ExactScopeChildCursor.Read(cursor, "Project", "r1", "root", 26));
    }

    [Fact]
    public void RejectsCorruptedOversizedOrNoncanonicalToken()
    {
        string cursor = ExactScopeChildCursor.Create("p", "r", "root", 1, "child");
        Assert.Throws<ArgumentException>(() =>
            ExactScopeChildCursor.Read(cursor + "x", "p", "r", "root", 1));
        Assert.Throws<ArgumentException>(() =>
            ExactScopeChildCursor.Read("%%.bad", "p", "r", "root", 1));
        Assert.Throws<ArgumentException>(() =>
            ExactScopeChildCursor.Read(new string('a', 12001), "p", "r", "root", 1));
        Assert.Throws<ArgumentException>(() =>
            ExactScopeChildCursor.Create("p", "r", "root", 1, "bad\nchild"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExactScopeChildCursor.Create("p", "r", "root", 101, "child"));
    }
}
