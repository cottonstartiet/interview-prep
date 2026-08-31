using System;

namespace coding.Solutions;

public class KeyboardDistance
{
  private static readonly string Keyboard = "abcdefghijklmnopqrstuvwxyz";

  public static int GetDistance(string input)
  {    
    if (string.IsNullOrEmpty(input))
    {
      throw new Exception("Input string can not be null or empty.");
    }

    int position = 0;
    int total = 0;
    Dictionary<char, int> keyPosition = [];

    for (int i = 0; i < Keyboard.Length; i++)
    {
      keyPosition.Add(Keyboard[i], i);
    }


    foreach (var ch in input)
    {
      int distance = Math.Abs(position - keyPosition[ch]);
      total += distance;
      position = keyPosition[ch];
    }

    return total;
  }
}
