namespace CandidateParsingAgilityPack
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using CandidateParsingAgilityPack.Model;

    using HtmlAgilityPack;

    using numl.Model;
    using numl.Supervised;
    using numl.Supervised.DecisionTree;

    #endregion

    internal static class Program
    {
        private static void Main(string[] args)
        {
            // ItemLevelCandidates retrieves list item/ table row level candidates representing a relationship between one company and one brand
            // Change to false to retrieve list/ table level candidates representing one company and any of its brands present in that block
            const bool ItemLevelCandidates = true;

            var knownCompanyBrandRelationships = FreeBaseHelpers.GetKnownCompanyBrandRelationshipsFromConsumerCompanies();

            var positiveTrainingCandidates = GetPositiveTrainingCandidates(
                                                                           knownCompanyBrandRelationships,
                                                                           ItemLevelCandidates);

            var negativeTrainingCandidates = GetNegativeTrainingCandidates(
                                                                           knownCompanyBrandRelationships,
                                                                           ItemLevelCandidates);

            var trainingCandidates = new List<Candidate>();
            trainingCandidates.AddRange(positiveTrainingCandidates);
            trainingCandidates.AddRange(negativeTrainingCandidates);

            var testCandidates = GetTestCandidates(knownCompanyBrandRelationships, ItemLevelCandidates);

            var model = GenerateModel(trainingCandidates);

            Console.WriteLine(model);

            ClassifyTestCandidates(testCandidates, model);

            Helpers.OutputCandidates(testCandidates, "labelledTestCandidates");

            Console.ReadLine();
        }

        private static void ClassifyTestCandidates(List<Candidate> testCandidates, IModel model)
        {
            for (int i = 0; i < testCandidates.Count; i++)
            {
                testCandidates[i] = model.Predict<Candidate>(testCandidates[i]);
            }
        }

        private static IModel GenerateModel(List<Candidate> trainingCandidates)
        {
            // description maps the class and it's attributes to the ML algorithm, and back
            var descriptor = Descriptor.Create < Candidate>();

            var generator = new DecisionTreeGenerator(200, 2, null, null, 0.5);

            //generator.SetHint(true);

            var model = generator.Generate(descriptor, trainingCandidates);

            return model;
        }

        private static List<Candidate> GetPositiveTrainingCandidates(
                List<CompanyAndBrands> companyBrandRelationships,
                bool itemBrandLevelCandidates)
        {
            // Only use seed data where the company owns more than one brand
            // TODO should I remove this step as it's not matched in the negative examples? 
            var knownCompanyBrandRelationshipsWithMultipleBrands =
                    Helpers.FilterKnownCompanyBrandRelationshipsForMultipleBrandRelationshipsOnly(companyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidates = Helpers.GetTrainingCandidatesFromPages(
                                                                    pages,
                                                                    knownCompanyBrandRelationshipsWithMultipleBrands,
                                                                    itemBrandLevelCandidates,
                                                                    true);

            Helpers.OutputCandidates(candidates, "positivetraining");

            return candidates;
        }

        private static List<Candidate> GetNegativeTrainingCandidates(
                List<CompanyAndBrands> companyBrandRelationships,
                bool itemBrandLevelCandidates)
        {
            var knownCompanyBrandNonRelationships =
                    Helpers.CreateKnownCompanyBrandNonRelationships(companyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var negativeCandidates = Helpers.GetTrainingCandidatesFromPages(
                                                                            pages,
                                                                            knownCompanyBrandNonRelationships,
                                                                            itemBrandLevelCandidates,
                                                                            false);

            Helpers.OutputCandidates(negativeCandidates, "negativetraining");

            return negativeCandidates;
        }

        private static List<Candidate> GetTestCandidates(
                List<CompanyAndBrands> companyBrandRelationships,
                bool itemLevelCandidates)
        {
            var testBrands = Helpers.GetTestBrands(companyBrandRelationships);

            var testCompanies = Helpers.GetTestCompanies(companyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");
            // TODO use real data

            var testCandidates = Helpers.GetTestCandidatesFromPages(pages, testCompanies, testBrands, itemLevelCandidates);

            Helpers.OutputCandidates(testCandidates, "unlabelledTestCandidates");

            return testCandidates;
        }
    }

}


