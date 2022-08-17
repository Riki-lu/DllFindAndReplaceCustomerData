using Kusto.Language;
using Kusto.Language.Syntax;
using System.Collections;

namespace DllProject
{
    public class ClearQueryFromCustomerData
    {
        /// <summary>
        /// wrapper function-call the all functions in order to find and replace the Customer Data
        /// </summary>
        /// <param name="query">a Kusto query</param>
        /// <returns>if the query is valid, return a clean query-without Customer Data.
        /// else return list of the validate errors</returns>
        public static object ReplaceCustomerDataInQuery(string query)
        {
            var queryValidateErrors = ValidateQuery(query);
            if (queryValidateErrors == null)
            {
                var customerDataWordsAndReplacementWords = PassQueryFindCustomerData(query);
                return customerDataWordsAndReplacementWords.Count > 0 ? BuildCleanQueryReplaceCustomerData(query, customerDataWordsAndReplacementWords) : query;
            }
            return queryValidateErrors.Select(x => x.Message).ToList();
        }

        /// <summary>
        /// Validation checks to the query
        /// </summary>
        /// <param name="query">a Kusto query</param>
        /// <returns>true if the query is valid and false if not</returns>
        private static IReadOnlyList<Diagnostic>? ValidateQuery(string query)
        {
            //func GetDiagnostics find validate errors in KQL query 
            return query == String.Empty ? new List<Diagnostic>() { new Diagnostic("", "The query is empty") } : KustoCode.ParseAndAnalyze(query).GetDiagnostics();
        }

        /// <summary>
        /// pass the query, find the customer data.
        /// </summary>
        /// <param name="query">a Kusto query</param>
        /// <returns>hash table:key-all customer data had found value-Replacement word</returns>
        private static Hashtable PassQueryFindCustomerData(string query)
        {
            var index = 0;
            var customerDataWordsAndReplacementWords = new Hashtable();
            var parseQuery = KustoCode.Parse(query);
            SyntaxElement.WalkNodes(parseQuery.Syntax,
            n =>
            {
                switch (n.Kind)
                {
                    //Sensitive Operators-contain Customer Data
                    //each Node operator represents root of tree, the first Descendant is the Customer Data word.
                    case SyntaxKind.LetStatement:
                    case SyntaxKind.LookupOperator:
                    case SyntaxKind.AsOperator:
                    case SyntaxKind.PatternStatement:
                    case SyntaxKind.RangeOperator:
                    case SyntaxKind.NameAndTypeDeclaration:
                        if (customerDataWordsAndReplacementWords[n.GetFirstDescendant<NameDeclaration>().GetFirstToken().ValueText] == null)
                            customerDataWordsAndReplacementWords.Add(n.GetFirstDescendant<NameDeclaration>().GetFirstToken().ValueText, "CustomerData" + index++);
                        break;
                    case SyntaxKind.ProjectOperator:
                    case SyntaxKind.ProjectRenameOperator:
                    case SyntaxKind.SummarizeOperator:
                    case SyntaxKind.PrintOperator:
                    case SyntaxKind.ExtendOperator:
                    case SyntaxKind.ParseOperator:
                    case SyntaxKind.ParseWhereOperator:
                        var lstCustomerData = n.GetDescendants<NameDeclaration>().ToList();
                        foreach (var item in lstCustomerData)
                        {
                            if (customerDataWordsAndReplacementWords[item.GetFirstToken().ValueText] == null)
                                customerDataWordsAndReplacementWords.Add(item.GetFirstToken().ValueText, "CustomerData" + index++);
                        }
                        break;
                    //Sensitive Parmeters-themselvs Customer Data word.
                    case SyntaxKind.NamedParameter:
                        if (customerDataWordsAndReplacementWords[n.GetFirstToken().ValueText] == null)
                            customerDataWordsAndReplacementWords.Add(n.GetFirstToken().ValueText, "CustomerData" + index++);
                        break;
                    case SyntaxKind.StringLiteralExpression:
                        if (customerDataWordsAndReplacementWords[n.ToString()] == null)
                            customerDataWordsAndReplacementWords.Add(n.ToString(), "CustomerData" + index++);
                        break;
                }
            });
            return customerDataWordsAndReplacementWords;
        }

        /// <summary>
        /// Replace the customer data words 
        /// </summary>
        /// <param name="query">a Kusto query</param>
        /// <param name="customerDataWordsAndReplacementWords">list of all customer data had found in this query and the alternate words</param>
        /// <returns>new query without customer data</returns>
        public static string BuildCleanQueryReplaceCustomerData(string query, Hashtable customerDataWordsAndReplacementWords)
        {
            var parseQuery = KustoCode.Parse(query);
            var splitQuery = parseQuery.GetLexicalTokens().ToList();
            var cleanQuery = "";
            var customerDataWord = "";
            foreach (var word in splitQuery)
            {
                if (customerDataWordsAndReplacementWords[word.Text] != null)
                {
                    customerDataWord = customerDataWordsAndReplacementWords[word.Text].ToString();
                    cleanQuery += char.IsLetter(customerDataWord[0]) ? " " + customerDataWord : customerDataWord;
                }
                else
                    cleanQuery += word.Text != "" ? Char.IsLetter(word.Text[0]) ? " " + word.Text : word.Text : "";
            }
            return cleanQuery;
        }
    }
}
