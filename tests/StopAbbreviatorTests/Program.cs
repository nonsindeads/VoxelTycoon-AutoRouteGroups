using AutoRouteGroups;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var names = new[]
{
    new KeyValuePair<int, string>(1, "Mannheim"),
    new KeyValuePair<int, string>(2, "Marburg"),
    new KeyValuePair<int, string>(3, "Bad Ems"),
    new KeyValuePair<int, string>(4, "Bad Essen"),
    new KeyValuePair<int, string>(5, "New York"),
    new KeyValuePair<int, string>(6, "New Yarmouth"),
    new KeyValuePair<int, string>(7, "Saint Paul"),
    new KeyValuePair<int, string>(8, "Saint Peter"),
    new KeyValuePair<int, string>(9, "Same Name"),
    new KeyValuePair<int, string>(10, "Same Name")
};

Dictionary<int, string> abbreviations = StopAbbreviator.Build(names);

Assert(abbreviations.Count == names.Length, "Every location needs an abbreviation.");
Assert(abbreviations.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Length,
    "Abbreviations must be globally unique.");
Assert(abbreviations.Values.All(value => value.Length is >= 2 and <= 4),
    "Abbreviations must contain two to four characters.");
Assert(StopAbbreviator.CreateCandidate("Bad Ems", 2) == "BE", "Bad Ems should become BE.");
Assert(StopAbbreviator.CreateCandidate("New York", 2) == "NY", "New York should become NY.");
Assert(StopAbbreviator.CreateCandidate("Saint Paul", 3) == "SPA", "Saint Paul should preserve its prefix.");

Console.WriteLine("StopAbbreviator tests passed.");
