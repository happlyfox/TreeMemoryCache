using System.IO;
using Spectre.Console;

namespace TreeMemoryCache.Diagnostics;

public static class DumpExtensions
{
    /// <summary>
    /// 输出缓存树的文本表示。
    /// </summary>
    public static void Dump(this TreeMemoryCache cache, TextWriter? writer = null)
    {
        writer ??= Console.Out;

        var tree = BuildTree(cache);
        AnsiConsole.Write(tree);
        writer.WriteLine();
    }

    private static Tree BuildTree(TreeMemoryCache cache)
    {
        var rootNodes = cache.GetRootNodes();
        var tree = new Tree("[bold]TreeMemoryCache[/]");

        foreach (var kvp in rootNodes)
        {
            BuildTreeNode(tree, kvp.Key, cache);
        }

        return tree;
    }

    private static void BuildTreeNode(Tree parentTree, string path, TreeMemoryCache cache)
    {
        var hasValue = cache.TryGetValue(path, out _);
        var style = hasValue ? "green" : "dim";
        var node = parentTree.AddNode($"[{style}]{Markup.Escape(path)}[/{style}]");

        foreach (var childPath in cache.GetChildPaths(path))
        {
            BuildChildNode(node, childPath, cache);
        }
    }

    private static void BuildChildNode(TreeNode parentNode, string path, TreeMemoryCache cache)
    {
        var hasValue = cache.TryGetValue(path, out _);
        var style = hasValue ? "green" : "dim";
        var node = parentNode.AddNode($"[{style}]{Markup.Escape(path)}[/{style}]");

        foreach (var childPath in cache.GetChildPaths(path))
        {
            BuildChildNode(node, childPath, cache);
        }
    }
}
