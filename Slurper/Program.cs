﻿namespace Slurper
{
    #region Using Directives

    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;

    using edu.stanford.nlp.ie.crf;
    using edu.stanford.nlp.tagger.maxent;

    using numl;
    using numl.Model;
    using numl.Supervised;
    using numl.Supervised.DecisionTree;
    using numl.Supervised.NaiveBayes;

    using Slurper.Model;

    using Console = System.Console;

    #endregion

    internal static class Program
    {
        private static void Main(string[] args)
        {
            // ItemLevelCandidates retrieves list item/ table row level candidates representing a relationship between one company and one brand
            // Change to false to retrieve list/ table level candidates representing one company and any of its brands present in that block
            const bool ItemLevelCandidates = false;

            MaxentTagger tagger = new MaxentTagger(@"C:/Users/Alice/Desktop/stanford-postagger-2014-08-27/models/english-left3words-distsim.tagger");

            CRFClassifier classifier =
            CRFClassifier.getClassifierNoExceptions(
                @"C:/Users/Alice/Desktop/english.all.3class.distsim.crf.ser.gz");

            var knownCompanyBrandRelationships = FreeBaseHelpers.GetKnownCompanyBrandRelationshipsFromConsumerCompanies();

            var trainingCandidates = new List<Candidate>();

            trainingCandidates.AddRange(GetPositiveTrainingCandidates(
                                                                           knownCompanyBrandRelationships,
                                                                           ItemLevelCandidates, classifier, tagger));

            // Make sure the number of positive and negative candidates is equal
            var positivesCount = trainingCandidates.Count();

            var negativeTrainingCandidates = GetNegativeTrainingCandidates(
                                                                           knownCompanyBrandRelationships,
                                                                           ItemLevelCandidates, classifier, tagger, positivesCount);

            trainingCandidates.AddRange(negativeTrainingCandidates);

            var testCandidates = GetTestCandidates(knownCompanyBrandRelationships, ItemLevelCandidates, classifier, tagger);

             //Decision tree
            var d = Descriptor.Create<Candidate>();
            var decisionTreeGenerator = new DecisionTreeGenerator(d);
            decisionTreeGenerator.SetHint(0.5);
            // The Learner uses 80% of the data to train the model and 20% to test the model. The learner also runs the generator 1000 times and returns the most accurate model.
            var dtModel = Learner.Learn(trainingCandidates, 0.80, 1000, decisionTreeGenerator);
            Console.WriteLine(dtModel);
            ClassifyWithDecisionTree(testCandidates, dtModel);

            //// Naive bayes
            //// https://github.com/sethjuarez/numl/issues/12
            //IGenerator nbGenerator = new NaiveBayesGenerator(20);
            //nbGenerator.Descriptor = Descriptor.Create<Candidate>();
            //LearningModel learningModel = Learner.Learn(trainingCandidates, 0.80, 1000, nbGenerator);
            //IModel nbModel = learningModel.Model;
            //Console.WriteLine(nbModel);
            //Console.WriteLine(learningModel.Accuracy);
            //ClassifyWithNaiveBayes(testCandidates, nbModel);

            // Neural network
            // https://github.com/sethjuarez/numl/blob/master/numl.Tests/SupervisedTests/NeuralNetworkTests.cs

            Helpers.OutputCandidates(testCandidates, "labelledTestCandidates");

            Helpers.OutputJsonResults(testCandidates);

            Console.WriteLine("done");

            Console.ReadLine();
        }

        private static void ClassifyWithNaiveBayes(List<Candidate> testCandidates, IModel nbModel)
        {
            for (int i = 0; i < testCandidates.Count; i++)
            {
                testCandidates[i] = nbModel.Predict(testCandidates[i]);
            }
        }

        private static void ClassifyWithDecisionTree(List<Candidate> testCandidates, LearningModel model)
        {
            for (int i = 0; i < testCandidates.Count; i++)
            {
                //testCandidates[i] = model.<Candidate>(testCandidates[i]);
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

        private static List<Candidate> GetPositiveTrainingCandidates(List<CompanyAndBrands> companyBrandRelationships, bool itemBrandLevelCandidates, CRFClassifier classifier, MaxentTagger tagger)
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
                                                                    true, classifier, tagger);

            Helpers.OutputCandidates(candidates, "positivetraining");

            return candidates;
        }

        private static List<Candidate> GetNegativeTrainingCandidates(List<CompanyAndBrands> companyBrandRelationships, bool itemBrandLevelCandidates, CRFClassifier classifier, MaxentTagger tagger, int positivesCount)
        {
            var knownCompanyBrandNonRelationships =
                    Helpers.CreateKnownCompanyBrandNonRelationships(companyBrandRelationships);

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var negativeCandidates = Helpers.GetTrainingCandidatesFromPages(
                                                                            pages,
                                                                            knownCompanyBrandNonRelationships,
                                                                            itemBrandLevelCandidates,
                                                                            false, classifier, tagger, positivesCount);

            Helpers.OutputCandidates(negativeCandidates, "negativetraining");

            return negativeCandidates;
        }

        private static List<Candidate> GetTestCandidates(List<CompanyAndBrands> companyBrandRelationships, bool itemLevelCandidates, CRFClassifier classifier, MaxentTagger tagger)
        {
            var testBrands = Helpers.GetTestBrands(companyBrandRelationships, tagger, classifier);

            var testCompanies = Helpers.GetTestCompanies(companyBrandRelationships);

            // Option to remove brands which are also in the companies list 
            //for (int i = testBrands.Count; i >= 0; i--)
            //{
            //    if (testCompanies.Any(tc => tc.Equals(testBrands[i])))
            //    {
            //        testBrands.RemoveAt(i);
            //    }
            //}

            var pages = Helpers.GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var testCandidates = Helpers.GetTestCandidatesFromPages(pages, testCompanies, testBrands, itemLevelCandidates, classifier, tagger);

            return testCandidates;
        }
    }

}


