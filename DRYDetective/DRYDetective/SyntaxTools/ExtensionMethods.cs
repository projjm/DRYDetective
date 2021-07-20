using System.Collections.Generic;
using System.Linq;

namespace DRYDetective.SyntaxTools
{
    public static class ExtensionMethods
    {
        public delegate bool IdPredicate<T, I>(T val, out I id);
        public static List<T> RemoveOnce<T, I>(this List<T> source, IdPredicate<T, I> match)
        {
            List<T> returnList = new List<T>(source);
            List<I> idMap = new List<I>();
            foreach (T value in source)
            {
                if (match(value, out I id) && !idMap.Contains(id))
                {
                    returnList.Remove(value);
                    idMap.Add(id);
                }
            }
            return returnList;
        }

        public static int Occurances<T>(this List<T> source, T target)
        {
            int occurances = 0;
            foreach (T val in source)
            {
                if (val.Equals(target))
                    occurances++;
            }
            return occurances;
        }

        public static void AddMultiple<T>(this List<T> source, T target, int amount)
        {
            for (int i = 0; i < amount; i++)
                source.Add(target);
        }

        public static void RemoveMultiple<T>(this List<T> source, T target, int amount)
        {
            int removals = 0;
            List<int> toRemoveIndexes = new List<int>();
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].Equals(target))
                {
                    toRemoveIndexes.Add(i);
                    removals++;
                    if (removals == amount)
                        break;
                }
            }

            foreach (int index in toRemoveIndexes.OrderByDescending(i => i))
            {
                source.RemoveAt(index);
            }
        }

        public static void Increment<K>(this Dictionary<K, int> source, K key)
        {
            if (source.ContainsKey(key))
                source[key]++;
            else
                source.Add(key, 1);
        }
    }
}
