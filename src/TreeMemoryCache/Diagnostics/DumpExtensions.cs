using System.IO;
using System.Text;

namespace TreeMemoryCache.Diagnostics;

/// <summary>
/// 树形缓存的文本可视化扩展。
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
    /// <item>■ 前缀:路径上有实际缓存值</item>
    /// <item>□ 前缀:仅作为中间路径节点(无缓存值)</item>
    /// </list>
    /// 树形连线使用标准 Box-Drawing 字符:├─ └─ │ 。
    /// </remarks>
    public static void Dump(this TreeMemoryCache cache, TextWriter? writer = null)
    {
        writer ??= Console.Out;

        var sb = new StringBuilder();
        sb.AppendLine("TreeMemoryCache");

        foreach (var (path, _) in cache.GetRootNodes())
        {
            AppendNode(sb, path, cache, isLast: false, prefix: "");
        }

        writer.Write(sb.ToString());
    }

    private static void AppendNode(StringBuilder sb, string path, TreeMemoryCache cache, bool isLast, string prefix)
    {
        var hasValue = cache.TryGetValue(path, out _);
        var marker = hasValue ? "■ " : "□ ";
        var connector = isLast ? "└─ " : "├─ ";
        sb.Append(prefix);
        sb.Append(connector);
        sb.Append(marker);
        sb.AppendLine(path);

        var childPaths = cache.GetChildPaths(path).ToList();
        for (var i = 0; i < childPaths.Count; i++)
        {
            var childIsLast = i == childPaths.Count - 1;
            var childPrefix = prefix + (isLast ? "    " : "│   ");
            AppendNode(sb, childPaths[i], cache, childIsLast, childPrefix);
        }
    }
}
