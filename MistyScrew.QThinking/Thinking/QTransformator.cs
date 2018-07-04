using MistyScrew.Functional;
using MistyScrew.QSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MistyScrew.QThinking
{
    public class QTransformator
    {
        public static IEnumerable<QNode> ProcessAll(IEnumerable<QNode> qs, Dictionary<string, QNode[]> functionIndex)
        {
            var typed_qs = Typified(qs
              .Select(node => q.Q("point", q.Q("id", node.RawValue()), node.Nodes()))
              .ToArray(),
              functionIndex
            );
            foreach (var normalizator in AllNormalizators)
                typed_qs = Transform(typed_qs, normalizator);
            return typed_qs;
        }

        public static IEnumerable<Tuple<string, T>> Browse<T>(IEnumerable<QNode> nodes, Func<QNode, T> f)
        {
            foreach (var node in nodes)
            {
                var name = node.P("id").QValue().Skip(1).FirstOrDefault().AsString();
                foreach (var notify in Browse(node, f))
                    yield return new Tuple<string, T>(name, notify);
            }
        }
        public static IEnumerable<T> Browse<T>(QNode node, Func<QNode, T> f)
        {
            var r = f(node);
            return Enumerable.Repeat(r, ((object)r != null) ? 1 : 0)
              .Concat(node.Nodes().Skip(1).SelectMany(child => Browse(child, f)));
        }
        public static IEnumerable<QNode> Transform(IEnumerable<QNode> nodes, Func<QNode, Func<QNode, QNode>, QNode> f)
        {
            return nodes.Select(node => Transform(node, f) ?? node);
        }
        public static QNode Transform(QNode node, Func<QNode, Func<QNode, QNode>, QNode> f)
        {
            if (node == null)
                return null;
            var r = f(node, n => Transform(n, f));
            if (r != null && r != node)
                return r;
            List<QNode> childs = null;
            var nodes = node.Nodes();
            //for (var i = 1; i < nodes.Length; ++i)
            foreach (var pair in nodes.Select((n, i) => new { child = n, i }).Skip(1))
            {
                var child = pair.child;

                var r_child = Transform(child, f);
                if (r_child != null && r_child != child && childs == null)
                {
                    childs = new List<QNode>(nodes.Take(pair.i));
                }
                childs?.Add(r_child ?? child);
            }
            if (childs != null)
                return new QNode(node.RawValue(), childs.ToArray());
            return node;
        }

        public static string Error(QNode typedNode)
        {
            var type = typedNode.C(0).P("type").QValue().AsString();
            if (type == "-")
                return $"Не задан тип '{type}' для '{typedNode.AsString()}'";
            if (!typedNode.C(0).P("inter-type").QValue().Any())
                return $"Не задан inter-type для '{typedNode.AsString()}'";
            return null;
        }
        public static QNode MetaType(string type, string childType = null, string interType = null, string[] interTypes = null)
        {
            return q.Q("meta", type != null ? q.Q("type", q.Q(type, childType)) : null, (interType ?? (object)interTypes) != null ? q.Q("inter-type", interTypes?.Select(s => q.Q(s)) ?? (object)interType) : null);
        }

        public static readonly Func<QNode, Func<QNode, QNode>, QNode>[] AllNormalizators = new Func<QNode, Func<QNode, QNode>, QNode>[]
        {
          Normalizator, Normalizator2, Normalizator3,
          Normalizator4, Normalizator5_Lift
        };

        public static QNode Normalizator(QNode typedNode, Func<QNode, QNode> normalize)
        {
            var s = typedNode.AsString();
            switch (s)
            {
                case "число":
                    return normalize(typedNode.C(1)) ?? typedNode.C(1);
                case "skip":
                    return q.Q(typedNode.RawValue(),
                      typedNode.C(0),
                      typedNode.C(1).Maybe(reasonNode =>
                        q.Q(reasonNode.RawValue(),
                          MetaType("s-literal", interType: "void"),
                          reasonNode.Nodes().OrEmpty().Skip(1)
                        )
                      )
                    );
                case "point":
                    {
                        var pointNode = q.Q(typedNode.RawValue(),
                          typedNode.C(0),
                          typedNode.C(1).Maybe(taskNode => taskNode.AsString() == "id" ? normalize(taskNode) ?? taskNode : q.Q("id", MetaType("id"), normalize(taskNode) ?? taskNode)),
                          typedNode.C(2).Maybe(taskNode => taskNode.AsString() == "task" ? normalize(taskNode) ?? taskNode : q.Q("task", MetaType("task"), normalize(taskNode) ?? taskNode)),
                          typedNode.C(3).Maybe(expectedNode => expectedNode.AsString() == "expected" ? normalize(expectedNode) ?? expectedNode : q.Q("expected", MetaType("expected"), normalize(expectedNode) ?? expectedNode))
                        );
                        return q.Q(pointNode.RawValue(),
                          pointNode.C(0),
                          pointNode.C(1),
                          q.Q("eq", MetaType("bool"), ToCollection_IfNeed(pointNode.C(3).QValue()), ToCollection_IfNeed(pointNode.C(2).QValue()))
                        );
                    }
                case "::query":
                    {
                        var childs = typedNode.Nodes.Skip(1);
                        var result = childs.Skip(1).Aggregate(childs.FirstOrDefault(), (current, child) => q.Q(child.Value, child.Nodes.FirstOrDefault(), current, child.Nodes().Skip(1)));
                        return normalize(result);
                    }
                default:
                    {
                        var childs = typedNode.Nodes().Skip(1).ToArray();
                        //if (!new[] { "point", "skip", "is?", "and", "частное" }.Contains(s) && childs.Length > 1)
                        if (new[] { "from" }.Contains(s) && childs.Length > 1)
                        {
                            var childType = childs[0].C(0).P("type").QValue().AsString();
                            if (!childType.IsNullOrEmpty() && childType != "-")
                            {
                                if (childs.Skip(1).All(child => child.C(0).P("type").QValue().AsString() == childType))
                                {
                                    if (!childType.EndsWith("-s"))//TODO поддержать коллекции коллекций и смешение одиночек и коллекций
                                        return q.Q(typedNode.RawValue(), typedNode.C(0), q.Q("collection", MetaType(childType + "-s", interType: childType), childs));
                                }
                            }
                        }
                    }
                    break;
            }
            return typedNode;
        }
        static QNode ToCollection_IfNeed(IEnumerable<QNode> _nodes)
        {
            var nodes = _nodes.OrEmpty().ToArray();
            if (nodes.Length <= 1)
                return q.Q("void", MetaType("void", interType: "void"));
            if (nodes.Length > 2)
            {
                var type = nodes[1].P_(0, "type", "*").AsString();
                return q.Q("collection", MetaType(type + "-s", interType: type), nodes.Skip(1));
            }
            return nodes[1];
        }
        public static QNode Normalizator2(QNode typedNode, Func<QNode, QNode> normalize)
        {
            var s = typedNode.AsString();
            switch (s)
            {
                case "choose":
                    {
                        if (typedNode.C(1).AsString() == "from" && typedNode.C(2).AsString() == "condition"
                          && typedNode.C(1).Nodes().Count() <= 2 && typedNode.C(2).Nodes().Count() <= 2)
                        {
                            return q.Q("and", MetaType("-"), normalize(typedNode.C(1).C(1)) ?? typedNode.C(1).C(1), normalize(typedNode.C(2).C(1)) ?? typedNode.C(2).C(1));
                        }
                    }
                    break;
            }
            return null;
        }

        public static QNode Normalizator3(QNode typedNode, Func<QNode, QNode> normalize)
        {
            var s = typedNode.AsString();
            switch (s)
            {
                case "not":
                    {
                        var childType = typedNode.C(1).C(0).P("type").QValue().AsString();
                        if (childType != null)
                            return q.Q(typedNode.RawValue(), MetaType(childType, interType: childType), typedNode.C_s(1).OrEmpty().Select(child => normalize(child) ?? child));
                    }
                    break;
                case "::query":
                    {
                        var childType = typedNode.Nodes().LastOrDefault().C(0).P("type").QValue().AsString();
                        if (childType != null)
                            return q.Q(typedNode.RawValue(), MetaType(childType, interType: childType), typedNode.C_s(1).OrEmpty().Select(child => normalize(child) ?? child));
                    }
                    break;
                case "and":
                    {
                        var childs = typedNode.C_s(1).OrEmpty().Select(child => normalize(child) ?? child).ToArray();
                        if (childs.All(n => Is("set", n.C(0).P("type").QValue().AsString())))
                            return q.Q(typedNode.RawValue(), MetaType("set", interType: "set"), childs);
                    }
                    break;
            }
            return null;
        }
        public static QNode Normalizator4(QNode typedNode, Func<QNode, QNode> normalize)
        {
            var s = typedNode.AsString();
            switch (s)
            {
                case "eq":
                    {
                        var childType = typedNode.C(1).C(0).P("type").QValue().AsString();
                        var childType2 = typedNode.C(2).C(0).P("type").QValue().AsString();
                        if (childType == "int" && childType2 == "number")
                            childType = "number";
                        if (childType == "int" && childType2 == "set")
                            childType = "int-s";
                        if (childType == "int" && childType2 == "int-s")
                            childType = "int-s";
                        if (childType == "int-s" && childType2 == "int")
                            childType = "int-s";
                        return q.Q(typedNode.RawValue(), MetaType("bool", interType: childType), typedNode.Nodes().Skip(1).Select(child => normalize(child) ?? child));
                    }
            }
            return typedNode;
        }

        public class Lifter
        {
            public Lifter(string targetType, string sourceType, params string[] functions)
            {
                this.TargetType = targetType;
                this.SourceType = sourceType;
                this.Functions = functions;
            }
            public readonly string TargetType;
            public readonly string SourceType;
            public readonly string[] Functions;
        }
        public static readonly Lifter[] Lifters = new[]
        {
          new Lifter("int-s", "set", "enumerate"),
          new Lifter("int-s", "int", "collection"),
          new Lifter("number", "int", "to-number"),
          new Lifter("set", "int-s", "set"),
          new Lifter("set", "int", "set", "collection"),
        };
        static readonly Dictionary<string, Lifter[]> LifterIndex = Lifters.GroupBy(lifter => lifter.TargetType).ToDictionary(group => group.Key, group => group.ToArray());

        public static QNode Normalizator5_Lift(QNode typedNode, Func<QNode, QNode> normalize)
        {
            var s = typedNode.AsString();
            switch (s)
            {
                case "eq":
                case "and":
                case "sum":
                case "min":
                case "max":
                case "count":
                case "is?":
                case "take":
                    {
                        var interTypes = typedNode.P_s(0, "inter-type", "*").OrEmpty().Select(interType => interType.AsString()).ToArray();

                        var childs = typedNode.Nodes().Skip(1)
                            .Select((child, i) =>
                            {
                                var interType = interTypes.ElementAtOrDefault(i) ?? interTypes.FirstOrDefault();
                                var type = child.P_(0, "type", "*").AsString();
                                return new { child, nchild = Lift(interType, type, normalize(child) ?? child) };
                            });
                        if (childs.Any(pair => pair.child != pair.nchild))
                            return q.Q(typedNode.RawValue(), typedNode.C(0), childs.Select(pair => pair.nchild));
                    }
                    break;
                    //case "::query":
                    //    {
                    //        var childs = typedNode.Nodes.Skip(1);
                    //        var prev = childs.FirstOrDefault();
                    //        List<QNode> nchilds = null;
                    //        foreach (var pair in childs.Select((c, i) => new { child = c, i }))
                    //        {
                    //            var child = pair.child;

                    //            var sourceType = prev.P_(0, "type", "*").AsString();
                    //            var targetType = child.AsString() == "take" ? "int-s" : null;
                    //            var nchild = Lift(targetType, sourceType, normalize(prev));
                    //            if (nchild != child && childs == null)
                    //            {
                    //                nchilds = new List<QNode>(typedNode.Nodes.Take(pair.i + 1));
                    //            }

                    //            if (nchilds != null)
                    //                nchilds.Add(nchild);
                    //        }
                    //        if (nchilds != null)
                    //            return q.Q(typedNode.Value, nchilds);
                    //    }
                    //    break;
            }
            return typedNode;
        }
        static Lifter FindLift(string targetType, string sourceType)
        {
            var lifters = LifterIndex.Find(targetType);
            return lifters?.FirstOrDefault(lift => lift.SourceType == sourceType);
        }
        static QNode Lift(string targetType, string sourceType, QNode node)
        {
            var lift = FindLift(targetType, sourceType);
            if (lift == null)
                return node;
            foreach (var function in lift.Functions.Reverse())
            {
                node = q.Q(function, MetaType(lift.TargetType, interType: lift.SourceType), node);//TODO для множественных преобразований корректно выставлять типы
            }
            return node;
        }

        public static bool IsAnyType(QNode type)
        {
            var s = type.AsString();
            return s == "s-literal";
        }

        static bool Is(string expectedType, string type)
        {
            if (expectedType == "set")
                //return type == "int-s" || type == "number-s" || type == "set" || type == "fraction-s";
                return type == "set" || type.EndsWith("-s");
            return false;
        }

        public static QNode Typified(QNode node, Dictionary<string, QNode[]> functionIndex)
        {
            var type = "-";
            string childType = null;
            string interType = null;
            string[] interTypes = null;

            var s = node.AsString();
            QNode prefix = null;
            switch (s)
            {
                case "id":
                    return q.Q(s, MetaType("id", interType: "s-literal"), q.Q(node.QValue().RawValue(), MetaType("s-literal", interType: "void")));
                case "делитель":
                case "кратный":
                case "кроме":
                case "n-значный":
                case "четный":
                case "нечетный":
                case ">":
                case "<":
                case "больше":
                case "меньше":
                    type = "set";
                    childType = "int";
                    interType = "int";
                    break;
                case "is?":
                    return q.Q(s, q.Q("meta", q.Q("type", "bool"), q.Q("inter-type", "int", q.Q("set", "int"))), node.Nodes().Select(child => Typified(child, functionIndex)));
                case "true":
                case "false":
                    type = "bool";
                    interType = "void";
                    break;
                case "частное"://TODO synonym div
                case "add":
                case "subtract":
                case "div":
                case "mul":
                    type = "number";
                    interType = "number";
                    break;
                case "mod":
                case "idiv":
                    type = "int";
                    interType = "int";
                    break;
                case "sum":
                case "count":
                case "min":
                case "max":
                    type = "int";
                    interType = "int-s";
                    break;
                case "число":
                    type = "number";
                    break;
                case "enumerate":
                    type = "int-s";
                    interType = "set";
                    break;
                case "take":
                    type = "int-s";
                    interTypes = new[] { "int-s", "int" };
                    break;
                default:
                    {
                        var functions = functionIndex.Find(s);
                        if (functions.OrEmpty().Any())
                        {
                            var function = functions[0];
                            type = function.P_("result", "type", "*").AsString();
                            childType = function.P_("result", "type", "*", "*").AsString();
                            interType = function.P_("arg", 0, "type", "*").AsString();
                        }
                        else if (!string.IsNullOrEmpty(s) && char.IsDigit(s[0]) && s.All(ch => char.IsDigit(ch) || ch == '-' || ch == '.' || ch == ','))
                        {
                            type = "s-literal";
                            interType = "void";
                            var numberType = s.Any(ch => ch == ',' || ch == '.') ? "number" : "int";
                            prefix = q.Q("to-" + numberType, MetaType(numberType, interType: "s-literal"));
                        }
                        else
                        {
                            var structureTypes = new[] { "point", "task", "expected", "skip" };
                            if (structureTypes.Contains(s))
                            {
                                type = s;
                                if (s == "point")
                                    interType = "structure";
                                if (s == "skip")
                                    interType = "s-literal";
                            }
                        }
                    }
                    break;
            }


            var v = node.RawValue();
            object n = MetaType(type, childType, interType: interType, interTypes: interTypes);

            var childs = node.Nodes().Select(child => Typified(child, functionIndex)).ToArray();
            var res = q.Q(v, n, childs);
            if (prefix != null)
                return prefix.W(res);
            return res;
        }
        public static IEnumerable<QNode> Typified(IEnumerable<QNode> nodes, Dictionary<string, QNode[]> functionIndex)
        {
            return nodes?.Select(node => Typified(node, functionIndex));
        }

        private static QNode ResolveFunction(QNode qs_function)
        {
            var cs_name = qs_function.P_("cs", "name", "*").AsString();
            var assemblyName = qs_function.P_("cs", "assembly", "*").AsString();
            QNode q_method = null;
            QNode error = null;

            if (cs_name != null && assemblyName != null)
            {
                try
                {
                    var index = cs_name.LastIndexOf('.');
                    if (index < 0)
                        throw new Exception($"Invalid name: '{cs_name}'");

                    var argCount = qs_function.P_s("arg", "*").Count();

                    //var fullName = $"{cs_name.Substring(0, index)}, {assemblyName}";
                    var fullName = cs_name.Substring(0, index);
                    var type = Type.GetType(fullName);
                    if (type == null)
                        throw new Exception($"Не найден тип '{fullName}'");
                    var methodName = cs_name.Substring(index + 1);
                    var methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                      .Where(_method => _method.Name == methodName)
                      .Where(_method => _method.GetParameters().Length == argCount)
                      .ToArray();
                    var method = methods.FirstOrDefault();
                    var argType = qs_function.P_("arg", 0, "cs-type", "*").AsString();
                    if (argType != null)
                    {
                        var argCsType = Type.GetType(argType);
                        method = methods.FirstOrDefault(_method => _method.GetParameters().FirstOrDefault()?.ParameterType == argCsType) ?? method;
                    }
                    if (method == null)
                        throw new Exception($"Не найден метод '{methodName}' в типе '{fullName}'");
                    q_method = q.Q("method", method);
                }
                catch (Exception exc)
                {
                    error = q.Q("error", exc.ToDisplayMessage());
                }
            }
            if (q_method != null)
                qs_function = qs_function.To_s().W_s(new object[] { "cs", q.Q("method", "+") }, q_method).FirstOrDefault();
            if (error != null)
                qs_function = qs_function.To_s().W_s(new object[] { "cs", q.Q("error", "+") }, error).FirstOrDefault();
            return qs_function;
        }

        public static IEnumerable<QNode> ResolveFunctions(IEnumerable<QNode> qs_functions)
        {
            return qs_functions
                    .Select(qs_function => ResolveFunction(qs_function));
        }


        private static readonly IQNodeBuilder q = null;

    }



}