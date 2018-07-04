using MistyScrew.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Web;

namespace MistyScrew.QThinking
{
  public class Functor<T>
  {
    public Functor(System.Linq.Expressions.Expression<Func<T, bool>> expression)
    {
      this.Expression = expression;
      this.F = expression.Compile();
    }
    public readonly System.Linq.Expressions.Expression<Func<T, bool>> Expression;
    public readonly Func<T, bool> F;
  }

  public class Set<T>
  {
    public Set(Functor<T> condition) : this(new[] { new[] { condition } })
    {
    }

    public Set(Functor<T>[][] conditions)
    {
      this.Conditions = conditions.OrEmpty();
    }

    public readonly Functor<T>[][] Conditions;

    public bool Is(T value)
    {
      return Conditions.Any(or => or.All(and => and.F(value)));
    }
    public IEnumerable<T> Enumerate(IEnumerable<T> items)
    {
      return items.Where(item => Is(item));
    }
  }
  public static class SetHlp
  {
    public static Tuple<int?, int?> BoundRange(this Functor<int> functor)
    {
      var binaryExpression = functor?.Expression?.Body as BinaryExpression;
      if (binaryExpression == null)
        return null;
      var left = ReduceExpression(binaryExpression.Left);
      var right = ReduceExpression(binaryExpression.Right);
      if (left == null || right == null)
        return null;
      if (!((left is ParameterExpression || left is ConstantExpression) && (right is ParameterExpression || right is ConstantExpression)))
        return null;
      if (binaryExpression.NodeType == ExpressionType.Equal)
      {
        var v = ToInt(left.As<ConstantExpression>() ?? right.As<ConstantExpression>());
        return Tuple.Create(v, v + 1);
      }
      if (binaryExpression.NodeType == ExpressionType.LessThan)
      {
        var l = ToInt(left.As<ConstantExpression>());
        var r = ToInt(right.As<ConstantExpression>());
        if (l != null)
        {
          return Tuple.Create(l + 1, (int?)null);
        }
        if (r != null)
        {
          return Tuple.Create((int?)null, r);
        }
      }
      if (binaryExpression.NodeType == ExpressionType.LessThanOrEqual)
      {
        var l = ToInt(left.As<ConstantExpression>());
        var r = ToInt(right.As<ConstantExpression>());
        if (l != null)
        {
          return Tuple.Create(l, (int?)null);
        }
        if (r != null)
        {
          return Tuple.Create((int?)null, r + 1);
        }
      }
      if (binaryExpression.NodeType == ExpressionType.GreaterThan)
      {
        var l = ToInt(left.As<ConstantExpression>());
        var r = ToInt(right.As<ConstantExpression>());
        if (l != null)
        {
          return Tuple.Create((int?)null, l);
        }
        if (r != null)
        {
          return Tuple.Create(r + 1, (int?)null);
        }
      }
      if (binaryExpression.NodeType == ExpressionType.GreaterThanOrEqual)
      {
        var l = ToInt(left.As<ConstantExpression>());
        var r = ToInt(right.As<ConstantExpression>());
        if (l != null)
        {
          return Tuple.Create((int?)null, l + 1);
        }
        if (r != null)
        {
          return Tuple.Create(r, (int?)null);
        }
      }
      return null;
    }
    static int? ToInt(ConstantExpression constExpression)
    {
      if (constExpression == null || constExpression.Type != typeof(int) && constExpression.Type != typeof(int?))
        return null;
      return (int?)constExpression.Value;
    }
    public static Expression ReduceExpression(Expression expr)
    {
      if (expr is ParameterExpression)
        return expr;
      if (expr is ConstantExpression)
        return expr;
      if (expr is MemberExpression)
      {
        var memberExpression = (MemberExpression)expr;
        var constExpr = memberExpression.Expression as ConstantExpression;
        var field = memberExpression.Member as System.Reflection.FieldInfo;
        if (constExpr != null && field != null)
        {
          return Expression.Constant(field.GetValue(constExpr.Value));
        }
      }
      return expr;
    }

    public static bool Is(this Set<int> set, int? value)
    {
      if (value == null || set == null)
        return false;
      return set.Is(value.Value);
    }
    public static IEnumerable<int> EnumerateNumbers(this Set<int> set, int? min = null, int? max = null)
    {
      if (set == null)
        return null;
      min = min ?? set.Conditions.Min(conds => conds.Max(f => f.BoundRange()?.Item1)) ??  -100000;
      max = max ?? set.Conditions.Max(conds => conds.Min(f => f.BoundRange()?.Item2)) ?? 100000;
      return set.Enumerate(Enumerable.Range(min.Value, max.Value - min.Value));
    }
    public static Set<T> And<T>(this Set<T> set1, Set<T> set2)
    {
      if (set1 == null || set2 == null)
        return null;
      var conditions = new List<Functor<T>[]>();
      foreach (var c1 in set1.Conditions)
        foreach (var c2 in set2.Conditions)
        {
          conditions.Add(c1.Concat(c2).ToArray());
        }

      return new Set<T>(conditions.ToArray());
    }
    public static Set<T> Or<T>(this Set<T> set1, Set<T> set2)
    {
      if (set1 == null || set2 == null)
        return null;
      return new Set<T>(set1.Conditions.Concat(set2.Conditions).ToArray());
    }
    public static Set<T> Or<T>(this IEnumerable<Set<T>> sets)
    {
      var r = sets.FirstOrDefault();
      foreach (var s in sets.Skip(1))
        r = r.Or(s);
      return r;
    }
    public static Set<T> And<T>(this IEnumerable<Set<T>> sets)
    {
      var r = sets.FirstOrDefault();
      foreach (var s in sets.Skip(1))
        r = r.And(s);
      return r;
    }

  }
}