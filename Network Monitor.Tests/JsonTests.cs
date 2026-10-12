using System;
using System.Collections.Generic;

namespace Network_Monitor.Tests;

public class JsonTests
{
    [Fact]
    public void Parse_ReadsEveryKindOfValue()
    {
        var json = (IDictionary<string, object>)Json.Parse("""
            {
              "text": "hello",
              "fraction": 1.5,
              "negative": -2,
              "exponent": 1e3,
              "yes": true,
              "no": false,
              "nothing": null,
              "list": [1, "two", []],
              "nested": { "empty": {} }
            }
            """);

        Assert.Equal("hello", json["text"]);
        Assert.Equal(1.5, json["fraction"]);
        Assert.Equal(-2.0, json["negative"]);
        Assert.Equal(1000.0, json["exponent"]);
        Assert.True((bool)json["yes"]);
        Assert.False((bool)json["no"]);
        Assert.Null(json["nothing"]);
        Assert.Equal(new object[] { 1.0, "two", new List<object>() }, (List<object>)json["list"]);
        Assert.Empty((IDictionary<string, object>)((IDictionary<string, object>)json["nested"])["empty"]);
    }

    [Fact]
    public void Parse_ReadsEscapedCharacters()
    {
        Assert.Equal("\"\\/\b\f\n\r\té", Json.Parse("""
            "\"\\\/\b\f\n\r\t\u00e9"
            """));
    }

    [Fact]
    public void Parse_NamesIgnoreCase()
    {
        var json = (IDictionary<string, object>)Json.Parse("""{ "topmost": false }""");

        Assert.False((bool)json["Topmost"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("""{ "a" 1 }""")]
    [InlineData("""{ "a": 1,, }""")]
    [InlineData("""{ , }""")]
    [InlineData("""{ a: 1 }""")]
    [InlineData("[1 2]")]
    [InlineData("[,]")]
    [InlineData("\"not closed")]
    [InlineData("\"\\x\"")]
    [InlineData("\"\\u12\"")]
    [InlineData("tru")]
    [InlineData("-")]
    [InlineData("1e")]
    [InlineData("1 2")]
    public void Parse_RejectsWhatIsntJson(string text)
    {
        Assert.Throws<FormatException>(() => Json.Parse(text));
    }

    [Fact]
    public void Parse_AllowsACommaAfterTheLastValue()
    {
        var json = (IDictionary<string, object>)Json.Parse("""
            {
              "list": [1, 2,],
              "last": true,
            }
            """);

        Assert.Equal(new object[] { 1.0, 2.0 }, (List<object>)json["list"]);
        Assert.True((bool)json["last"]);
    }

    [Fact]
    public void Parse_RefusesDeepNestingRatherThanRunningOutOfStack()
    {
        Assert.Throws<FormatException>(() => Json.Parse(new string('[', 100_000)));
    }

    [Fact]
    public void Write_PutsOneValueOnEachLine()
    {
        var json = Json.Write(new List<KeyValuePair<string, object>>
        {
            new("Size", 96),
            new("Theme", "Dark"),
            new("Topmost", true),
            new("Nothing", null),
            new("Empty", new List<KeyValuePair<string, object>>()),
            new("Placement", new List<KeyValuePair<string, object>> { new("X", -1), new("Y", 2) }),
        });

        Assert.Equal(string.Join(Environment.NewLine,
            "{",
            "  \"Size\": 96,",
            "  \"Theme\": \"Dark\",",
            "  \"Topmost\": true,",
            "  \"Nothing\": null,",
            "  \"Empty\": {},",
            "  \"Placement\": {",
            "    \"X\": -1,",
            "    \"Y\": 2",
            "  }",
            "}"), json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{4D36E972-E325-11CE-BFC1-08002BE10318}")]
    [InlineData("quote \" backslash \\ slash /")]
    [InlineData("new\r\nline\ttab\0null\u001f")]
    [InlineData("é 中文 🙂")]
    public void Write_ThenParse_KeepsTextExactly(string text)
    {
        Assert.Equal(text, Json.Parse(Json.Write(text)));
    }

    [Fact]
    public void Write_RefusesValuesItCantWrite()
    {
        Assert.Throws<NotSupportedException>(() => Json.Write(1.5));
    }
}
