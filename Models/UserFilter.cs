using System.Collections.Generic;

namespace VANTAGE.Models
{
    public class UserFilter
    {
        public string Name { get; set; } = string.Empty;
        public List<FilterCondition> Conditions { get; set; } = new();
    }

    public class FilterCondition
    {
        public string Column { get; set; } = string.Empty;
        public string Criteria { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string LogicOperator { get; set; } = "AND"; // AND or OR
    }

    public static class FilterCriteria
    {
        // Text criteria
        public new const string Equals = "Equals";
        public const string NotEquals = "Not Equals";
        public const string Contains = "Contains";
        public const string NotContains = "Does Not Contain";
        public const string StartsWith = "Starts With";
        public const string EndsWith = "Ends With";
        public const string IsEmpty = "Is Empty";
        public const string IsNotEmpty = "Is Not Empty";

        // Numeric criteria
        public const string GreaterThan = "Greater Than";
        public const string GreaterThanOrEqual = "Greater Than or Equal";
        public const string LessThan = "Less Than";
        public const string LessThanOrEqual = "Less Than or Equal";

        public static List<string> TextCriteria => new()
        {
            Equals,
            NotEquals,
            Contains,
            NotContains,
            StartsWith,
            EndsWith,
            IsEmpty,
            IsNotEmpty
        };

        public static List<string> NumericCriteria => new()
        {
            Equals,
            NotEquals,
            GreaterThan,
            GreaterThanOrEqual,
            LessThan,
            LessThanOrEqual
        };

        public static List<string> AllCriteria => new()
        {
            Equals,
            NotEquals,
            Contains,
            NotContains,
            StartsWith,
            EndsWith,
            IsEmpty,
            IsNotEmpty,
            GreaterThan,
            GreaterThanOrEqual,
            LessThan,
            LessThanOrEqual
        };
    }
}
