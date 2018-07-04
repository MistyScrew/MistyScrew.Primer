using MistyScrew.Functional;
using MistyScrew.QSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Web;

namespace MistyScrew.QThinking
{
  public class QCalculator
  {
        public class Function
        {
            public Guid Id = Guid.NewGuid();
            public string Name;
            public ImmutableArray<string> Args;
            public Func<object[], object> F;
            public QNode RawDescription;
            public int Index;
        }

    public QCalculator(IEnumerable<QNode> qs_functions)
    {
      this.FunctionIndex = qs_functions
        .Select((function, i) => new Function
            {
              Name = function.P_("name", "*").AsString(),
              F = ToF(function.P_("cs", "method", "*").RawValue().As<System.Reflection.MethodInfo>()),
              Args = function.P_s("arg", "*").OrEmpty().Select(arg => arg.P_("type", "*").AsString()).ToImmutableArray(),
              RawDescription = function,
              Index = i,
            })
        .Where(pair => pair.F != null)
        .GroupBy(pair => pair.Name)
        .ToImmutableDictionary(pair => pair.Key, group => group.ToArray());
    }
    static Func<object[], object> ToF(System.Reflection.MethodInfo method)
    {
      if (method == null)
        return null;
      return args => method.Invoke(null, args);
    }
  
    public readonly ImmutableDictionary<string, Function[]> FunctionIndex;

    public QNode Identify(QNode typedNode, Func<QNode, QNode> normalize)
    {
      var s = typedNode.AsString();
      var interType = typedNode.C(0).P_("inter-type", "*").AsString();
      var f = FunctionIndex.Find(s).OrEmpty().FirstOrDefault(_f => _f.Args.FirstOrDefault() == interType);
      if (f != null)
        return q.Q(typedNode.RawValue(), typedNode.C(0).W(q.Q("function-id", f.Id)), typedNode.Nodes().Skip(1).Select(child=> normalize(child)));
      return typedNode;
    }

    public object Calculate(QNode typedNode, object prev = null)
    {
      if (typedNode == null)
        return null;

      var s = typedNode.AsString();
      var type = typedNode.C(0).P("type").QValue().AsString();
      switch (type)
      {
        case "s-literal":
          return s;
      }

      try
      {
        if (s == "::query")
        {
          var r = Calculate(typedNode.C(1));
          foreach (var child in typedNode.Nodes().Skip(2))
          {
            if (r is Exception)
              return r;
            r = Calculate(child, r);
          }
          return r;
        }

        var functions = new Dictionary<string, Func<object, object>>
        {
          //{"int",  o => ConvertHlp.ToInt(o)},
          //{"to-int",  o => ConvertHlp.ToInt(o)},
          //{"делитель",  o => Делитель(o as int?)},
          //{"кратный",  o => Кратный(o as int?)},
          //{"n-значный", o => N_Значный(o as int?) },
          //{"кроме", o => Кроме(o as int?) },
          {"true", o => true },
          {"false", o => false },
          {"enumerate", o => ((Set<int>)o).EnumerateNumbers() },
          {"число", o => o },
          {"sum", o => ((IEnumerable<int>)o).OrEmpty().Sum() },
        };

        var f = functions.Find(s);
        if (f != null)
        {
          var v = Calculate(typedNode.C(1));
          if (v is Exception)
            return v;
          return f(v);
        }

        var functions2 = new Dictionary<string, Func<object, object, object>>
        {
          //{"is?",  (o1, o2) => ((Set<int>)o2).Is((int)o1)},
          {"частное",  (o1, o2) => {var v2 = o2 as int?; if (v2 == 0)return new DivideByZeroException(); return o1.As<int?>() / v2; } },
          //{"делитель",  o => Делитель(o as int?)},
        };

        var f2 = functions2.Find(s);
        if (f2 != null)
        {
          var v = Calculate(typedNode.C(1));
          var v2 = Calculate(typedNode.C(2));
          if (v is Exception)
            return v;
          if (v2 is Exception)
            return v2;
          return f2(v, v2);
        }

        var functions_s = new Dictionary<string, Func<IEnumerable<object>, object>>
        {
          {"collection", objs => objs.OrEmpty().Select(obj => (int)obj) },
          {"and", sets => sets.Select(set => (Set<int>)set).And() }
        };
        var fs = functions_s.Find(s);
        if (fs != null)
        {
          var vs = typedNode.Nodes().Skip(1).Select(n => Calculate(n)).ToArray();
          var error = vs.OfType<Exception>().FirstOrDefault();
          if (error != null)
            return error;
          return fs(vs);
        }

        var f_objs = FunctionIndex.Find(s);
        if (f_objs != null)
        {
          var vs = typedNode.Nodes().Skip(1).Select(n => Calculate(n)).ToArray();
          var error = vs.OfType<Exception>().FirstOrDefault();
          if (error != null)
            return error;
          var fn = f_objs[0];
          if (f_objs.Length > 1)
          {
            var interType = typedNode.P_("meta", "inter-type", "*").AsString();
            fn = f_objs.FirstOrDefault(_f => _f.Args.FirstOrDefault() == interType) ?? f_objs[0];
          }

          return fn.F(prev != null ? Enumerable.Repeat(prev, 1).Concat(vs).ToArray(): vs);
        }


        switch (s)
        {
          case "point":
            {
              if (typedNode.P_("eq", 1).AsString() == "skip")
                return typedNode.P_("eq", 1);
              var eqNode = typedNode.P("eq").FirstOrDefault();
              var result = Calculate(eqNode);
              if (result is Exception)
                return result;
              return q.Q(result, Calculate(eqNode.C(1)), Calculate(eqNode.C(2)));

              //var taskNode = typedNode.P("task").FirstOrDefault().C(1);
              //var expectedNode = typedNode.P("expected").FirstOrDefault().C(1);
              //if (expectedNode.AsString() == "skip")
              //  return q.Q("skip", expectedNode, taskNode);
              //var task = Calculate(taskNode);
              //var expected = Calculate(expectedNode);
              //if (task is Exception)
              //  return task;
              //if (expected is Exception)
              //  return expected;
              //return q.Q(object.Equals(expected, task), expected, task);
              //return q.Q(typedNode.P("id").FirstOrDefault().C(1).RawValue(),
              //  q.Q("result", object.Equals(expected, task)),
              //  q.Q("expected", expected),
              //  q.Q("actual", task)
              //);
              //return new Exception(string.Format("{0} - {1,5}: {2}, {3}", typedNode.P("id").FirstOrDefault().C(1).AsString(), , expected, task));
            }
        }



        return new Exception($"{s}<{type}>");
      }
      catch (Exception exc)
      {
        return new Exception($"{s}<{type}>", exc);
      }
    }

    public static readonly IQNodeBuilder q = null;
  }


}