using System;
using System.Collections.Generic;

namespace Basalt.Core
{
    public static class ProfileView
    {
        public static List<Profile> Filter(List<Profile> source, string query, string group, bool favorites, bool sortByLatency, IDictionary<string, int> latency)
        {
            var result = source.FindAll(delegate(Profile p)
            {
                return (!favorites || p.Favorite) && (group == null || (group == "manual" ? p.SubscriptionId == null : p.SubscriptionId == group)) &&
                    (String.IsNullOrWhiteSpace(query) || (p.Name + " " + p.Server + " " + p.Protocol).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            });
            result.Sort(delegate(Profile a, Profile b)
            {
                if (sortByLatency)
                {
                    int av, bv; if (!latency.TryGetValue(a.Id, out av) || av < 0) av = Int32.MaxValue;
                    if (!latency.TryGetValue(b.Id, out bv) || bv < 0) bv = Int32.MaxValue;
                    if (av != bv) return av.CompareTo(bv);
                }
                return StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
            });
            return result;
        }
    }
}
