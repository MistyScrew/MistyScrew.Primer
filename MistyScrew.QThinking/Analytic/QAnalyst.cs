using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MistyScrew.Functional;
using System.Text;
using MistyScrew.Wui;

namespace MistyScrew.QThinking
{
    public class QAnalyst
    {
        public static HElement Execute()
        {
            var stringType = typeof(string);
            var methods = stringType.GetMethods();
            foreach (var method in methods)
            {
                Console.WriteLine(method.Name);
            }
            var groups = methods.Select(method =>
               {
                   var parameters = Parameters(method);
                   var thisParameter = parameters.OfType<ThisParameter>().FirstOrDefault();
                   var returnParameter = parameters.OfType<ReturnParameter>().FirstOrDefault();
                   return new
                   {
                       method,
                       name = $"{(thisParameter != null ? "(" + thisParameter.Type.Name + ").":null)}{method.Name}({parameters.Where(p => !(p is ThisParameter ) && !(p is ReturnParameter)).Select(p => p.Type.Name).JoinToString(",") }) => {returnParameter?.Type.Name}",
                       parameters = parameters
                           .Where(parameter => !(parameter is ReturnParameter))
                           .Select(parameter => parameter.Type.Name)
                           .JoinToString(",") 
                        + " => " 
                        + parameters
                            .Where(parameter => (parameter is ReturnParameter))
                           .Select(parameter => parameter.Type.Name)
                           .JoinToString(",")
                   };
               })
                .GroupBy(pair => pair.parameters, pair => pair);
            var lines = new List<HElement>();
            foreach (var group in groups.OrderByDescending(group => group.Count()))
            {
                lines.Add(h.Div(group.Key));
                foreach (var pair in group)
                {
                   lines.Add(h.Div(h.style("padding-left:20px;"), h.Span(pair.method.Name + ":"), h.Span(h.style("color:darkgray;"), pair.name)));
                }
            }
            return h.Div(lines);
        }
        readonly static HBuilder h = null;
        static IParameter[] Parameters(System.Reflection.MethodInfo method)
        {
            var types = method.GetParameters().Select(param => (IParameter)new Parameter(param))
                .Concat(new[] { new ReturnParameter(method.ReturnType) });

            if (method.IsStatic)
                return types.ToArray();
            return
                new[] { new ThisParameter(method.ReflectedType) }.Concat(types).ToArray();
        }
    }
    interface IParameter
    {
        Type Type { get; }
    }
    class Parameter:IParameter
    {
        public Parameter(System.Reflection.ParameterInfo parameter)
        {
            this.ParameterInfo = parameter;
        }
        public readonly System.Reflection.ParameterInfo ParameterInfo;

        public Type Type
        {
            get
            {
                return ParameterInfo.ParameterType;
            }
        }
    }
    class ThisParameter:IParameter
    {
        public ThisParameter(Type type)
        {
            this.Type = type;
        }
        public Type Type { get; private set; }
    }

    class ReturnParameter: IParameter
    {
        public ReturnParameter(Type type)
        {
            this.Type = type;
        }
        public Type Type { get; private set; }
    }
    class ExternalParameter: IParameter
    {
        public ExternalParameter(Type type, string fullPath)
        {
            this.Type = type;
            this.FullPath = fullPath;
        }
        public Type Type { get; private set; }
        public readonly string FullPath;
    }

    //class Argument
    //{
    //    public Argument(Type type, bool isIn, bool isOut, bool isThis, string external)
    //    public readonly 
    //}
    //class DelaringArgument
    //{
    //    public Type Type;
    //    public bool IsThis;
    //}

    //Аргумент
    // this - "x".ToLower()
    // in - Math.Sin(x)
    // out - 
}