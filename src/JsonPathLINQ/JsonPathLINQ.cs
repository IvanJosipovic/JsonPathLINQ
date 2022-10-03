﻿using JsonPathExpressions;
using JsonPathExpressions.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
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
                        var param2 = Expression.Parameter(body.Type.GenericTypeArguments[0], "y");

                        var filter = ProcessFilterExpression(param2, ((JsonPathFilterExpressionElement)element).Expression);

                        var newFunc = typeof(Func<,>).MakeGenericType(body.Type.GenericTypeArguments[0], typeof(bool));

                        var filterFunc = Expression.Lambda(newFunc, filter, param2);

                        body = Expression.Call(typeof(Enumerable), nameof(Enumerable.FirstOrDefault), new[] { body.Type.GenericTypeArguments[0] }, body, filterFunc);
                        break;
                    default:
                        break;
                }
            }

            Expression conversion = Expression.Convert(body, typeof(object));
            return Expression.Lambda<Func<T, object>>(conversion, param);
        }

        static private Expression ProcessFilterExpression(ParameterExpression param, string jsonExpression)
        {
            //@.type=="Ready"
            //@	the current object/element
            //* wildcard.All objects / elements regardless their names.
            var isWildcard = jsonExpression[..1] == "*";

            var isCurrent = jsonExpression[..1] == "@";

            var left = jsonExpression[1..];
            var right = string.Empty;

            // ==	left is equal to right (note that 1 is not equal to '1').
            // !=	left is not equal to right.
            // <	left is less than right.
            // <=	left is less or equal to right.
            // >	left is greater than right.
            // >=	left is greater than or equal to right.

            var op = string.Empty;

            foreach (var opStr in new List<string>(){ "==", "!=", "<", "<=", ">", ">=" })
            {
                if (left.IndexOf(opStr) > -1)
                {
                    op = opStr;
                    right = left.Substring(left.IndexOf(opStr) + 2);
                    left = left.Substring(0, left.IndexOf(opStr))[1..];

                    return Expression.Equal(param.GetNestedProperty(left), Expression.Constant(right.Trim('"')));
                };
            }

            return null;
        }

        internal static MemberExpression GetNestedProperty(this Expression param, string property)
        {
            var propNames = property.Split('.');
            var propExpr = Expression.Property(param, propNames[0]);

            for (int i = 1; i < propNames.Length; i++)
            {
                propExpr = Expression.Property(propExpr, propNames[i]);
            }

            return propExpr;
        }
    }
}