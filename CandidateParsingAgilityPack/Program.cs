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
            //GetListItemOrTableRowPositiveTrainingCandidates(false);

            //GetTableOrListPositiveTrainingCandidatesWithMultipleBrands();

            GetNegativeTrainingCandidates();

            //GetTestCandidates();
        }

        // Get candidates representing a single confirmed company/ brand relationship
        private static void GetListItemOrTableRowPositiveTrainingCandidates(bool requireMultipleBrands)
        {
            //var knownCompanyBrandRelationships = Helpers.GetKnownCompanyBrandRelationships();
            var knownCompanyBrandRelationships = Helpers.GetKnownCompanyBrandRelationshipsFromConsumerCompanies();

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            if (requireMultipleBrands)
            {
                // First, narrow down the company/ brands entries processed to only those containing multiple brands
                var knownCompanyBrandRelationshipsWithMultipleBrands =
        Helpers.GetKnownCompanyBrandRelationshipsWithMultipleBrands(knownCompanyBrandRelationships);
                var candidates = Helpers.GetCandidatesFromPages(pages, knownCompanyBrandRelationshipsWithMultipleBrands, true);
                Helpers.SaveAndPrintCandidates(candidates);
            }
            else
            {
                var candidates = Helpers.GetCandidatesFromPages(pages, knownCompanyBrandRelationships, false);
                Helpers.SaveAndPrintCandidates(candidates);
            }
        }

        // Get candidates representing confirmed company/ brand relationships between one company and several of its brands
        // Currently just gets relationships with multiple brands
        private static void GetTableOrListPositiveTrainingCandidatesWithMultipleBrands()
        {
            var knownCompanyBrandRelationships = Helpers.GetKnownCompanyBrandRelationshipsFromConsumerCompanies();

            var knownCompanyBrandRelationshipsWithMultipleBrands =
                    Helpers.GetKnownCompanyBrandRelationshipsWithMultipleBrands(knownCompanyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidatesWithMultipleBrands = Helpers.GetCandidatesWithMultipleBrandsFromPages(pages, knownCompanyBrandRelationshipsWithMultipleBrands);

            Helpers.SaveAndPrintCandidates(candidatesWithMultipleBrands);
        }

        // Get candidates containing a company and a brand which do not have a confirmed company/ brand relationship
        private static void GetNegativeTrainingCandidates()
        {
            var knownCompanyBrandRelationships = Helpers.GetKnownCompanyBrandRelationshipsFromConsumerCompanies();

            var knownCompanyBrandNonRelationships = Helpers.GetKnownCompanyBrandNonRelationships(knownCompanyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var negativeCandidates = Helpers.GetCandidatesFromPages(pages, knownCompanyBrandNonRelationships, false);

            Helpers.SaveAndPrintCandidates(negativeCandidates);
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
    }
}