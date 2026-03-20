using TreeMemoryCache;

Console.OutputEncoding = System.Text.Encoding.UTF8;

using var cache = new TreeMemoryCache.TreeMemoryCache();

cache.SetTree("Line:6:Upward:Stations", new[] { "A站", "B站", "C站" }).Dispose();
cache.SetTree("Line:6:Downward:Stations", new[] { "C站", "B站", "A站" }).Dispose();
cache.SetTree("Line:8:Upward:Stations", new[] { "X站", "Y站" }).Dispose();




if (cache.TryGetTree<string[]>("Line:6:Upward:Stations", out var stations))
{
    Console.WriteLine($"读取成功: {string.Join(" -> ", stations!)}");
}

var children = cache.GetChildPaths("Line:6").ToList();
Console.WriteLine($"Line:6 直接子节点: {string.Join(", ", children)}");

var upwardPaths = cache.GetPathsByPattern("Line:*:Upward:*").ToList();
Console.WriteLine($"匹配 Line:*:Upward:* 的路径数: {upwardPaths.Count}");

using (var batch = cache.CreateBatch())
{
    batch.Set("Line:9:Config", "Enable")
         .Set("Line:9:Version", 1)
         .Remove("Line:8:Upward:Stations");
    batch.Execute();
}

cache.RemoveTree("Line:6");
Console.WriteLine($"删除 Line:6 后仍可读取 Line:9:Config: {cache.TryGetTree<string>("Line:9:Config", out _)}");

Console.WriteLine("异步删除 Line:9:");
await foreach (var removedPath in cache.RemoveTreeAsync("Line:9"))
{
    Console.WriteLine($" - {removedPath}");
}

var stats = cache.GetStatistics();
Console.WriteLine($"统计 => 节点数: {stats.TotalNodeCount}, 命中: {stats.HitCount}, 未命中: {stats.MissCount}, 级联删除次数: {stats.CascadeDeleteCount}");
