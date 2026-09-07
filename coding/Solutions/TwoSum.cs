using System;

namespace coding.Solutions;

public class TwoSum
{
  public static Tuple<int, int>? GetTwoSumResult(int[] nums, int target)
  {
    HashSet<int> map = [];

    foreach (var num in nums)
    {
      map.Add(num);
    }

    foreach (var num in nums)
    {
      if (map.Contains(target-num))
      {
        return new Tuple<int, int>(num, target-num);
      }
    }

    return null;
  }
}
