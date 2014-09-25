namespace Slurper
{
    #region Using Directives

    using System;
    using System.Collections.Generic;

    using edu.stanford.nlp.ie.crf;

    using numl;
    using numl.Model;
    using numl.Supervised;
    using numl.Supervised.DecisionTree;

    using Slurper.Model;

    #endregion

    internal static class Program
    {
        private static void Main(string[] args)
        {
            // ItemLevelCandidates retrieves list item/ table row level candidates representing a relationship between one company and one brand
            // Change to false to retrieve list/ table level candidates representing one company and any of its brands present in that block
            const bool ItemLevelCandidates = false;

            var sentence1 = "Apple Genius Bar are a type of shop";
            var sentence2 = "Genius Bar, the workplace of Anita Millas";
            var sentence3 = "Microsoft are a large corporation based in the USA, Europe, Paris, PorcupineHat";

            CRFClassifier classifier =
            CRFClassifier.getClassifierNoExceptions(
                @"C:/Users/Alice/Desktop/english.all.3class.distsim.crf.ser.gz");

            Console.WriteLine("{0}\n", classifier.classifyToString(sentence3));

            var classification = classifier.classify(sentence1).toArray();
            for (var i = 0; i < classification.Length; i++)
            {
                Console.WriteLine("{0}\n:{1}\n", i, classification[i]);
            }
            
            // classified output contains 2+ People
            // classified output contains 2+ Organisations
            // classified output contains 2+ Places

            var knownCompanyBrandRelationships = FreeBaseHelpers.GetKnownCompanyBrandRelationshipsFromConsumerCompanies();

            var trainingCandidates = new List<Candidate>();

            trainingCandidates.AddRange(GetPositiveTrainingCandidates(
                                                                           knownCompanyBrandRelationships,
                                                                           ItemLevelCandidates));

            trainingCandidates.AddRange(GetNegativeTrainingCandidates(
                                                                           knownCompanyBrandRelationships,
                                                                           ItemLevelCandidates));

            var testCandidates = GetTestCandidates(knownCompanyBrandRelationships, ItemLevelCandidates);

            //// Create naive bayes, holding back data
            //var d = Descriptor.Create<Candidate>();
            //var g = new DecisionTreeGenerator(d);
            //g.SetHint(0.5);
            //// The Learner uses 80% of the data to train the model and 20% to test the model. The learner also runs the generator 1000 times and returns the most accurate model.
            //var nbmodel = Learner.Learn(trainingCandidates, 0.80, 1000, g);
            //Console.WriteLine(nbmodel);
            //Console.ReadLine();

            // Create decision tree
            var model = GenerateModel(trainingCandidates);

            Console.WriteLine(model);

            ClassifyTestCandidates(testCandidates, model);

            Helpers.OutputCandidates(testCandidates, "labelledTestCandidates");

            Helpers.OutputJsonResults(testCandidates);

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

            // TODO Remove duplicates between the two lists
            //for (int i = testBrands.Count; i >= 0; i--)
            //{
            //    if (testCompanies.Any(tc => tc.Equals(testBrands[i])))
            //    {
            //        testBrands.RemoveAt(i);
            //    }
            //}

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var testCandidates = Helpers.GetTestCandidatesFromPages(pages, testCompanies, testBrands, itemLevelCandidates);

            return testCandidates;
        }
    }

}


