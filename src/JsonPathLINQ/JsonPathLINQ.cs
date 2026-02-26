using ClientGo.JsonPath;
using JsonPathExpressions;
using JsonPathExpressions.Elements;
using System.Linq.Expressions;

namespace JsonPathLINQ
{
    public static class JsonPathLINQ
    {
        public static Expression<Func<T, object>> GetExpression<T>(string jsonPath, bool addNullChecks = false)
        {
            return GetExpression<T, object>(jsonPath, addNullChecks);
        }

        public static Expression<Func<T, T2>> GetExpression<T,T2>(string jsonPath, bool addNullChecks = false)
        {
            if (jsonPath[0] != '{')
            {
                jsonPath = '{' + jsonPath;
            }
            if (jsonPath[^1] != '}')
            {
                jsonPath += '}';
            }

            var jp = Parser.Parse("query", jsonPath);

            Expression body = null;

            foreach (var node in jp.Root.Nodes)
            {
                switch (node.Type)
                {
                    case NodeType.Text:
                        break;
                    case NodeType.Array:
                        break;
                    case NodeType.List:
                        break;
                    case NodeType.Field:
                        break;
                    case NodeType.Identifier:
                        break;
                    case NodeType.Filter:
                        break;
                    case NodeType.Int:
                        break;
                    case NodeType.Float:
                        break;
                    case NodeType.Wildcard:
                        break;
                    case NodeType.Recursive:
                        break;
                    case NodeType.Union:
                        break;
                    case NodeType.Bool:
                        break;
                    default:
                        break;
                }
            }

            if (addNullChecks)
            {
                body = CreateNullChecks(body);
            }

            Expression conversion = Expression.Convert(body, typeof(T2));

            return Expression.Lambda<Func<T, T2>>(conversion, Expression.Parameter(typeof(T), "x"));
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

        static private Expression ProcessFilterExpression(ParameterExpression param, string jsonExpression)
        {
            //@.type=="Ready"
            //@.type=='Ready'
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
                    right = left.Substring(left.IndexOf(opStr) + 2).Replace('\'', '\"');
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
        public static Expression CreateNullChecks(this Expression expression)
        {
            var parents = new Queue<Expression>();

            Expression? newExpression = null;

            Expression temp = expression;

            while (temp is MemberExpression || temp is UnaryExpression || temp is MethodCallExpression)
            {
                parents.Enqueue(temp);

                if (temp is MemberExpression tempMember)
                {
                    temp = tempMember.Expression;
                }
                else if(temp is UnaryExpression tempUnary)
                {
                    temp = tempUnary.Operand;
                }
                else if (temp is MethodCallExpression tempMethod)
                {
                    temp = tempMethod.Arguments.First();
                }
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

        public static Expression ElvisOperator(Expression expression, Expression propertyOrField, Type finalType)
        {
            return Expression.Condition(
                    Expression.Equal(expression, Expression.Constant(null, expression.Type)),
                    Expression.Constant(GetDefaultValue(finalType), finalType),
                    propertyOrField,
                    finalType
                   );
        }

        static private Expression PropertyOrFieldOrDictionaryKey<T>(Expression expression, string name)
        {
            // Check if the expression is a dictionary
            if (expression.Type.IsGenericType &&
                (expression.Type.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
                 expression.Type.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
            {
                var keyType = expression.Type.GetGenericArguments()[0];
                var valueType = expression.Type.GetGenericArguments()[1];

                // Create a constant expression for the key
                var key = Expression.Constant(name, keyType);

                // Get the "get_Item" method of the dictionary
                var getItemMethod = expression.Type.GetMethod("get_Item");

                // Call the "get_Item" method with the key constant
                var call = Expression.Call(expression, getItemMethod, key);

                // Convert the result to the expected type
                var convert = Expression.Convert(call, typeof(T));

                return convert;
            }
            else
            {
                // Use the PropertyOrField function
                return Expression.PropertyOrField(expression, name);
            }
        }
    }
}
