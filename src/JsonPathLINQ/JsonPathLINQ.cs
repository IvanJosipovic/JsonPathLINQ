﻿using ExpressionTreeToString;
using JsonPathExpressions;
using JsonPathExpressions.Elements;
using System.Linq.Expressions;
using System.Xml.Linq;
using ZSpitz.Util;

namespace JsonPathLINQ
{
    public static class JsonPathLINQ
    {
        public static Expression<Func<T, object>> GetExpression<T>(string jsonPath, bool addNullChecks = false)
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

                        var filterFunc = Expression.Lambda(Expression.GetFuncType(new[] { body.Type.GenericTypeArguments[0], typeof(bool) }), filter, param2);

                        body = Expression.Call(typeof(Enumerable), nameof(Enumerable.FirstOrDefault), new[] { body.Type.GenericTypeArguments[0] }, body, filterFunc);
                        break;
                    default:
                        break;
                }
            }

            if (addNullChecks)
            {
                body = CreateNullChecks(body);
            }

            Expression conversion = Expression.Convert(body, typeof(object));

            return Expression.Lambda<Func<T, object>>(conversion, param);
        }

        public static object GetDefaultValue(Type type)
        {
            if (type == typeof(string))
            {
                return string.Empty;
            }
            else
            {
                return Activator.CreateInstance(type);
            }
        }

        public static Type GetFinalType<T>(JsonPathExpression jsonPathExpression)
        {
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
                        body = Expression.Call(typeof(Enumerable), nameof(Enumerable.FirstOrDefault), new[] { body.Type.GenericTypeArguments[0] }, body);
                        break;
                    default:
                        break;
                }
            }

            return body.Type;
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

        /// <summary>
        /// Recursively walks up the tree and adds null checks
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="skipFinalMember"></param>
        /// <returns></returns>
        public static Expression CreateNullChecks(this Expression expression, bool skipFinalMember = false)
        {
            var parents = new Queue<Expression>();

            Expression? newExpression = null;

            if (expression is UnaryExpression unary)
            {
                expression = unary.Operand;
            }

            if (expression is LambdaExpression lambda)
            {
                expression = lambda.Body;
            }

            var count = GetDepth(expression);

            if (count == 1) return expression;

            MemberExpression temp = (MemberExpression)expression;

            while (temp is MemberExpression member)
            {
                try
                {
                    parents.Enqueue(member);
                }
                catch (InvalidOperationException) { }

                temp = member.Expression as MemberExpression;
            }

            var finalType = parents.First().Type;


            Expression? lastExpression;

            while (true)
            {
                if (parents.Count == 0)
                {
                    break;
                }

                lastExpression = parents.Dequeue();

                if (lastExpression.Type.IsValueType)
                {
                    newExpression = lastExpression;
                    continue;
                }

                if (newExpression == null)
                {
                    newExpression = ElvisOperator(lastExpression, lastExpression, finalType);
                }
                else
                {
                    newExpression = ElvisOperator(lastExpression, newExpression, finalType);
                }
            }

            return newExpression;
        }

        private static int GetDepth(Expression expression)
        {
            int count = 0;

            while (expression is MemberExpression member)
            {
                count++;
                expression = member.Expression;
            }

            return count;
        }

        public static Expression ElvisOperator(Expression expression, Expression propertyOrField, Type finalType)
        {
            return Expression.Condition(
                    Expression.Equal(expression, Expression.Constant(null, expression.Type)),
                    Expression.Constant(GetDefaultValue(finalType), finalType),
                    propertyOrField,
                    finalType
                   );
        }
    }
}
