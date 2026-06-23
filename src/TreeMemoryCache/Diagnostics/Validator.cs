using System.Collections.Concurrent;

namespace TreeMemoryCache.Diagnostics;

internal static class Validator
{
    public static ValidationResult Validate(
        ConcurrentDictionary<string, CacheNode> nodes,
        ConcurrentDictionary<string, HashSet<string>> tagIndex)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var (path, node) in nodes)
        {
            if (node.ParentPath is not null && !nodes.ContainsKey(node.ParentPath))
            {
                errors.Add($"DeadParentLink: {path} 指向不存在的父节点 {node.ParentPath}");
            }
        }

        var nodeTags = nodes.Values
            .Where(n => n.Tag is not null)
            .Select(n => n.Tag!)
            .ToHashSet();

        foreach (var tag in tagIndex.Keys)
        {
            if (!nodeTags.Contains(tag))
            {
                warnings.Add($"孤立标签索引: {tag}");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
}
