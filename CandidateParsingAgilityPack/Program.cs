namespace CandidateParsingAgilityPack
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using CandidateParsingAgilityPack.Model;

    using HtmlAgilityPack;

    #endregion

    internal static class Program
    {
        private static void Main(string[] args)
        {
            GetPositiveTrainingCandidates();

            //GetPositiveTrainingCandidatesWithMultipleBrands();

            GetNegativeTrainingCandidates();

            GetTestCandidates();
        }

        private static void GetPositiveTrainingCandidatesWithMultipleBrands()
        {
            var knownCompanyBrandRelationships = Helpers.GetKnownCompanyBrandRelationships();

            var knownCompanyBrandRelationshipsWithMultipleBrands =
                    Helpers.GetKnownCompanyBrandRelationshipsWithMultipleBrands(knownCompanyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidatesWithMultipleBrands = Helpers.GetCandidatesFromPages(pages, knownCompanyBrandRelationshipsWithMultipleBrands);

            Helpers.SaveAndPrintCandidates(candidatesWithMultipleBrands);
        }

        private static void GetNegativeTrainingCandidates()
        {
            // 
        }

        private static void GetTestCandidates()
        {
            // TODO 

            // Get synonyms
            //var allCompanies = getAllCompanies();

            //var allBrands = getAllBrands();

            var pages = Helpers.GetPages(""); // ENDNAS01/Personal/Alice/Wikipedia/a/a/a

            //var testCandidates = Helpers.GetTestCandidatesFromPages();

            var file = new StreamWriter("C:/Users/Alice/Desktop/TestCandidates.txt");

            Console.ReadLine();
        }

        // Get training examples which contain a known company/ brand relationship
        private static void GetPositiveTrainingCandidates()
        {
            var knownCompanyBrandRelationships = Helpers.GetKnownCompanyBrandRelationships();

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidates = Helpers.GetCandidatesFromPages(pages, knownCompanyBrandRelationships);

            Helpers.SaveAndPrintCandidates(candidates);
        }
    }
}