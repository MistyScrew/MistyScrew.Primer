using MistyScrew.Functional;
using MistyScrew.QSharp;
using MistyScrew.Wui;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;

namespace MistyScrew.QThinking
{
    public class DashboardController:ApiController
    {
        [HttpGet, HttpPost]
        [Route("")]
        public HttpResponseMessage Route() { return HWebApiSynchronizeHandler.Process<ConsoleState>(this.Request, HView); }

        public static HtmlResult<HElement> HView(ConsoleState state, JsonData[] jsons, HttpRequestMessage context)
        {

            foreach (var json in jsons.OrEmpty())
            {

                switch (json.JPath("data", "command")?.ToString())
                {
                    default:
                        break;
                }
            }
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            var qs_functions_doc = QParser.Parse(System.IO.File.ReadAllText(HostingEnvironment.MapPath("~/") + "/Data/functions.qs"));
            var qs_functions = QTransformator.ResolveFunctions(qs_functions_doc.P_s("function"));
            var qs_function_index = qs_functions.GroupBy(function => function.P_("name", "*").AsString())
                .ToDictionary(group => group.Key, group => group.ToArray());


            var calculator = new QCalculator(qs_functions);

            var qs_text = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~/") + "/Data/textbook.math.6.qs");
            var qs = QParser.Parse(qs_text);


            var prepareDuration = watch.Elapsed;

            watch.Reset();
            watch.Start();
            var typed_qs = QTransformator.ProcessAll(qs.P_s("point", "*"), qs_function_index);
            typed_qs = QTransformator.Transform(typed_qs, calculator.Identify).ToArray();
            var transformDuration = watch.Elapsed;

            watch.Reset();
            watch.Start();
            var results = typed_qs.Select(typedNode => Tuple.Create(typedNode, calculator.Calculate(typedNode)))
              .ToArray();
            var calculateDuration = watch.Elapsed;

            watch.Reset();
            watch.Start();
            var notifies = QTransformator.Browse(typed_qs, QTransformator.Error).ToArray();
            var types = QTransformator.Browse(typed_qs, q => q.C(0).P("type").OrEmpty().FirstOrDefault().C(0)).ToArray();
            var usedFunctions = QTransformator.Browse(typed_qs, q => QTransformator.IsAnyType(q.C(0).P("type").OrEmpty().FirstOrDefault().C(0)) ? null : q)
              .Where(pair => pair.Item2 != null).ToArray();
            watch.Stop();

            var version = ConvertHlp.ToInt(qs.P_("version", "*").RawValue());
            var resultVersionFilename = HostingEnvironment.MapPath("~/App_Data") + $"/textbook.math.6.({version}).qs";
            if (!System.IO.File.Exists(resultVersionFilename))
                System.IO.File.WriteAllText(resultVersionFilename, typed_qs.ToText());

            var elements = new[]
            {
                h.Div($"Подготовка: {prepareDuration}"),
                h.Div($"Преобразование: {transformDuration}"),
                h.Div($"Вычисление: {calculateDuration}"),
                h.Div($"Notifies: {watch.Elapsed}"),
            };


            return new HtmlResult<HElement>
            {
                Html = Page(state, typed_qs, notifies, types, usedFunctions, calculator.FunctionIndex.Values.SelectMany(_ => _), results, elements, context),
                State = state,
                RefreshPeriod = TimeSpan.FromSeconds(2),
            };
        }

        private static bool Result_IsTrue(Tuple<QNode, object> pair)
        {
            return pair.Item2.As<QNode>().AsString()?.ToString().ToLower() == "true";
        }
        private static bool Result_IsSkip(Tuple<QNode, object> pair)
        {
            return pair.Item2.As<QNode>().AsString()?.ToString().ToLower() == "skip";
        }

