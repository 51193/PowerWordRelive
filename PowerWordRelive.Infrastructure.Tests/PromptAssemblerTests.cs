using PowerWordRelive.Infrastructure.Prompt;
using Xunit;

namespace PowerWordRelive.Infrastructure.Tests;

public class PromptAssemblerTests
{
    private static string BaseDir => "/prompts";

    [Fact]
    public void SimpleValueReplacement()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/tmpl.md", "Hello {{value:name}}!");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string> { ["name"] = "World" };

        var result = assembler.Assemble("tmpl.md", vars);

        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void MultipleValueReplacements()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/tmpl.md", "{{value:a}} and {{value:b}} and {{value:a}}");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string> { ["a"] = "X", ["b"] = "Y" };

        var result = assembler.Assemble("tmpl.md", vars);

        Assert.Equal("X and Y and X", result);
    }

    [Fact]
    public void SimpleDirInclusion()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/main.md", "Preamble. {{dir:part.md}} End.");
        fs.AddFile("/prompts/part.md", "middle");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var result = assembler.Assemble("main.md", vars);

        Assert.Equal("Preamble. middle End.", result);
    }

    [Fact]
    public void DirNestingWithValues()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/outer.md", "A {{dir:inner.md}} D");
        fs.AddFile("/prompts/inner.md", "B {{value:x}} C");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string> { ["x"] = "val" };

        var result = assembler.Assemble("outer.md", vars);

        Assert.Equal("A B val C D", result);
    }

    [Fact]
    public void MultiLevelDirNesting()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/l1.md", "1 {{dir:l2.md}} 1");
        fs.AddFile("/prompts/l2.md", "2 {{dir:l3.md}} 2");
        fs.AddFile("/prompts/l3.md", "3");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var result = assembler.Assemble("l1.md", vars);

        Assert.Equal("1 2 3 2 1", result);
    }

    [Fact]
    public void MixValuesAndDirsInSameFile()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/system.md", "You are {{value:role}}. {{dir:rules.md}}");
        fs.AddFile("/prompts/rules.md", "Rule: {{value:rule}}");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string> { ["role"] = "assistant", ["rule"] = "be nice" };

        var result = assembler.Assemble("system.md", vars);

        Assert.Equal("You are assistant. Rule: be nice", result);
    }

    [Fact]
    public void RecursionDepthExceeded()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/deep.md", "{{dir:deep.md}}");
        var assembler = new PromptAssembler(fs, BaseDir, maxDepth: 3);
        var vars = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidPromptException>(() =>
            assembler.Assemble("deep.md", vars));

        Assert.Contains("recursion depth", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void MissingDirTemplate()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/main.md", "{{dir:nonexistent.md}}");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidPromptException>(() =>
            assembler.Assemble("main.md", vars));

        Assert.Contains("not found", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void MissingValueKey()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/tmpl.md", "{{value:missing}}");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidPromptException>(() =>
            assembler.Assemble("tmpl.md", vars));

        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void EmptyDirPath()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/tmpl.md", "{{dir: }}");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidPromptException>(() =>
            assembler.Assemble("tmpl.md", vars));

        Assert.Contains("empty", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void EmptyValueKey()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/tmpl.md", "{{value: }}");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidPromptException>(() =>
            assembler.Assemble("tmpl.md", vars));

        Assert.Contains("empty", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void UnknownPlaceholderPrefix()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/tmpl.md", "{{foo: bar}}");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidPromptException>(() =>
            assembler.Assemble("tmpl.md", vars));

        Assert.Contains("unknown placeholder", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void TopLevelTemplateNotFound()
    {
        var fs = new InMemoryFileSystem();
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidPromptException>(() =>
            assembler.Assemble("no_file.md", vars));

        Assert.Contains("not found", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void VariablesSharedAcrossRecursion()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/a.md", "A {{value:x}} {{dir:b.md}}");
        fs.AddFile("/prompts/b.md", "B {{value:y}} {{dir:c.md}}");
        fs.AddFile("/prompts/c.md", "C {{value:z}}");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string> { ["x"] = "1", ["y"] = "2", ["z"] = "3" };

        var result = assembler.Assemble("a.md", vars);

        Assert.Equal("A 1 B 2 C 3", result);
    }

    [Fact]
    public void WhitespaceTrimInPlaceholder()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/tmpl.md", "{{  value:name  }}");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string> { ["name"] = "World" };

        var result = assembler.Assemble("tmpl.md", vars);

        Assert.Equal("World", result);
    }

    [Fact]
    public void MultipleDirReferences()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/main.md", "{{dir:a.md}} {{dir:b.md}} {{dir:c.md}}");
        fs.AddFile("/prompts/a.md", "A");
        fs.AddFile("/prompts/b.md", "B");
        fs.AddFile("/prompts/c.md", "C");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var result = assembler.Assemble("main.md", vars);

        Assert.Equal("A B C", result);
    }

    [Fact]
    public void DirPathWithSubdirectory()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/main.md", "{{dir:sub/file.md}}");
        fs.AddFile("/prompts/sub/file.md", "inside sub");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var result = assembler.Assemble("main.md", vars);

        Assert.Equal("inside sub", result);
    }

    [Fact]
    public void NoPlaceholders_PassesThrough()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/plain.md", "Hello, World!");
        var assembler = new PromptAssembler(fs, BaseDir);
        var vars = new Dictionary<string, string>();

        var result = assembler.Assemble("plain.md", vars);

        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void MaxDepthExactlyReached_Succeeds()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/l1.md", "{{dir:l2.md}}");
        fs.AddFile("/prompts/l2.md", "leaf");
        var assembler = new PromptAssembler(fs, BaseDir, maxDepth: 1);
        var vars = new Dictionary<string, string>();

        var result = assembler.Assemble("l1.md", vars);

        Assert.Equal("leaf", result);
    }

    [Fact]
    public void MaxDepthExactlyReached_FailsAtOnePast()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("/prompts/l1.md", "{{dir:l2.md}}");
        fs.AddFile("/prompts/l2.md", "{{dir:l3.md}}");
        fs.AddFile("/prompts/l3.md", "leaf");
        var assembler = new PromptAssembler(fs, BaseDir, maxDepth: 1);
        var vars = new Dictionary<string, string>();

        Assert.Throws<InvalidPromptException>(() =>
            assembler.Assemble("l1.md", vars));
    }

    [Fact]
    public void CustomMaxDepth()
    {
        var fs = new InMemoryFileSystem();
        for (var i = 1; i <= 7; i++)
        {
            var to = i < 7 ? $"{{{{dir:l{i + 1}.md}}}}" : "leaf";
            fs.AddFile($"/prompts/l{i}.md", to);
        }
        var assembler = new PromptAssembler(fs, BaseDir, maxDepth: 5);
        var vars = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidPromptException>(() =>
            assembler.Assemble("l1.md", vars));

        Assert.Contains("recursion depth", ex.Message.ToLowerInvariant());
    }
}
