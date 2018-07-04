using MistyScrew.Functional;
using MistyScrew.QSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MistyScrew.QThinking
{
    public static class QNodeHelper2
    {
        [System.Diagnostics.DebuggerStepThrough]
        public static TValue Maybe<TItem, TValue>(this TItem item, Func<TItem, TValue> f)
        {
            if (item == null || f == null)
                return default(TValue);
            return f(item);
        }
        [System.Diagnostics.DebuggerStepThrough]
        public static TValue? MaybeValue<TItem, TValue>(this TItem item, Func<TItem, TValue> f)
          where TValue : struct
        {
            if (item == null || f == null)
                return null;
            return f(item);
        }
        [System.Diagnostics.DebuggerStepThrough]
        public static void Maybe<TItem>(this TItem item, Action<TItem> action)
        {
            if (item == null || action == null)
                return;
            action(item);
        }

        public static string AsString(this IEnumerable<QNode> nodes)
        {
            return nodes.OrEmpty().FirstOrDefault()?.Value?.ToString();
        }

        public static IEnumerable<QNode> Nodes(this QNode node)
        {
            return (node?.Nodes).OrEmpty();
        }
        public static IEnumerable<QNode> QValue(this QNode node)
        {
            return (node?.Nodes).OrEmpty();
        }
        public static IEnumerable<QNode> QValue(this IEnumerable<QNode> nodes)
        {
            return nodes.OrEmpty().SelectMany(node => node?.Nodes);
        }
        public static object RawValue(this QNode node)
        {
            return node?.Value;
        }
        public static object RawValue(this IEnumerable<QNode> nodes)
        {
            return nodes.OrEmpty().FirstOrDefault()?.Value;
        }
        public static IEnumerable<QNode> P(this QNode node, string name)
        {
            return node?.Nodes.Where(n => n.AsString() == name);
        }
    }
    public interface IQNodeBuilder { }

    public static class QNodeHelper
    {
        public static QNode Q(this IQNodeBuilder q, object value, params object[] content)
        {
            if (value == null)
                return null;
            var childs = new List<QNode>();
            foreach (var item in content)
            {
                if (item == null)
                    continue;
                var q_item = item as QNode;
                if (q_item != null)
                {
                    childs.Add(q_item);
                    continue;
                }
                var qs_item = item as IEnumerable<QNode>;
                if (qs_item != null)
                {
                    childs.AddRange(qs_item);
                    continue;
                }
                childs.Add(new QNode(item));
            }
            return new QNode(value, childs.ToArray());
        }
        public static QNode[] Qs(this IQNodeBuilder q, object value, params object[] content)
        {
            var node = Q(q, value, content);
            if (node == null)
                return null;
            return new[] { node };
        }
        public static QNode W(this QNode q, params object[] content)
        {
            if (q == null)
                return null;
            return new QNode(q.RawValue(), q.Nodes().Concat(content.Select(item => item is QNode ? (QNode)item : new QNode(item))).ToArray());
        }
        public static QNode C(this QNode q, int index) => q?.Nodes.ElementAtOrDefault(index);

        public static IEnumerable<QNode> C_s(this QNode q, int index) => q?.Nodes.Skip(index);

        public static IEnumerable<TDest> Wave<TAcc, TSource, TDest>(this IEnumerable<TSource> items, TAcc a, Func<TAcc, TSource, Tuple<TAcc, TDest>> f)
        {
            foreach (var item in items)
            {
                var r = f(a, item);
                yield return r.Item2;
                a = r.Item1;
            }
        }

        public static QNode P_(this QNode node, params object[] path)
        {
            return P_s(node, path).FirstOrDefault();
        }
        public static QNode P_(this IEnumerable<QNode> nodes, params object[] path)
        {
            return P_s(nodes, path).FirstOrDefault();
        }

        public static IEnumerable<QNode> P_s(this QNode node, params object[] path)
        {
            return P_s(Enumerable.Repeat(node, node != null ? 1 : 0), path);
        }
        public static IEnumerable<QNode> P_s(this IEnumerable<QNode> nodes, params object[] path)
        {
            foreach (var entry in path)
            {
                var i = entry as int?;
                string s = null;
                if (i == null)
                {
                    s = entry?.ToString();
                    if (!s.IsNullOrEmpty() && char.IsDigit(s[0]))
                        i = ConvertHlp.ToInt(s);
                }
                if (i != null)
                    nodes = nodes.Select(n => n.Nodes().ElementAtOrDefault(i.Value)).Where(n => n != null);
                else if (s == "*")
                    nodes = nodes.SelectMany(n => n.Nodes());
                else
                    nodes = nodes.SelectMany(n => n.P(s));
            }
            return nodes;
        }
        public static IEnumerable<QNode> W_s(this IEnumerable<QNode> nodes, IEnumerable<object> path, QNode v)
        {
            if (path == null || !path.Any())
                return To_s(v);

            var entry = path.FirstOrDefault();

            var qentry = entry as QNode;
            var i = entry as int?;
            string s = null;
            var isAddIfNeed = false;
            var isOnlySingle = false;
            if (qentry != null)
            {
                s = qentry.AsString();
                isAddIfNeed = qentry.Nodes().Any(n => n.AsString() == "+");
                isOnlySingle = qentry.Nodes().Any(n => n.AsString() == "!" || n.AsString() == "single");
            }
            else if (i == null)
            {
                s = entry?.ToString();
                if (!s.IsNullOrEmpty() && char.IsDigit(s[0]))
                    i = ConvertHlp.ToInt(s);
            }

            if (i != null)
                return nodes.Select(n => q.Q(n.RawValue(), n.Nodes().Skip(i.Value).Concat(W_s(To_s(n.Nodes().ElementAtOrDefault(i.Value)), path.Skip(1), v)).Concat(n.Nodes().Skip(i.Value + 1))));
            else if (s == "*")
                return nodes.Select(n => q.Q(n.RawValue(), W_s(To_s(n), path.Skip(1), v)));
            else
                return nodes.Select(n =>
                {
                    var childs = new List<QNode>();
                    var isUpdated = false;
                    foreach (var node in n.Nodes())
                    {
                        if (node.AsString() == s)
                        {
                            if (!isOnlySingle || !isUpdated)
                                childs.AddRange(W_s(To_s(node), path.Skip(1), v));
                            isUpdated = true;
                        }
                        else
                            childs.Add(node);
                    }
                    if (isAddIfNeed && !isUpdated)
                        childs.AddRange(W_s(q.Qs(s), path.Skip(1), v));
                    return q.Q(n.RawValue(), childs);
                }
                );
        }

        public static IEnumerable<QNode> To_s(this QNode node)
        {
            return Enumerable.Repeat(node, node != null ? 1 : 0);
        }


        public static readonly IQNodeBuilder q = null;
    }
}