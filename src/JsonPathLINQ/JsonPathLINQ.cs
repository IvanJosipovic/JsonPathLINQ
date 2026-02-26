using ClientGo.JsonPath;
using System.Linq.Expressions;

namespace JsonPathLINQ
{
    public static class JsonPathLINQ
    {
        /// <summary>
        /// Returns a Expression representing the jsonPath
        /// </summary>
        /// <typeparam name="T">source object</typeparam>
        /// <param name="jsonPath">jsonPath</param>
        /// <param name="addNullChecks">add null checks</param>
        /// <returns></returns>
        public static Expression<Func<T, object>> GetExpression<T>(string jsonPath, bool addNullChecks = false)
        {
            return GetExpression<T, object>(jsonPath, addNullChecks);
        }

        /// <summary>
        /// Returns a Expression representing the jsonPath
        /// </summary>
        /// <typeparam name="T">source object</typeparam>
        /// <typeparam name="T2">return type</typeparam>
        /// <param name="jsonPath">jsonPath</param>
        /// <param name="addNullChecks">add null checks</param>
        /// <returns></returns>
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

            Expression conversion = Expression.Convert(body, typeof(T2));

            return Expression.Lambda<Func<T, T2>>(conversion, Expression.Parameter(typeof(T), "x"));
        }
    }
}
