namespace CandidateParsingAgilityPack
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using HtmlAgilityPack;

    #endregion

    internal static class Program
    {
        private static void Main(string[] args)
        {
            GetTrainingCandidates();

            GetTestCandidates();
        }
        
        private static void GetTestCandidates()
        {
            // TODO 
            Console.ReadLine();
        }

        // Get training examples which contain a known company/ brand relationship
        private static void GetTrainingCandidates()
        {
            // TODO must sanitise this data, as it's user generated

            var knownCompanyBrandRelationships = Helpers.GetKnownCompanyBrandRelationships();

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidates = Helpers.GetCandidatesFromPages(pages, knownCompanyBrandRelationships);

            var file = new StreamWriter("C:/Users/Alice/Desktop/Candidates.txt");

            foreach (var candidate in candidates)
            {
                file.WriteLine(
                               "Page title: " + candidate.PageTitle + Environment.NewLine + Environment.NewLine
                               + "Known company: " + candidate.KnownCompany.ToList() + Environment.NewLine
                               + Environment.NewLine + "\r Html & Text: " + candidate.CandidateHtmlAndText
                               + Environment.NewLine + Environment.NewLine);
                Console.WriteLine(
                                  candidate.PageTitle + Environment.NewLine + ' '
                                  + candidate.KnownCompany.ToList().ToString() + Environment.NewLine + ' '
                                  + candidate.CandidateHtmlAndText + Environment.NewLine);
            }

            file.Close();

            Console.WriteLine(candidates.Count.ToString());

            Console.ReadLine();
        }
    }
}