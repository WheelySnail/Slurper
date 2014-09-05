﻿namespace CandidateParsingAgilityPack
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
            var knownCompanyBrandRelationships = Helpers.GetKnownCompanyBrandRelationshipsFromConsumerCompanies();

            var positiveTrainingCandidates = GetPositiveTrainingCandidates(knownCompanyBrandRelationships, true);

            var negativeTrainingCandidates = GetNegativeTrainingCandidates(knownCompanyBrandRelationships, true);

            var testCandidates = GetTestCandidates(knownCompanyBrandRelationships);

            Console.ReadLine();
        }

        /// <summary>
        /// Get candidates representing confirmed company/ brand relationships 
        /// </summary>
        /// <param name="companyBrandRelationships"></param>
        /// <param name="itemBrandLevelCandidates">If true, candidates returned will represent an individual list item/ table row containing a brand. If false, candidates returned will represent entire tables/ lists + all brands they contain</param>
        /// <returns>Candidates representing a known company brand relationship</returns>
        private static List<Candidate> GetPositiveTrainingCandidates(List<CompanyAndBrands> companyBrandRelationships, bool itemBrandLevelCandidates)
        {
            // Only use seed data where the company owns more than one brand
            // TODO should I remove this step as it's not matched in the negative examples? 
            var knownCompanyBrandRelationshipsWithMultipleBrands =
                    Helpers.GetKnownCompanyBrandRelationshipsWithMultipleBrands(companyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidates = Helpers.GetTrainingCandidatesFromPages(pages, knownCompanyBrandRelationshipsWithMultipleBrands, itemBrandLevelCandidates, true);

            Helpers.SaveAndPrintCandidates(candidates, "positivetraining");

            return candidates;
        }

        /// <summary>
        /// Get candidates containing a company and a brand which do not have a confirmed company/ brand relationship
        /// </summary>
        /// <returns>Candidates which are known not to contain a company brand relationship</returns>
        private static List<Candidate> GetNegativeTrainingCandidates(List<CompanyAndBrands> companyBrandRelationships, bool itemBrandLevelCandidates)
        {
            var knownCompanyBrandNonRelationships = Helpers.GetKnownCompanyBrandNonRelationships(companyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var negativeCandidates = Helpers.GetTrainingCandidatesFromPages(pages, knownCompanyBrandNonRelationships, itemBrandLevelCandidates, false);

            Helpers.SaveAndPrintCandidates(negativeCandidates, "negativetraining");

            return negativeCandidates;
        }

        /// <summary>
        /// Get candidate segments containing a company and brand not yet known to have a relationship
        /// </summary>
        /// <param name="companyBrandRelationships"></param>
        /// <returns></returns>
        private static List<Candidate> GetTestCandidates(List<CompanyAndBrands> companyBrandRelationships)
        {
            var testCompaniesAndBrands = GetTestCompaniesAndBrands(companyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments"); // ENDNAS01/Personal/Alice/Wikipedia/a/a/a

            var testCandidates = GetTestCandidatesFromPages(pages, testCompaniesAndBrands);

            Helpers.SaveAndPrintCandidates(testCandidates, "test");

            return testCandidates;
        }

        private static TestCompaniesAndBrands GetTestCompaniesAndBrands(List<CompanyAndBrands> companyBrandRelationships)
        {
            var testCompaniesAndBrands = new TestCompaniesAndBrands();
            return testCompaniesAndBrands;
        }

        private static List<Candidate> GetTestCandidatesFromPages(IEnumerable<string> pages, object testCompaniesAndBrands)
        {
            var testCandidates = new List<Candidate>();
            return testCandidates;
        }
    }

    internal class TestCompaniesAndBrands
    {
        public List<String> companies { get; set; }

        public List<String> brands { get; set; }
    }
}