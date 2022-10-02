using JsonPathExpressions;
using JsonPathExpressions.Elements;
using System;
using System.Linq.Expressions;

namespace JsonPathLINQ
{
    public static class JsonPathLINQ
    {
        public static Expression<Func<T, object>> GetExpression<T>(string jsonPath)
        {
            var jsonPathExpression = new JsonPathExpression(jsonPath).GetNormalized();

            var param = Expression.Parameter(typeof(T), "x");
            Expression body = param;

            foreach (var element in jsonPathExpression.Elements)
            {
                switch (element.Type)
                {
                    case JsonPathElementType.Root:
                        break;
                    case JsonPathElementType.RecursiveDescent:
                        break;
                    case JsonPathElementType.Property:
                        body = Expression.PropertyOrField(body, ((JsonPathPropertyElement)element).Name);
                        break;
                    case JsonPathElementType.AnyProperty:
                        break;
                    case JsonPathElementType.PropertyList:
                        break;
                    case JsonPathElementType.ArrayIndex:
                        break;
                    case JsonPathElementType.AnyArrayIndex:
                        break;
                    case JsonPathElementType.ArrayIndexList:
                        break;
                    case JsonPathElementType.ArraySlice:
                        break;
                    case JsonPathElementType.Expression:
                        break;
                    case JsonPathElementType.FilterExpression:
                        break;
                    default:
                        break;
                }
            }

            return Expression.Lambda<Func<T, object>>(body, param);
        }
    }
}
