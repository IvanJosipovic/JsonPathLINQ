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
        public static Expression<Func<T, T2>> GetExpression<T, T2>(string jsonPath, bool addNullChecks = false)
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

            if (jp.Root.Nodes.Count == 0 || jp.Root.Nodes.Count > 1)
            {
                throw new Exception("Not supported");
            }

            Expression body = Generate(jp.Root.Nodes[0]);;

            Expression conversion = Expression.Convert(body, typeof(T2));

            return Expression.Lambda<Func<T, T2>>(conversion, Expression.Parameter(typeof(T), "x"));
        }

        public static Expression Generate(INode node)
        {
            switch (node.Type)
                {
                    case NodeType.Text:
                        var textNode = node as TextNode;

                        break;
                    case NodeType.Array:
                        var arrayNode = node as ArrayNode;
                        break;
                    case NodeType.List:
                        var listNode = node as ListNode;
                        break;
                    case NodeType.Field:
                        var fieldNode = node as FieldNode;

                        break;
                    case NodeType.Identifier:
                        var identifierNode = node as IdentifierNode;

                        break;
                    case NodeType.Filter:
                        var filterNode = node as FilterNode;

                        break;
                    case NodeType.Int:
                        var ln = node as ListNode;

                        break;
                    case NodeType.Float:
                        var floatNode = node as FloatNode;

                        break;
                    case NodeType.Wildcard:
                        var wildcardNode = node as WildcardNode;

                        break;
                    case NodeType.Recursive:
                        var recursiveNode = node as RecursiveNode;

                        break;
                    case NodeType.Union:
                        var unionNode = node as UnionNode;

                        break;
                    case NodeType.Bool:
                        var boolNode = node as BoolNode;

                        break;
                    default:
                        throw new Exception("Node Type not supported");
                }
        }
    }
}
