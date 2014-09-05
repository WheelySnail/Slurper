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
            GetPositiveTrainingCandidates(true);

            //GetNegativeTrainingCandidates();

            //GetTestCandidates();
        }

        // Get candidates representing confirmed company/ brand relationships 
        // Param itemLevelCandidates determines whether the candidates returned are entire tables/ lists + all brands they contain, or each individual list item/ table row containing a brand
        private static void GetPositiveTrainingCandidates(bool itemBrandLevelCandidates)
        {
            var knownCompanyBrandRelationships = Helpers.GetKnownCompanyBrandRelationshipsFromConsumerCompanies();

            var knownCompanyBrandRelationshipsWithMultipleBrands =
                    Helpers.GetKnownCompanyBrandRelationshipsWithMultipleBrands(knownCompanyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidatesWithMultipleBrands = Helpers.GetPositiveCandidatesFromPages(pages, knownCompanyBrandRelationshipsWithMultipleBrands, itemBrandLevelCandidates);

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