        public static HElement Page(ConsoleState state, IEnumerable<QNode> typed_qs, Tuple<string, string>[] notifies, IEnumerable<Tuple<string, QNode>> typeNodes,
          IEnumerable<Tuple<string, QNode>> usedFunctionNodes, IEnumerable<QCalculator.Function> functions, IEnumerable<Tuple<QNode, object>> results, object output, HttpRequestMessage hcontext)
        {
            var types = typeNodes
              .GroupBy(type => type.Item2.AsString())
              .OrderByDescending(group => group.Count())
              .ToArray();

            var usedFunctions = usedFunctionNodes
              .GroupBy(function => function.Item2.AsString())
              .OrderByDescending(group => group.Count())
              .ToArray();
            var argFunctions = usedFunctionNodes
              .GroupBy(function => new { name = function.Item2.AsString(), interType = function.Item2.C(0).P("inter-type").OrEmpty().FirstOrDefault().C(0).AsString() })
              .OrderByDescending(group => group.Count())
              .ToArray();

            var errorResults = results.Where(res => !(Result_IsTrue(res) || Result_IsSkip(res))).ToArray();

            var functionIndex = functions.ToImmutableDictionary(f => f.Id);

            return h.Html
              (
                h.Head
                (
                  h.Element("title", "Thinking Dashboard")
                ),
                h.Body
                (
                  h.Css(style),
                  //h.TextArea(h.style("width:100%;height:400px;")),
                  h.Div
                  (
                    h.style("width:100%;"),
                    Widget("Результаты",
                      h.Div
                      (
                        h.Span(h.style("color:red"), errorResults.Length),
                        h.Span("/"),
                        h.Span($"{results.Count(res => Result_IsSkip(res))}"),
                        h.Span("/"),
                        h.Span(h.style("color:green"), $"{results.Count(res => Result_IsTrue(res))}"),
                        h.Span("/"),
                        h.Span($"{results.Count()}")
                      ),
                      errorResults.Select(result =>
                        h.Div
                        (
                          h.A(h.href($"#{result.Item1.P_("id", 1).AsString()}"), result.Item1.P_("id", 1).AsString()),
                          h.Span($", {(result.Item2 is System.Exception ? "Error: " + ((System.Exception)result.Item2).ToDisplayMessage() : result.Item2)}")
                        )
                      )
                    ),
                    Widget("Группы ошибок",
                      notifies.GroupBy(notify => notify.Item2)
                        .OrderByDescending(group => group.Count())
                        .Select(group => h.Div(h.Span(h.@class("count"), h.A(h.href("#" + group.FirstOrDefault().Item1), group.Count())), h.Span($" - {group.Key}")))
                    ),
                    Widget
                    (
                      "Ошибки",
                      h.Div($"Кол-во: {notifies.Length}"),
                      notifies.Take(20)
                       .Select(notify => h.Div(h.A(h.href($"#{notify.Item1}"), notify.Item1), h.Span($": {notify.Item2}")))
                    ),
                    Widget
                    (
                      "Типы",
                      h.Div($"Кол-во: {types.Length}"),
                      types.Take(100)
                        .Select(type => h.Div(h.Span(h.@class("count"), h.A(h.href("#" + type.FirstOrDefault().Item1), type.Count())), h.Span($" - {type.Key}")))
                    ),
                    Widget
                    (
                      "Функции",
                      h.Div($"Кол-во: {functions.Count()}"),
                      functions.Take(100)
                        .OrderBy(f => f.Index)
                        .Select(function =>
                        {
                            var fdesc = function.RawDescription;

                            var q_error = fdesc.P_("cs", "error", "*");
                            return h.Div
                            (
                                h.Attribute("id", "f-" + function.Id),
                                h.Span(
                                    q_error != null ? h.title(q_error.ToText()) : h.title(fdesc.P_("cs", "method", "*").RawValue()),
                                    h.style(fdesc.P_("cs", "method", "*") != null ? "color:#00C000" : q_error != null ? "color:red" : null),
                                    "● "
                                ),
                                h.Span(h.style("font-size:75%;width:15px;display:inline-block;text-align:right;padding:0px 3px;"), function.Index),
                                h.Span(FunctionFullName(fdesc))
                            );
                        })
                    ),
                    Widget
                    (
                      "Используемые функции",
                      h.Div($"Кол-во: {usedFunctions.Length}"),
                      usedFunctions.Take(100)
                        .SelectMany(function =>
                          new[]
                          {
                      h.Div(h.Span(h.@class("count"), h.A(h.href("#" + function.FirstOrDefault().Item1), function.Count())), h.Span($" - {function.Key}")),
                          }
                          .Concat
                          (
                            function.GroupBy(function1 => function1.Item2.C(0).P("inter-type").OrEmpty().FirstOrDefault().C(0).AsString())
                              .OrderByDescending(group => group.Count())
                              .Select(group => h.Div(h.Span(h.@class("count"), h.A(h.href("#" + group.FirstOrDefault().Item1), group.Count())), h.Span($" -\xA0\xA0 {function.Key}<{group.Key}>")))
                          )
                        )
                    ),
                    Widget
                    (
                      "Используемые функции<?>",
                      h.Div($"Кол-во: {argFunctions.Length}"),
                      argFunctions.Take(100)
                        .Select(function => h.Div(h.Span(h.@class("count"), h.A(h.href("#" + function.FirstOrDefault().Item1), function.Count())), h.Span($" - {function.Key.name}<{function.Key.interType}>")))
                    ),
                    Widget
                    (
                      "Вывод",
                      output,
                      DevConsole.ParseExpressions()
                    ),
                    h.Div(h.style("clear:both"))
                  ),
                  h.Div
                  (
                    h.style("height:500px;width:400px;overflow-y:auto;"),
                    typed_qs.OrEmpty().Select(n => h.Div(h.Attribute("id", n.P("id").QValue().Skip(1).FirstOrDefault().AsString()), View(n, functionIndex)))
                  ),
                  //h.Div
                  //(
                  //    HWebSynchronizeHandler.Updates(hcontext.HttpContext).Reverse()
                  //      .Take(10)
                  //      .Select(update => h.Div($"{update.Cycle}: {update.Elapsed}"))
                  //),
                  h.Div(h.style("color:darkgray;font-size:80%;"), DateTime.Now)
                )
              );
        }
        static string FunctionFullName(QNode fdesc)
        {
            var args = fdesc.P_s("arg", "*").Select(arg => arg.P_("type", "*").AsString()).JoinToString(",");
            return $"{fdesc.P_("name", "*").AsString()}({args}) -> {fdesc.P_("result", "type", "*").AsString()}";
        }
        static HElement View(QNode node, IDictionary<Guid, QCalculator.Function> functionIndex)
        {
            var functionId = ConvertHlp.ToGuid(node.C(0).P_("function-id", "*").AsString());
            var f = functionId.Maybe(_ => functionIndex.Find(functionId.Value));
            return h.Div
              (
                h.@class("q"),
                h.Span(h.@class("q-type"), $" ({TypeToString(node.C(0).P("type").QValue().FirstOrDefault())}) "),
                h.Span(node.RawValue()),
                h.Span(h.@class("q-inter-type"), $" ({node.P_s(0, "inter-type", "*").Select(interType => TypeToString(interType)).JoinToString(", ")}) "),
                functionId.Maybe(_ => h.A(h.style("font-size:75%"), h.href($"#f-{_}"), "f-" + f?.Index, h.title(FunctionFullName(f.RawDescription)))),
                node.Nodes().OrEmpty().Skip(1).Select(n => View(n, functionIndex))
              );
        }
        static HElement Widget(object title, params object[] content)
        {
            return h.Div
              (
                h.@class("widget"),
                h.Div(h.@class("widget-header"), title),

                h.Div(h.@class("widget-content"), content)
              );
        }
        static string TypeToString(QNode type)
        {
            var childType = type.QValue().AsString();
            if (childType == null)
                return type.AsString();
            return $"{type.AsString()}<{childType}>";
        }


        const string style = @"
          .q {padding-left:10px;}
          .q-type {color:darkgray;display:inline-block;width:100px;padding-right:5px;}
          .q-inter-type {color:darkgray;display:inline-block;width:100px;padding-left:15px;}
          .widget 
          {
            border:1px solid darkgray;
            width:300px;
            float:left;
            margin:5px;                
          }
          .widget-header
          {
            padding-left:10px;
            background-color:lightgray;
          }
          .widget-content
          {
            height:400px;
            overflow-x:hidden;
            overflow-y:auto;
            padding-left:5px;
          }
          .count
          {
              display:inline-block;
              width:20px;
              text-align:right;
          }
        ";

        static readonly HBuilder h = null;
        public static readonly IQNodeBuilder q = null;
    }

    public partial class ConsoleState
    {
        [Meta("null")]
        public readonly string Text;
    }

    public class MetaAttribute : Attribute
    {
        public MetaAttribute(string text) { }
    }


}