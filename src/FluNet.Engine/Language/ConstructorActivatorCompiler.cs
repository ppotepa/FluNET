using System.Linq.Expressions;
using System.Reflection;

namespace FluNET.Language;

internal static class ConstructorActivatorCompiler
{
    public static Func<object?[], object> Compile(ConstructorInfo constructor)
    {
        ParameterExpression arguments = Expression.Parameter(typeof(object[]), "arguments");
        ParameterInfo[] parameters = constructor.GetParameters();
        Expression[] converted = new Expression[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            BinaryExpression index = Expression.ArrayIndex(arguments, Expression.Constant(i));
            converted[i] = Expression.Convert(index, parameters[i].ParameterType);
        }

        NewExpression create = Expression.New(constructor, converted);
        UnaryExpression box = Expression.Convert(create, typeof(object));
        return Expression.Lambda<Func<object?[], object>>(box, arguments).Compile();
    }
}
