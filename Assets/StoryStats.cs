﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ink.Runtime;
using System.Text.RegularExpressions;

public static class StoryStats
{
    public struct VariableStats
    {
        public double Minimum;
        public double Maximum;
        public double Mean;
        public double Median;
        public List<int> Modes;
        public double StandardDeviation;
    }

    public const string WordCountVariableName = "word_count";
    public const int Trials = 100;

    private readonly static Regex FlagRegex = new Regex(@"#\w+\s+");
    private readonly static Regex WhitespaceRegex = new Regex(@"\s+");

    public static Dictionary<string, VariableStats> CalculateVariableStats(string storyJson, IEnumerable<string> variableNames, int trials = Trials, string stoppingPoint = null)
    {
        var suppliedVariableNames = variableNames.ToList();
        var story = new Story(storyJson);
        story.BindExternalFunction("makeNewDesiredOrder", () =>
            {
                CoffeeMinigame.Instance.MakeNewDesiredOrder();
                return CoffeeMinigame.Instance.CurrentDesiredOrder.ToString().ToLowerInvariant();
            });
        story.BindExternalFunction("setCaffeine", (bool caffeinated) => CoffeeMinigame.Instance.SetCaffeine(caffeinated));
        story.BindExternalFunction("setShots", (int number) => CoffeeMinigame.Instance.SetShots(number));
        story.BindExternalFunction("addMilk", () => CoffeeMinigame.Instance.AddMilk());
        story.BindExternalFunction("addFoam", () => CoffeeMinigame.Instance.AddFoam());
        story.BindExternalFunction("addVanilla", () => CoffeeMinigame.Instance.AddVanilla());
        story.BindExternalFunction("addStrawberry", () => CoffeeMinigame.Instance.AddStrawberry());
        story.BindExternalFunction("addChocolate", () => CoffeeMinigame.Instance.AddChocolate());
        story.BindExternalFunction("finishCreatedOrder", () => { return CoffeeMinigame.Instance.FinishCreatedOrder(); });
        story.BindExternalFunction("getCreatedOrder", () => CoffeeMinigame.Instance.CurrentCreatedOrder.ToString().ToLowerInvariant());
        story.BindExternalFunction("keepTakingOrders", () => { return CoffeeMinigame.Instance.KeepTakingOrders(); });

        var results = new Dictionary<string, List<int>>();
        var rng = new Random();
        Regex stoppingPointRegex = string.IsNullOrEmpty(stoppingPoint) ? null : new Regex(stoppingPoint);
        for (int currTrial = 0; currTrial < trials; currTrial++)
        {
            int wordCount = 0;
            string line;
            while (true)
            {
                if (stoppingPointRegex != null && stoppingPointRegex.IsMatch(story.currentText))
                {
                    break;
                }

                if (story.canContinue)
                {
                    line = story.Continue();
                    if (!GameManager.IsDirective(line))
                    {
                        line = line.Substring(line.IndexOf(':') + 1).Trim();
                        line = FlagRegex.Replace(line, string.Empty).Trim();
                        var splitLine = WhitespaceRegex.Split(line);
                        wordCount += splitLine.Length;
                    }
                }
                else if (story.currentChoices.Count > 0)
                {
                    story.ChooseChoiceIndex(rng.Next(story.currentChoices.Count));
                }
                else
                {
                    break;
                }
            }

            if (!suppliedVariableNames.Contains(WordCountVariableName))
            {
                if (!results.ContainsKey(WordCountVariableName))
                {
                    results[WordCountVariableName] = new List<int>();
                }
                results[WordCountVariableName].Add(wordCount);
            }
            foreach (var variableName in suppliedVariableNames)
            {
                if (!results.ContainsKey(variableName))
                {
                    results[variableName] = new List<int>();
                }
                results[variableName].Add((int)story.variablesState[variableName]);
            }
            story.ResetState();
            CoffeeMinigame.Instance.Reset();
        }

        var allVariableNames = new List<String>(suppliedVariableNames);
        if (!allVariableNames.Contains(WordCountVariableName))
        {
            allVariableNames.Add(WordCountVariableName);
        }
        Dictionary<string, VariableStats> output = new Dictionary<string, VariableStats>();
        foreach (var variableName in allVariableNames)
        {
            var varStats = new VariableStats();
            var doubleResults = results[variableName].Select(x => (double)x).OrderBy(x => x).ToList();

            varStats.Minimum = doubleResults.First();
            varStats.Maximum = doubleResults.Last();

            varStats.Mean = doubleResults.Average();

            int medianPoint = trials / 2;
            if (trials % 2 == 0)
            {
                varStats.Median = (doubleResults[medianPoint] + doubleResults[medianPoint - 1]) / 2.0;
            }
            else
            {
                varStats.Median = doubleResults[medianPoint];
            }

            var counts = results[variableName].GroupBy(x => x);
            var maxCount = counts.Select(g => g.Count()).Max();
            varStats.Modes = counts.Where(g => g.Count() == maxCount).Select(g => g.Key).ToList();

            varStats.StandardDeviation = Math.Sqrt(doubleResults.Select(x => Math.Pow(x - varStats.Mean, 2)).Sum() / trials);
            output[variableName] = varStats;
        }

        return output;
    }
}
