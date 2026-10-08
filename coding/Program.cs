using coding.Solutions;

if (args.Contains("--fnv1a", StringComparer.Ordinal))
{
  coding.LowLevel.ConsistentHashRingDemo.Run();
  return;
}

// Imagine these keys are user sessions stored in a distributed cache.
string[] keys = Enumerable.Range(1, 1_000)
  .Select(number => $"user-session-{number}")
  .ToArray();

var ring = new ConsistentHashRing(virtualNodesPerNode: 100);
ring.AddNode("cache-A");
ring.AddNode("cache-B");
ring.AddNode("cache-C");

Dictionary<string, string> assignmentsBeforeScaling = keys.ToDictionary(
  key => key,
  key => ring.GetNode(key));

PrintDistribution("Before scaling", assignmentsBeforeScaling.Values);

// Scale out by adding one cache server. Consistent hashing does not reshuffle
// every key: primarily, the new node takes portions from its ring neighbors.
ring.AddNode("cache-D");

Dictionary<string, string> assignmentsAfterScaling = keys.ToDictionary(
  key => key,
  key => ring.GetNode(key));

PrintDistribution("After adding cache-D", assignmentsAfterScaling.Values);

int movedKeys = keys.Count(
  key => assignmentsBeforeScaling[key] != assignmentsAfterScaling[key]);

Console.WriteLine();
Console.WriteLine(
  $"Consistent hashing moved {movedKeys} of {keys.Length} keys " +
  $"({movedKeys * 100.0 / keys.Length:F1}%).");

// Compare that result with the common hash(key) % nodeCount approach.
// Adding a fourth node changes the divisor from 3 to 4, moving most keys.
int moduloMovedKeys = keys.Count(key =>
  StableModulo(key, nodeCount: 3) != StableModulo(key, nodeCount: 4));

Console.WriteLine(
  $"Modulo hashing would move {moduloMovedKeys} of {keys.Length} keys " +
  $"({moduloMovedKeys * 100.0 / keys.Length:F1}%).");

Console.WriteLine();
Console.WriteLine("Example assignments after scaling:");
foreach (string key in keys.Take(10))
{
  Console.WriteLine(
    $"{key,-18} {assignmentsBeforeScaling[key],-8} -> {assignmentsAfterScaling[key]}");
}

static void PrintDistribution(string title, IEnumerable<string> assignments)
{
  Console.WriteLine(title);

  foreach (var group in assignments.GroupBy(node => node).OrderBy(group => group.Key))
  {
    Console.WriteLine($"  {group.Key}: {group.Count()} keys");
  }
}

static int StableModulo(string key, int nodeCount)
{
  // This simple deterministic hash is sufficient for comparing remapping.
  // The consistent-hash ring itself uses SHA-256 for stable ring positions.
  uint hash = 2166136261;
  foreach (byte value in System.Text.Encoding.UTF8.GetBytes(key))
  {
    hash = (hash ^ value) * 16777619;
  }

  return (int)(hash % nodeCount);
}