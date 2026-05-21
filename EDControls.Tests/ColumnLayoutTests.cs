using System.Text.Json.Nodes;
using EDControls.EDList;

namespace EDControls.Tests;

public class ColumnLayoutTests
{
    // ─── Round-trip ──────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var layout = new ColumnLayout
        {
            Version = 1,
            Columns =
            [
                new ColumnPref { Key = "rz",    Width = 110, DisplayIndex = 0 },
                new ColumnPref { Key = "typ",   Width = 130, DisplayIndex = 1 },
                new ColumnPref { Key = "datum", Width = 100, DisplayIndex = 2 },
            ],
            Sort = [ new SortPref { Key = "datum", Direction = "desc" } ]
        };

        var json = ColumnLayoutSerializer.Serialize(layout);
        var back = ColumnLayoutSerializer.Deserialize(json);

        Assert.NotNull(back);
        Assert.Equal(1, back!.Version);
        Assert.Equal(3, back.Columns.Count);
        Assert.Equal("rz", back.Columns[0].Key);
        Assert.Equal(130, back.Columns[1].Width);
        Assert.Equal(2, back.Columns[2].DisplayIndex);
        Assert.NotNull(back.Sort);
        Assert.Single(back.Sort!);
        Assert.Equal("desc", back.Sort![0].Direction);
    }

    [Fact]
    public void RoundTrip_NoSort_PreservedAsNullOrEmpty()
    {
        var layout = new ColumnLayout
        {
            Columns = [ new ColumnPref { Key = "a", Width = 100 } ],
            Sort = null
        };

        var json = ColumnLayoutSerializer.Serialize(layout);
        var back = ColumnLayoutSerializer.Deserialize(json);

        Assert.NotNull(back);
        Assert.True(back!.Sort is null || back.Sort.Count == 0);
    }

    [Fact]
    public void RoundTrip_EmptyColumns_NoError()
    {
        var layout = new ColumnLayout { Columns = [] };
        var json = ColumnLayoutSerializer.Serialize(layout);
        var back = ColumnLayoutSerializer.Deserialize(json);

        Assert.NotNull(back);
        Assert.Empty(back!.Columns);
    }

    // ─── Best-effort čtení ───────────────────────────────────────────────────

    [Fact]
    public void Deserialize_Null_ReturnsNull()
    {
        Assert.Null(ColumnLayoutSerializer.Deserialize(null));
    }

    [Fact]
    public void Deserialize_UnknownFields_AreIgnored()
    {
        var json = JsonNode.Parse("""
            {
              "version": 1,
              "columns": [{"key": "a", "width": 100, "displayIndex": 0}],
              "futureField": "ignored"
            }
            """);

        var layout = ColumnLayoutSerializer.Deserialize(json);
        Assert.NotNull(layout);
        Assert.Single(layout!.Columns);
    }

    [Fact]
    public void Deserialize_MissingDisplayIndex_DefaultsToZero()
    {
        var json = JsonNode.Parse("""
            {
              "version": 1,
              "columns": [{"key": "a", "width": 100}]
            }
            """);

        var layout = ColumnLayoutSerializer.Deserialize(json);
        Assert.NotNull(layout);
        Assert.Equal(0, layout!.Columns[0].DisplayIndex);
    }

    [Fact]
    public void Deserialize_FutureVersion_StillRead()
    {
        // Best-effort — neignoruj data jen kvůli version mismatch
        var json = JsonNode.Parse("""
            {
              "version": 999,
              "columns": [{"key": "a", "width": 100, "displayIndex": 0}]
            }
            """);

        var layout = ColumnLayoutSerializer.Deserialize(json);
        Assert.NotNull(layout);
        Assert.Equal(999, layout!.Version);
        Assert.Single(layout.Columns);
    }

    // ─── Sort direction edge cases ───────────────────────────────────────────

    [Fact]
    public void SortDirection_DefaultAsc()
    {
        var pref = new SortPref { Key = "x" };
        Assert.Equal("asc", pref.Direction);
    }

    [Fact]
    public void SortDirection_PreservedAsString()
    {
        var layout = new ColumnLayout
        {
            Sort = [ new SortPref { Key = "x", Direction = "desc" } ]
        };
        var json = ColumnLayoutSerializer.Serialize(layout);
        var back = ColumnLayoutSerializer.Deserialize(json);

        Assert.Equal("desc", back!.Sort![0].Direction);
    }
}
