using System.Text.Json.Nodes;
using EDControls.EDList;

namespace EDControls.Tests;

public class ColumnLayoutTests
{

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
    public void RoundTrip_NoSort_PreservedAsNull()
    {
        var layout = new ColumnLayout
        {
            Columns = [ new ColumnPref { Key = "a", Width = 100 } ],
            Sort = null
        };

        var json = ColumnLayoutSerializer.Serialize(layout);
        var back = ColumnLayoutSerializer.Deserialize(json);

        Assert.NotNull(back);
        // WhenWritingNull vynechá klíč "sort" → deserializuje se zpět jako null (ne prázdný list).
        Assert.Null(back!.Sort);
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
        // Best-effort - neignoruj data jen kvůli version mismatch
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

    [Fact]
    public void Deserialize_MalformedColumnsShape_ReturnsNull()
    {
        // "columns" má být pole - string tam deserializaci shodí → best-effort null.
        var json = JsonNode.Parse("""{"version":1,"columns":"not-an-array"}""");

        Assert.Null(ColumnLayoutSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_NullColumns_YieldsEmptyList()
    {
        // "columns": null → Columns nesmí zůstat null (??= [] větev), ať UI nespadne.
        var json = JsonNode.Parse("""{"version":1,"columns":null}""");

        var layout = ColumnLayoutSerializer.Deserialize(json);

        Assert.NotNull(layout);
        Assert.NotNull(layout!.Columns);
        Assert.Empty(layout.Columns);
    }
}
