using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Basalt.Core
{
    // Event parser only: no CLR object deserialization or provider-defined types.
    public sealed class BoundedYaml
    {
        readonly IParser parser; int nodes;
        readonly Dictionary<string, object> anchors = new Dictionary<string, object>();
        BoundedYaml(string text) { parser = new Parser(new StringReader(text)); }
        public static object Read(string text)
        {
            try
            {
                var reader = new BoundedYaml(text); reader.parser.MoveNext(); reader.Next<StreamStart>(); reader.Next<DocumentStart>();
                object result = reader.Node(0); reader.Next<DocumentEnd>();
                if (!(reader.parser.Current is StreamEnd)) throw new FormatException(L10n.T("Clash: ожидается один YAML-документ.")); return result;
            }
            catch (YamlException) { throw new FormatException(L10n.T("Некорректный Clash YAML.")); }
        }
        void Next<T>() where T : ParsingEvent { if (!(parser.Current is T)) throw new FormatException(L10n.T("Некорректная структура YAML.")); parser.MoveNext(); }
        void Limit(int depth) { if (++nodes > 30000 || depth > 48) throw new FormatException(L10n.T("YAML превышает предел сложности.")); }
        object Copy(object value, int depth)
        {
            Limit(depth); var map = value as Dictionary<string, object>; if (map != null) { var copy = new Dictionary<string, object>(); foreach (var pair in map) copy.Add(pair.Key, Copy(pair.Value, depth + 1)); return copy; }
            var list = value as IList; if (list != null) { var copy = new List<object>(); foreach (var item in list) copy.Add(Copy(item, depth + 1)); return copy; } return value;
        }
        object Node(int depth)
        {
            Limit(depth); var alias = parser.Current as AnchorAlias;
            if (alias != null) { object value; if (!anchors.TryGetValue(alias.Value, out value)) throw new FormatException(L10n.T("YAML: циклический или неизвестный alias.")); parser.MoveNext(); return Copy(value, depth + 1); }
            var node = parser.Current as NodeEvent; if (node == null) throw new FormatException(L10n.T("Ожидался узел YAML.")); string anchor = node.Anchor;
            object result; var scalar = node as Scalar;
            if (scalar != null) { result = scalar.Value; parser.MoveNext(); }
            else if (node is SequenceStart)
            { var list = new List<object>(); parser.MoveNext(); while (!(parser.Current is SequenceEnd)) list.Add(Node(depth + 1)); parser.MoveNext(); result = list; }
            else if (node is MappingStart)
            {
                var map = new Dictionary<string, object>(); var explicitKeys = new HashSet<string>(); parser.MoveNext();
                while (!(parser.Current is MappingEnd))
                {
                    string key = Node(depth + 1) as string; if (key == null || !explicitKeys.Add(key)) throw new FormatException(L10n.T("YAML: повторяющийся или сложный ключ."));
                    object value = Node(depth + 1);
                    if (key == "<<") { var merged = value as Dictionary<string, object>; if (merged == null) throw new FormatException(L10n.T("YAML merge требует mapping.")); foreach (var pair in merged) if (!map.ContainsKey(pair.Key)) map[pair.Key] = pair.Value; }
                    else map[key] = value;
                }
                parser.MoveNext(); result = map;
            }
            else throw new FormatException(L10n.T("Неизвестный YAML-узел."));
            if (!String.IsNullOrEmpty(anchor)) { if (anchors.ContainsKey(anchor)) throw new FormatException(L10n.T("Повторяющийся YAML anchor.")); anchors.Add(anchor, result); } return result;
        }
    }
}
