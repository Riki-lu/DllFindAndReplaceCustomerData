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
                //maybe to call it-customerDataWordsAndReplacementWords(Replacement)
                var customerDataWordsAndAlternateWords = PassQueryFindCustomerData(query);
                return customerDataWordsAndAlternateWords.Count > 0 ? ReplaceCustomerData(query, customerDataWordsAndAlternateWords) : query;
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
        /// maybe Replacement word/altenate
        /// <returns>hash table:key-all customer data had found value-Replacement word/altenate</returns>
        private static Hashtable PassQueryFindCustomerData(string query)
        {
            var index = 0;
            var customerDataWordsAndAlternateWords = new Hashtable();
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
                    case SyntaxKind.FunctionParameters:
                    case SyntaxKind.RangeOperator:
                        //would you rather put it in a new variable? {[n.GetFirstDescendant<NameDeclaration>().GetFirstToken().ValueText}
                        if (customerDataWordsAndAlternateWords[n.GetFirstDescendant<NameDeclaration>().GetFirstToken().ValueText] == null)
                        customerDataWordsAndAlternateWords.Add(n.GetFirstDescendant<NameDeclaration>().GetFirstToken().ValueText, "CustomerData" + index++);
                        break;
                    case SyntaxKind.ProjectOperator:
                    case SyntaxKind.ProjectRenameOperator:
                    case SyntaxKind.SummarizeOperator:
                    case SyntaxKind.PrintOperator:
                    case SyntaxKind.ExtendOperator:
                        var lstCustomerData = n.GetDescendants<NameDeclaration>().ToList();
                        foreach (var item in lstCustomerData)
                        {
                            if (customerDataWordsAndAlternateWords[item.GetFirstToken().ValueText] == null)
                                customerDataWordsAndAlternateWords.Add(item.GetFirstToken().ValueText, "CustomerData" + index++);
                        }
                        break;
                    //Sensitive Parmeters-themselvs Customer Data word.
                    case SyntaxKind.NamedParameter:
                        if (customerDataWordsAndAlternateWords[n.GetFirstToken().ValueText] == null)
                            customerDataWordsAndAlternateWords.Add(n.GetFirstToken().ValueText, "CustomerData" + index++);
                        break;
                    case SyntaxKind.StringLiteralExpression:
                        if (customerDataWordsAndAlternateWords[n.ToString()] == null)
                            customerDataWordsAndAlternateWords.Add(n.ToString(), "CustomerData" + index++);
                        break;
                }
            });
            return customerDataWordsAndAlternateWords;
        }

        /// <summary>
        /// Replace the customer data words 
        /// </summary>
        /// <param name="query">a Kusto query</param>
        /// <param name="customerDataWordsAndAlternateWords">list of all customer data had found in this query and the alternate words</param>
        /// <returns>new query without customer data</returns>
        public static string ReplaceCustomerData(string query, Hashtable customerDataWordsAndAlternateWords)
        {
            var parseQuery = KustoCode.Parse(query);
            var splitQuery = parseQuery.GetLexicalTokens().ToList();
            var cleanQuery = "";
            splitQuery.ForEach(word => cleanQuery += customerDataWordsAndAlternateWords[word.Text] != null ? customerDataWordsAndAlternateWords[word.Text] + " " : word.Text + " ");
            return cleanQuery;
        }
    }
}
