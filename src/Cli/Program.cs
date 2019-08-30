using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Cli
{
    class Program
    {
        static Random random = new Random();

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Please specify the data file");
                return;
            }

            var file = args[0];

            if (!File.Exists(file))
            {
                Console.WriteLine("Could not find {0}", Path.GetFullPath(file));
                return;
            }

            var year = DateTime.Now.Year;

            var json = File.ReadAllText(file);
            var data = JsonConvert.DeserializeObject<Data>(json);

            if (data.History.Any(h => h.Year == year))
            {
                Console.WriteLine("There is already an entry for this year");
                return;
            }

            var rules = new List<IRule>();
            rules.Add(new DoNotGiveToOwnFamily(data));
            rules.Add(new NewNameEveryYear(data));
            rules.Add(new DoNotGiveToSomeoneMyFamilyGaveTo(data));

            var entry = new HistoryEntry();

            entry.Year = year;
            entry.Assignments = new List<GiftAssignment>();

            var adults = data.Adults.ToList();
            var kids = data.Kids.ToList();

            var adultPossibilities = CalculatePossibilities(adults, rules);
            var kidsPossibilities = CalculatePossibilities(kids, rules);

            MakeAssignments(adults, adultPossibilities, entry);
            MakeAssignments(kids, kidsPossibilities, entry);

            Console.WriteLine(JsonConvert.SerializeObject(entry, Formatting.Indented));

            data.History.Add(entry);

            data.History = data.History.OrderByDescending(e => e.Year).ToList();
            File.WriteAllText(file, JsonConvert.SerializeObject(data, Formatting.Indented));

        }

        private static Dictionary<string, List<string>> CalculatePossibilities(List<string> names, List<IRule> rules)
        {
            var possibilities = new Dictionary<string, List<string>>();

            foreach (var giver in names)
            {
                var availableNames = names.ToList();

                foreach (var rule in rules)
                {
                    var removed = rule.RemoveNames(availableNames, giver);
                }

                possibilities[giver] = availableNames;
            }

            return possibilities;
        }

        private static void MakeAssignments(List<string> names, Dictionary<string, List<string>> possibilities, HistoryEntry entry)
        {
            var successful = false;
            while (!successful)
            {
                var workingCopy = possibilities.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
                try
                {

                    foreach (var name in names)
                    {
                        bool chosen = false;
                        while (!chosen)
                        {
                            var idx = random.Next(workingCopy[name].Count);

                            var choice = workingCopy[name][idx];

                            bool canRemove = workingCopy.All(kvp => kvp.Key == name || kvp.Value.Count > 1 || !kvp.Value.Contains(choice));

                            if (canRemove)
                            {
                                foreach (var n in names)
                                {
                                    if (n == name) continue;

                                    workingCopy[n].Remove(choice);
                                }

                                workingCopy[name] = new List<string> { choice };
                                chosen = true;
                            }
                            else
                            {
                                workingCopy[name].Remove(choice);

                                if (workingCopy[name].Count == 0)
                                {
                                    throw new Exception("No more choices");
                                }
                            }
                        }
                    }
                    successful = true;

                    foreach (var kvp in workingCopy)
                    {
                        var assignment = new GiftAssignment();
                        assignment.From = kvp.Key;
                        assignment.To = kvp.Value.First();
                        entry.Assignments.Add(assignment);
                    }
                }
                catch(Exception)
                {
                    Console.WriteLine("Something went wrong. Trying again");
                }
            }
        }
    }

    class Data
    {
        public List<string> Adults { get; set; }
        public List<string> Kids { get; set; }
        public List<List<string>> Families { get; set; }
        public List<HistoryEntry> History { get; set; }
    }

    class HistoryEntry
    {
        public int Year { get; set; }
        public List<GiftAssignment> Assignments { get; set; }
    }

    class GiftAssignment
    {
        public string From { get; set; }
        public string To { get; set; }
    }

    interface IRule
    {
        string Name { get; }
        string Description { get; }
        List<string> RemoveNames(List<string> names, string giver);
    }

    class DoNotGiveToOwnFamily : IRule
    {
        private readonly Data _data;

        public string Name { get; }
        public string Description { get; }

        public DoNotGiveToOwnFamily(Data data)
        {
            _data = data;
        }

        public List<string> RemoveNames(List<string> names, string giver)
        {
            var family = (
                from f in _data.Families
                where f.Contains(giver)
                select f).FirstOrDefault();

            if (family == null)
            {
                return new List<string>();
            }

            foreach (var name in family)
            {
                names.Remove(name);
            }

            return family;
        }
    }

    class DoNotGiveToSomeoneMyFamilyGaveTo : IRule
    {
        private readonly Data _data;

        public string Name { get; }
        public string Description { get; }

        public DoNotGiveToSomeoneMyFamilyGaveTo(Data data)
        {
            _data = data;
        }

        public List<string> RemoveNames(List<string> names, string giver)
        {
            var family = (
                from f in _data.Families
                where f.Contains(giver)
                select f).FirstOrDefault();

            if (family == null)
            {
                return new List<string>();
            }

            var familyNames = (
                from a in _data.History.First().Assignments
                where family.Contains(a.From)
                select a.To
            ).ToList();

            foreach (var name in familyNames)
            {
                names.Remove(name);
            }

            return familyNames;
        }
    }

    class NewNameEveryYear : IRule
    {
        private readonly Data _data;

        public string Name { get; }
        public string Description { get; }

        public NewNameEveryYear(Data data)
        {
            _data = data;
        }

        public List<string> RemoveNames(List<string> names, string giver)
        {
            var query =
                from e in _data.History.Take(3)
                from a in e.Assignments
                where a.From == giver
                select a.To;

            var removed = query.ToList();

            foreach (var name in removed)
            {
                names.Remove(name);
            }

            return removed;
        }
    }
}
