using PowerWordRelive.LLMRequester.Parsing;
using Xunit;

namespace PowerWordRelive.LLMRequester.Tests;

public class RefinementParserTests
{
    private readonly RefinementParser _parser = new();

    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        var result = _parser.Parse("EMPTY");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_Emptylowercase_ReturnsEmpty()
    {
        var result = _parser.Parse("empty");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_Append_CreatesCorrectOperation()
    {
        var result = _parser.Parse("refine|append|主持人：欢迎冒险者");

        Assert.Single(result);
        Assert.Equal(IncrementalOperation.OperationType.Append, result[0].Type);
        Assert.Equal("主持人：欢迎冒险者", result[0].Content);
    }

    [Fact]
    public void Parse_Insert_CreatesCorrectOperation()
    {
        var result = _parser.Parse("refine|insert|3|[场景]：描述");

        Assert.Single(result);
        Assert.Equal(IncrementalOperation.OperationType.Insert, result[0].Type);
        Assert.Equal(3, result[0].DisplayIndex);
        Assert.Equal("[场景]：描述", result[0].Content);
    }

    [Fact]
    public void Parse_Edit_CreatesCorrectOperation()
    {
        var result = _parser.Parse("refine|edit|2|梅琳：新内容");

        Assert.Single(result);
        Assert.Equal(IncrementalOperation.OperationType.Edit, result[0].Type);
        Assert.Equal(2, result[0].DisplayIndex);
        Assert.Equal("梅琳：新内容", result[0].Content);
    }

    [Fact]
    public void Parse_Remove_CreatesCorrectOperation()
    {
        var result = _parser.Parse("refine|remove|4");

        Assert.Single(result);
        Assert.Equal(IncrementalOperation.OperationType.Remove, result[0].Type);
        Assert.Equal(4, result[0].DisplayIndex);
    }

    [Fact]
    public void Parse_MultipleLines_ReturnsAll()
    {
        var result = _parser.Parse("""
                                   refine|append|主持人：欢迎
                                   refine|append|[场景]：冰风谷
                                   refine|edit|1|梅琳：修改
                                   refine|remove|2
                                   """);

        Assert.Equal(4, result.Count);
        Assert.Equal(IncrementalOperation.OperationType.Append, result[0].Type);
        Assert.Equal(IncrementalOperation.OperationType.Append, result[1].Type);
        Assert.Equal(IncrementalOperation.OperationType.Edit, result[2].Type);
        Assert.Equal(IncrementalOperation.OperationType.Remove, result[3].Type);
    }

    [Fact]
    public void Parse_EmptyLine_Ignores()
    {
        var result = _parser.Parse("""
                                   refine|append|A

                                   refine|append|B
                                   """);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_MissingIndex_ReturnsNull()
    {
        var result = _parser.Parse("refine|insert||content");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_InvalidIndex_ReturnsNull()
    {
        var result = _parser.Parse("refine|edit|abc|content");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ZeroIndex_ReturnsNull()
    {
        var result = _parser.Parse("refine|remove|0");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MissingContent_ReturnsNull()
    {
        var result = _parser.Parse("refine|append|");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_PipeInContent_Preserved()
    {
        var result = _parser.Parse("refine|append|梅琳：内容|带管道|符号");

        Assert.Single(result);
        Assert.Equal("梅琳：内容|带管道|符号", result[0].Content);
    }

    [Fact]
    public void Parse_LinesAfterEmpty_StillProcessed()
    {
        var result = _parser.Parse("""
                                   refine|append|A
                                   EMPTY
                                   refine|append|B
                                   """);

        Assert.Single(result); // stops at EMPTY
        Assert.Equal("A", result[0].Content);
    }

    [Fact]
    public void Parse_ExtraWhitespace_Trimmed()
    {
        var result = _parser.Parse("  refine|append|  主持人：欢迎  \n\trefine|edit| 2 | 梅琳：新 \n");

        Assert.Equal(2, result.Count);
        Assert.Equal("主持人：欢迎", result[0].Content);
        Assert.Equal("梅琳：新", result[1].Content);
    }

    [Fact]
    public void Parse_InsertContentWithIndex_ParsesBoth()
    {
        var result = _parser.Parse("refine|insert|5|破费事儿·朵蜜：我是野蛮人");

        Assert.Single(result);
        Assert.Equal(5, result[0].DisplayIndex);
        Assert.Equal("破费事儿·朵蜜：我是野蛮人", result[0].Content);
    }
}