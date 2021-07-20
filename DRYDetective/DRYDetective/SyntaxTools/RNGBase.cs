using System;

namespace DRYDetective.SyntaxTools
{
    public static class RNGBase
    {
        private static readonly Random random = new Random();
        public static int RandomNumber(int min, int max) => random.Next(min, max);
    }
}
