using System.IO;
using Spectre.Console;

namespace TreeMemoryCache.Diagnostics;

/// <summary>
/// 树形缓存的文本可视化扩展,基于 Spectre.Console。
/// </summary>
public static class DumpExtensions
{
    /// <summary>
    /// 输出缓存树的文本表示到控制台或指定 <see cref="TextWriter"/>。
    /// </summary>
    /// <param name="cache">目标缓存实例。</param>
    /// <param name="writer">输出流,<c>null</c> 时写入 <see cref="Console.Out"/>。</param>
    /// <remarks>
    /// 输出格式为分层树形:
    /// <list type="bullet">
    /// <item>绿色节点:路径上有实际缓存值</item>
    /// <item>灰色节点:仅作为中间路径节点存在(无缓存值)</item>
    /// </list>
    /// </remarks>
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
