using System.Globalization;
using System.Text;

namespace coding.LowLevel;

/// <summary>
/// Routes keys clockwise on a ring of FNV-1a 32-bit virtual-node positions.
/// Adding or removing a server changes only the keys in its ring segments.
/// This teaching implementation is not thread-safe; lookups are O(V).
/// </summary>
public sealed class ConsistentHashRing
{
  private readonly SortedDictionary<uint, string> ring = [];

  private const uint OffsetBasis = 2166136261;
  private const uint Prime = 16777619;

  public void AddServer(string server, int virtualNodeCount = 100)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(server);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(virtualNodeCount);

    if (ring.Values.Contains(server))
    {
      throw new InvalidOperationException($"Server '{server}' is already on the ring.");
    }

    // Stage positions so a collision cannot leave a partially added server.
    var positions = new HashSet<uint>();
    for (int i = 0; i < virtualNodeCount; i++)
    {
      string virtualNode = $"{server}-{i.ToString(CultureInfo.InvariantCulture)}";
      uint position = Hash(virtualNode);

      if (ring.ContainsKey(position) || !positions.Add(position))
      {
        throw new InvalidOperationException(
          $"Hash collision while adding virtual node '{virtualNode}'.");
      }
    }

    foreach (uint position in positions)
    {
      ring.Add(position, server);
    }
  }

  /// <summary>
  /// Removes all virtual nodes owned by a server, regardless of their count.
  /// Removing a server that is not present is a no-op.
  /// </summary>
  public void RemoveServer(string server)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(server);

    foreach (uint position in ring
      .Where(node => node.Value == server)
      .Select(node => node.Key)
      .ToArray())
    {
      ring.Remove(position);
    }
  }

  public string GetServer(string key)
  {
    ArgumentNullException.ThrowIfNull(key);

    if (ring.Count == 0)
    {
      throw new InvalidOperationException("No servers are available.");
    }

    uint position = Hash(key);
    foreach (var node in ring)
    {
      if (node.Key >= position)
      {
        return node.Value;
      }
    }

    return ring.First().Value;
  }

  /// <summary>
  /// Computes FNV-1a over UTF-8 bytes with multiplication modulo 2^32.
  /// Unlike string.GetHashCode(), the result is stable between processes.
  /// FNV-1a is non-cryptographic and is not suitable for security purposes.
  /// </summary>
  public static uint Hash(string key)
  {
    ArgumentNullException.ThrowIfNull(key);

    uint hash = OffsetBasis;
    foreach (byte value in Encoding.UTF8.GetBytes(key))
    {
      hash = unchecked((hash ^ value) * Prime);
    }

    return hash;
  }

  public void PrintRing()
  {
    foreach (var node in ring)
    {
      Console.WriteLine($"{node.Key,12} -> {node.Value}");
    }
  }
}

/// <summary>
/// Run with: dotnet run -- --fnv1a
/// </summary>
public static class ConsistentHashRingDemo
{
  public static void Run()
  {
    var ring = new ConsistentHashRing();
    ring.AddServer("Server-A", 5);
    ring.AddServer("Server-B", 5);
    ring.AddServer("Server-C", 5);

    Console.WriteLine("=== FNV-1a HASH RING ===");
    ring.PrintRing();

    string[] keys = Enumerable.Range(1, 10)
      .Select(number => $"user-{number}")
      .ToArray();
    Dictionary<string, string> before = keys.ToDictionary(key => key, ring.GetServer);

    Console.WriteLine();
    Console.WriteLine("=== KEY ROUTING ===");
    foreach (string key in keys)
    {
      Console.WriteLine($"{key,-10} -> {before[key]}");
    }

    ring.AddServer("Server-D", 5);
    Dictionary<string, string> afterAdding = PrintChanges(
      "ADDING SERVER-D", ring, keys, before);

    ring.RemoveServer("Server-B");
    PrintChanges("REMOVING SERVER-B", ring, keys, afterAdding);
  }

  private static Dictionary<string, string> PrintChanges(
    string title,
    ConsistentHashRing ring,
    string[] keys,
    Dictionary<string, string> previous)
  {
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");

    Dictionary<string, string> current = keys.ToDictionary(key => key, ring.GetServer);
    foreach (string key in keys)
    {
      bool moved = previous[key] != current[key];
      Console.WriteLine(
        $"{key,-10} {previous[key]} -> {current[key]}{(moved ? " (moved)" : "")}");
    }

    int movedKeys = keys.Count(key => previous[key] != current[key]);
    Console.WriteLine($"Moved {movedKeys} of {keys.Length} keys.");
    return current;
  }
}
