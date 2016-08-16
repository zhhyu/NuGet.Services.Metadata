using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CollectorSample.RegistrationPoc
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> Paged<T>(this IEnumerable<T> source, int pageSize)
        {
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var currentPage = new List<T>(pageSize)
                    {
                        enumerator.Current
                    };

                    while (currentPage.Count < pageSize && enumerator.MoveNext())
                    {
                        currentPage.Add(enumerator.Current);
                    }

                    yield return new ReadOnlyCollection<T>(currentPage);
                }
            }
        }
    }
}