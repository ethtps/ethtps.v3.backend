namespace ETHTPS.V3.LiveDataUpdater.Extensions
{
    public static class LinqExtensions
    {
        public static IEnumerable<IEnumerable<T>> Split<T>(this IEnumerable<T> list, int parts)
        {
            var collection = list is ICollection<T> c
                ? c
                : list.ToList();

            var itemCount = collection.Count;

            // return all items if source list is too short to split up
            if (itemCount < parts)
            {
                yield return collection;
                yield break;
            }

            var itemsInEachChunk = itemCount / parts;

            var chunks = itemCount % parts == 0
                ? parts
                : parts - 1;

            var itemsToChunk = chunks * itemsInEachChunk;

            foreach (var chunk in collection.Take(itemsToChunk).Chunk(itemsInEachChunk))
            {
                yield return chunk;
            }

            if (itemsToChunk < itemCount)
            {
                yield return collection.Skip(itemsToChunk);
            }
        }
    }
}
