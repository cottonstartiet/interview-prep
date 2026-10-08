using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace coding.Solutions;

/// <summary>
/// Maps keys to nodes arranged on a logical circle (the "hash ring").
///
/// With ordinary modulo hashing, a key is often assigned with:
///     hash(key) % numberOfNodes
/// Changing the number of nodes changes the divisor, so most keys move.
///
/// Consistent hashing places both nodes and keys on the same ring. A key belongs
/// to the first node encountered clockwise from the key's position. When a node
/// is added or removed, only keys in the neighboring section of the ring move.
/// This is useful when scaling caches, databases, and other partitioned systems.
/// </summary>
public sealed class ConsistentHashRing
{
  private readonly SortedDictionary<uint, string> ring = [];
  private readonly int virtualNodesPerNode;

  public ConsistentHashRing(int virtualNodesPerNode = 100)
  {
    if (virtualNodesPerNode <= 0)
    {
      throw new ArgumentOutOfRangeException(
        nameof(virtualNodesPerNode),
        "The number of virtual nodes must be greater than zero.");
    }

    this.virtualNodesPerNode = virtualNodesPerNode;
  }

  /// <summary>
  /// Adds several positions for one physical node.
  ///
  /// These positions are called virtual nodes. Without them, a physical node
  /// may randomly receive a very large or very small section of the ring.
  /// More virtual nodes generally produce a more even key distribution.
  /// </summary>
  public void AddNode(string node)
  {
    ValidateNode(node);

    for (int replica = 0; replica < virtualNodesPerNode; replica++)
    {
      uint position = Hash($"{node}#{replica}");

      if (!ring.TryAdd(position, node))
      {
        throw new InvalidOperationException(
          $"Hash collision while adding virtual node '{node}#{replica}'.");
      }
    }
  }

  public void RemoveNode(string node)
  {
    ValidateNode(node);

    foreach (uint position in ring
      .Where(entry => entry.Value == node)
      .Select(entry => entry.Key)
      .ToArray())
    {
      ring.Remove(position);
    }
  }

  public string GetNode(string key)
  {
    ArgumentNullException.ThrowIfNull(key);

    if (ring.Count == 0)
    {
      throw new InvalidOperationException("Add at least one node before assigning keys.");
    }

    uint keyPosition = Hash(key);

    // Move clockwise to the first node at or after the key's position.
    // If there is none, wrap around to the first position on the circle.
    foreach (var entry in ring)
    {
      if (entry.Key >= keyPosition)
      {
        return entry.Value;
      }
    }

    return ring.First().Value;
  }

  private static uint Hash(string value)
  {
    // string.GetHashCode() is randomized between .NET processes. SHA-256 gives
    // every machine the same ring positions, which a distributed system needs.
    byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return BinaryPrimitives.ReadUInt32BigEndian(digest);
  }

  private static void ValidateNode(string node)
  {
    if (string.IsNullOrWhiteSpace(node))
    {
      throw new ArgumentException("A node name cannot be empty.", nameof(node));
    }
  }
}
