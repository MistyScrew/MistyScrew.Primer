using MistyScrew.Functional;
using MistyScrew.Wui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Web;

namespace MistyScrew.QThinking
{
  public class DevConsole
  {
    public static IEnumerable<HElement> ParseExpressions()
    {
      var sets = new[]
      {
        QFunctions.FromNumber(10),
        QFunctions.Больше(10),
        QFunctions.Меньше(10),
        QFunctions.Делитель(10),
        QFunctions.Кратный(10),
        QFunctions.Кроме(10),
        QFunctions.N_Значный(10),
      };
      return sets.SelectMany(set => set.Conditions).SelectMany(fs => fs).Select(f => ParseExpression(f));
    }

    static HElement ParseExpression(Functor<int> functor)
    {
      try
      {
        
        var binaryExpression = functor.Expression.Body as BinaryExpression;
        if (binaryExpression != null)
        {
          var left = SetHlp.ReduceExpression(binaryExpression.Left);
          var right = SetHlp.ReduceExpression(binaryExpression.Right);
          var bound = functor.BoundRange();

          return h.Div
            (
              //h.Div("F: " + functor.Expression.Parameters.Count + ", " + functor.Expression),
              h.Div($"Expr: {ToDisplay(left)} {binaryExpression.NodeType} {ToDisplay(right)}"),
              h.Div("B: ", bound != null ? $"[{bound.Item1} - {bound.Item2})": null)
              //h.Div("M: " + binaryExpression.Method),
              //h.Div("N: " + binaryExpression.NodeType)
              //ParseAccessExpression(binaryExpression.Left),
              //ParseAccessExpression(binaryExpression.Right)
              //h.Div("RF: " + binaryExpression.Right)
            );
        }

        return h.Div(functor.Expression.Body.GetType().Name);
      }
      catch (Exception exc)
      {
        return h.Div(exc.ToDisplayMessage());
      }
    }
    static object ToDisplay(Expression expr)
    {
      if (expr is ParameterExpression)
        return ((ParameterExpression)expr).Name;
      if (expr is ConstantExpression)
        return ((ConstantExpression)expr).Value;
      return null;
    }



    static IEnumerable<HElement> ParseAccessExpression(Expression expr)
    {
      yield return h.Div("E: " + expr.GetType().Name);
      var constant = expr as ConstantExpression;
      if (constant != null)
        yield return h.Div("C: " + constant.Value);

      var paramExpression = expr as ParameterExpression;
      if (paramExpression != null)
      {
        yield return h.Div("P: " + paramExpression.Name);
      }

      var fieldExpression = expr as MemberExpression;
      if (fieldExpression != null)
      {
        yield return h.Div("Nt: " + fieldExpression.NodeType);
        yield return h.Div("Fd: " + fieldExpression.Member);
        foreach (var child in ParseAccessExpression(fieldExpression.Expression))
          yield return child;
      }

    }
    static readonly HBuilder h = null;
  }
}