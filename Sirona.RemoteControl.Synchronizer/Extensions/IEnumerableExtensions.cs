namespace Sirona.RemoteControl.Synchronizer.Extensions;

public static class EnumerableExtensions
{
    public static int IndexWhere<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        int index = 0;

        foreach (T item in source)
        {
            if (predicate(item))
            {
                return index;
            }

            index++;
        }

        return -1;
    }
}