﻿namespace CandidateParsingAgilityPack
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;

    using CandidateParsingAgilityPack.Model;

    using CsQuery.ExtensionMethods.Internal;

    using Html;

    using HtmlAgilityPack;

    using Newtonsoft.Json;

    #endregion

    internal class Helpers
    {
        // Return lists/ tables + company + a list of brand names for the company present in the list/ table
        /// <summary>
        /// Searches pages for html segments representing a known company brand relationship 
        /// </summary>
        /// <param name="pages">The pages to extract the candidates from</param>
        /// <param name="knownCompanyAndBrandsRelationships">A set of known relationships to look for in these pages</param>
        /// <param name="itemBrandLevelCandidates">If true, candidates returned will represent an individual list item/ table row containing a brand. If false, candiates returned will represent entire tables/ lists + all brands they contain</param>
        /// <param name="positiveCandidates">If true, candidates returned will be labelled as positive candidates. If false, candidates returned will be labelled as negative candidates</param>
        /// <returns></returns>
        public static List<Candidate> GetTrainingCandidatesFromPages(IEnumerable<string> pages, List<CompanyAndBrands> knownCompanyAndBrandsRelationships, bool itemBrandLevelCandidates, bool positiveCandidates)
        {
            var doc = new HtmlDocument();

            var candidates = new List<Candidate>();

            var safey = new HtmlSanitizer();

            foreach (var page in pages)
            {
                doc.Load(page);

                var root = doc.DocumentNode;

                // The relations containing an owner company mentioned in this page
                var relationsWhereOwnerMentionedOnPage =
                        KnownRelationsWhereCompanyIsPresentOnPage(knownCompanyAndBrandsRelationships, root, page);

                // Skip to next page if the current page doesn't contain a seed company name
                if (relationsWhereOwnerMentionedOnPage.Count == 0)
                {
                    continue;
                }

                // Gather the page title, which is an attribute of all candidates
                var titleElement = doc.DocumentNode.SelectSingleNode("//head/title");
                var title = "";

                if (titleElement != null)
                {
                    title = titleElement.InnerText;
                }

                // Gather all tables and lists. Doesn't seem to be a way to retrieve a node's html element type after it's been stored so add this to an initial candidate object! 
                // Exclude tables and lists which include child lists

                var initialCandidateSegments =
                        root.Descendants("table")
                            .ToList()
                            .Where(table => !table.OuterHtml.Contains("<ul>"))
                            .Select(table => new InitialCandidate() { Node = table, Type = "table" })
                            .ToList();

                initialCandidateSegments.AddRange(
                                                  root.Descendants("ul")
                                                      .ToList()
                                                      .Where(list => !list.InnerHtml.Contains("<ul>"))
                                                      .Select(
                                                              list =>
                                                              new InitialCandidate() { Node = list, Type = "list" }));

                // For each list or table in the page
                foreach (var initialcandidate in initialCandidateSegments)
                {
                    // For each relation where an owner name is present on this page. Candidates are created at this level as they are a combination of relation + segment.
                    foreach (var relation in relationsWhereOwnerMentionedOnPage)
                    {
                        var domainOrTitleContainsOwner = false;

                        var initialCandidateOrPreviousSiblingContainOwner = false;

                        var previousContent = GetPreviousRelevantNode(initialcandidate);

                        // Check that the owner name for the relation is present in the title, domain, list/ table or previous relevant node. This is a necessary but not sufficient condition for creating a candidate
                        // Only use one company name at the moment so a loop isn't necessary
                        foreach (var ownerSynonym in relation.CompanyNames)
                        {
                            if (page.ToLowerInvariant().Contains(ownerSynonym.ToLowerInvariant() + " ")
                                || title.ToLowerInvariant().Contains(ownerSynonym.ToLowerInvariant() + " "))
                            {
                                domainOrTitleContainsOwner = true;
                            }
                            if (
                                    initialcandidate.Node.OuterHtml.ToLowerInvariant()
                                                    .Contains(ownerSynonym.ToLowerInvariant() + " ")
                                    || previousContent.ToLowerInvariant()
                                                      .Contains(ownerSynonym.ToLowerInvariant() + " "))
                            {
                                initialCandidateOrPreviousSiblingContainOwner = true;
                            }
                        }

                        // If the company name for the relation is present in the title, domain, list/ table or previous relevant node, continue to check for brand names
                        if (domainOrTitleContainsOwner || initialCandidateOrPreviousSiblingContainOwner)
                        {
                            var knownBrandsPresent = new List<String>();

                            // For each brand owned by the company
                            foreach (var brand in relation.BrandNames)
                            {
                                // The space at the end helps to stop token fragments from being picked up as brand name instances
                                if (
                                        initialcandidate.Node.OuterHtml.ToLowerInvariant()
                                                        .Contains(brand.ToLowerInvariant() + " "))
                                {
                                    knownBrandsPresent.Add(brand);
                                    // Create a candidate here if want item level candidates? But now to check 'multiple'? 
                                }
                            }

                            if (knownBrandsPresent.Count > 0)
                            {
                                // Process to create a candidate for each individual list item or table row containing a brand for the relation
                                if (itemBrandLevelCandidates)
                                {
                                    foreach (var brand in knownBrandsPresent)
                                    {
                                        var innerSegments = new List<HtmlNode>();

                                        if (initialcandidate.Type == "list")
                                        {
                                            innerSegments.AddRange(
                                                                   initialcandidate.Node.Descendants("li")
                                                                                   .ToList()
                                                                                   .Where(
                                                                                          listItem =>
                                                                                          listItem.OuterHtml
                                                                                                  .ToLowerInvariant()
                                                                                                  .Contains(
                                                                                                            brand
                                                                                                                    .ToLowerInvariant
                                                                                                                    ())));
                                        }
                                        else if (initialcandidate.Type == "table")
                                        {
                                            innerSegments.AddRange(
                                                                   initialcandidate.Node.Descendants("tr")
                                                                                   .ToList()
                                                                                   .Where(
                                                                                          listItem =>
                                                                                          listItem.OuterHtml
                                                                                                  .ToLowerInvariant()
                                                                                                  .Contains(
                                                                                                            brand
                                                                                                                    .ToLowerInvariant
                                                                                                                    ())));
                                        }

                                        foreach (var innerSegment in innerSegments)
                                        {
                                            var candidate = new Candidate
                                                                {
                                                                        IsTableSegment =
                                                                                initialcandidate.Type
                                                                                == "table",
                                                                        IsListSegment =
                                                                                initialcandidate.Type
                                                                                == "list",
                                                                        IsItemLevelCandidate = true,
                                                                        PreviousContent =
                                                                                safey.Sanitize(
                                                                                               previousContent),
                                                                        CandidateHtml =
                                                                                safey.Sanitize(
                                                                                               innerSegment
                                                                                                       .OuterHtml),
                                                                        KnownCompany = relation.CompanyNames,
                                                                        KnownBrands = knownBrandsPresent,
                                                                        KnownCompanyAndBrands = relation,
                                                                        DomainOrPageTitleContainsOwner =
                                                                                domainOrTitleContainsOwner,
                                                                        Uri = page,
                                                                        PageTitle = title,
                                                                        ContainsMultipleBrands =
                                                                                knownBrandsPresent.Count > 1,
                                                                        CompanyBrandRelationship = positiveCandidates
                                                                };
                                            candidates.Add(candidate);
                                        }
                                    }
                                }

                                        // Process to create a candidate for each whole list or table and all its brands
                                else
                                {
                                    var candidate = new Candidate
                                                        {
                                                                IsTableSegment = initialcandidate.Type == "table",
                                                                IsListSegment = initialcandidate.Type == "list",
                                                                IsItemLevelCandidate = false,
                                                                PreviousContent = safey.Sanitize(previousContent),
                                                                CandidateHtml =
                                                                        safey.Sanitize(
                                                                                       initialcandidate.Node
                                                                                                       .OuterHtml),
                                                                KnownCompany = relation.CompanyNames,
                                                                KnownBrands = knownBrandsPresent,
                                                                KnownCompanyAndBrands = relation,
                                                                DomainOrPageTitleContainsOwner =
                                                                        domainOrTitleContainsOwner,
                                                                Uri = page,
                                                                PageTitle = title,
                                                                ContainsMultipleBrands =
                                                                        knownBrandsPresent.Count > 1,
                                                                CompanyBrandRelationship = positiveCandidates
                                                        };
                                    candidates.Add(candidate);
                                }
                            }
                        }
                    }
                }
            }
            return candidates;
        }

        public static List<CompanyAndBrands> GetKnownCompanyBrandNonRelationships()
        {
            string API_KEY = "";
            String url = "https://www.googleapis.com/freebase/v1/mqlread";
            String query =
                    "?query=[{\"id\":null,\"company\":null,\"brand\":null,\"type\":\"/business/company_brand_relationship\",\"limit\":2}]&key="
                    + API_KEY;

            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage reponse = client.GetAsync(query).Result;

            if (reponse.IsSuccessStatusCode)
            {
                var responseString = reponse.Content.ReadAsStringAsync().Result;
                var safey = new HtmlSanitizer();
                safey.Sanitize(responseString);
                var relationships = JsonConvert.DeserializeObject<FreeBaseRelationshipsResponse>(responseString);
                return MapFreeBaseRelationshipToCompanyBrandRelationship(relationships.Relationships);
            }
            else
            {
                throw new Exception();
            }

            // https://api.opencorporates.com/companies/search?q=barclays+bank
            // https://api.opencorporates.com/companies/gb/01320086/network
        }

        // Jumble up the list, creating new relationships where there are none. 
        // Should use all brands not belonging to the company but this may mean lots of processing? 
        // These ones are going to be hard to find, one way or another
        // Use a supermarket site? 
        public static List<CompanyAndBrands> GetKnownCompanyBrandNonRelationships(
                List<CompanyAndBrands> knownCompanyBrandRelationships)
        {
            var nonRelationships = new List<CompanyAndBrands>();

            // Create giant list of all brand names
            var allBrands = new List<String>();

            foreach (var knownCompanyBrandRelationship in knownCompanyBrandRelationships)
            {
                allBrands.AddRange(knownCompanyBrandRelationship.BrandNames);
            }

            // For each entry, make new local copy of giant list
            // Remove items which are associated with the brand
            for (int i = 0; i < knownCompanyBrandRelationships.Count - 1; i++)
            {
                var allTheBrandsExceptYoursI = new List<string>();
                allTheBrandsExceptYoursI.AddRange(allBrands);

                foreach (var brand in allBrands)
                {
                    if (knownCompanyBrandRelationships[i].BrandNames.Contains(brand))
                    {
                        allTheBrandsExceptYoursI.Remove(brand);
                    }
                }

                nonRelationships.Add(
                                     new CompanyAndBrands
                                         {
                                                 CompanyNames =
                                                         knownCompanyBrandRelationships[i].CompanyNames,
                                                 BrandNames = allTheBrandsExceptYoursI
                                         });
            }

            return nonRelationships;
        }

        public static List<CompanyAndBrands> GetKnownCompanyBrandRelationshipsWithMultipleBrands(
                List<CompanyAndBrands> knownCandidates)
        {
            // TODO combine candidates where the company name is the same
            // Loop through, add to dictionary string/ list? 
            return knownCandidates.Where(r => r.BrandNames.Count() > 1).ToList();
        }

        internal static List<Candidate> GetCandidatesFromPagesOld(
                IEnumerable<string> pages,
                List<CompanyAndBrands> knownCompanyAndBrandsRelationships,
                bool requireMultipleBrands = false)
        {
            var doc = new HtmlDocument();

            var candidates = new List<Candidate>();

            var safey = new HtmlSanitizer();

            foreach (var page in pages)
            {
                doc.Load(page);

                var root = doc.DocumentNode;

                // The relations containing an owner company mentioned in this page
                var relationsWhereOwnerMentionedOnPage =
                        KnownRelationsWhereCompanyIsPresentOnPage(knownCompanyAndBrandsRelationships, root, page);

                // Skip to next page if the current page doesn't contain a seed company name
                if (relationsWhereOwnerMentionedOnPage.Count == 0)
                {
                    continue;
                }

                // Gather the page title, which is an attribute of all candidates
                var titleElement = doc.DocumentNode.SelectSingleNode("//head/title");
                var title = "";

                if (titleElement != null)
                {
                    title = titleElement.InnerText;
                }

                // Gather all tables and lists. Doesn't seem to be a way to retrieve a node's html element type after it's been stored so add this to an initial candidate object! 
                // Exclude tables and lists which include child lists

                var initialCandidateSegments =
                        root.Descendants("table")
                            .ToList()
                            .Where(table => !table.OuterHtml.Contains("<ul>"))
                            .Select(table => new InitialCandidate() { Node = table, Type = "table" })
                            .ToList();

                initialCandidateSegments.AddRange(
                                                  root.Descendants("ul")
                                                      .ToList()
                                                      .Where(list => !list.InnerHtml.Contains("<ul>"))
                                                      .Select(
                                                              list =>
                                                              new InitialCandidate() { Node = list, Type = "list" }));

                // For each list or table in the page
                foreach (var initialcandidate in initialCandidateSegments)
                {
                    // For each relation where an owner name is present on this page. Candidates are created at this level as they are a combination of relation + segment.
                    foreach (var relation in relationsWhereOwnerMentionedOnPage)
                    {
                        var domainOrTitleContainsOwner = false;

                        var initialCandidateOrPreviousSiblingContainOwner = false;

                        var previousContent = GetPreviousRelevantNode(initialcandidate);

                        // Check that the owner name for the relation is present in the title, domain, list/ table or previous relevant node. This is a necessary but not sufficient condition for creating a candidate
                        // Only use one company name at the moment so a loop isn't necessary
                        foreach (var ownerSynonym in relation.CompanyNames)
                        {
                            if (page.ToLowerInvariant().Contains(ownerSynonym.ToLowerInvariant() + " ")
                                || title.ToLowerInvariant().Contains(ownerSynonym.ToLowerInvariant() + " "))
                            {
                                domainOrTitleContainsOwner = true;
                            }
                            if (
                                    initialcandidate.Node.OuterHtml.ToLowerInvariant()
                                                    .Contains(ownerSynonym.ToLowerInvariant() + " ")
                                    || previousContent.ToLowerInvariant()
                                                      .Contains(ownerSynonym.ToLowerInvariant() + " "))
                            {
                                initialCandidateOrPreviousSiblingContainOwner = true;
                            }
                        }

                        // If the company name for the relation is present in the title, domain, list/ table or previous relevant node, continue to check for brand names
                        if (domainOrTitleContainsOwner || initialCandidateOrPreviousSiblingContainOwner)
                        {
                            var candidatesForCompany = new List<Candidate>();

                            var numberOfBrandsfromRelationshipPresentInSegment = 0;

                            // For each brand owned by the company
                            foreach (var brand in relation.BrandNames)
                            {
                                // Get any tr or li element in the initial candidate containing the brand name
                                string brandName = brand;

                                var innerSegments = new List<HtmlNode>();

                                if (initialcandidate.Type == "list")
                                {
                                    innerSegments.AddRange(
                                                           initialcandidate.Node.Descendants("li")
                                                                           .ToList()
                                                                           .Where(
                                                                                  listItem =>
                                                                                  listItem.OuterHtml.ToLowerInvariant()
                                                                                          .Contains(
                                                                                                    brandName
                                                                                                            .ToLowerInvariant
                                                                                                            ())));
                                }
                                else if (initialcandidate.Type == "table")
                                {
                                    innerSegments.AddRange(
                                                           initialcandidate.Node.Descendants("tr")
                                                                           .ToList()
                                                                           .Where(
                                                                                  listItem =>
                                                                                  listItem.OuterHtml.ToLowerInvariant()
                                                                                          .Contains(
                                                                                                    brandName
                                                                                                            .ToLowerInvariant
                                                                                                            ())));
                                }

                                foreach (var innerSegment in innerSegments)
                                {
                                    numberOfBrandsfromRelationshipPresentInSegment++;

                                    var candidate = new Candidate
                                                        {
                                                                IsTableSegment = initialcandidate.Type == "table",
                                                                IsListSegment = initialcandidate.Type == "list",
                                                                PreviousContent = safey.Sanitize(previousContent),
                                                                CandidateHtml =
                                                                        safey.Sanitize(innerSegment.OuterHtml),
                                                                KnownCompany = relation.CompanyNames,
                                                                KnownBrand = brand,
                                                                KnownCompanyAndBrands = relation,
                                                                DomainOrPageTitleContainsOwner =
                                                                        domainOrTitleContainsOwner,
                                                                Uri = page,
                                                                PageTitle = title
                                                        };

                                    candidatesForCompany.Add(candidate);
                                }
                            }

                            if (numberOfBrandsfromRelationshipPresentInSegment > 1)
                            {
                                foreach (var candidate in candidatesForCompany)
                                {
                                    candidate.ContainsMultipleBrands = true;
                                }
                            }
                            candidates.AddRange(candidatesForCompany);
                        }
                    }
                }
            }
            if (requireMultipleBrands)
            {
                return candidates.Where(candidate => candidate.ContainsMultipleBrands).ToList();
            }
            else
            {
                return candidates;
            }
        }

        internal static List<CompanyAndBrands> GetKnownCompanyBrandRelationshipsFromConsumerCompanies()
        {
            string API_KEY = "AIzaSyAnlfYJbox67a_jRXUv_9SbGHcfvG0ldbU";
            String url = "https://www.googleapis.com/freebase/v1/mqlread";

            string companiesQuery =
                    "?query=[{\"type\":\"/business/consumer_company\",\"id\": null,\"name\": null,\"brands\":[{\"brand\": null}],\"products\":[{\"consumer_product\": null}],\"limit\":10}]&key="
                    + API_KEY;

            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage reponse = client.GetAsync(companiesQuery).Result;

            if (reponse.IsSuccessStatusCode)
            {
                var responseString = reponse.Content.ReadAsStringAsync().Result;
                var safey = new HtmlSanitizer();
                safey.Sanitize(responseString);
                var brands = JsonConvert.DeserializeObject<FreeBaseConsumerCompanyResponse>(responseString);
                return MapFreeBaseConsumerCompaniesToCompanyBrandRelationship(brands.Companies);
            }
            else
            {
                throw new Exception();
            }

            // https://api.opencorporates.com/companies/search?q=barclays+bank
            // https://api.opencorporates.com/companies/gb/01320086/network
        }

        internal static IEnumerable<string> GetPages(string path)
        {
            return Directory.GetFiles(path, "*.htm*", SearchOption.AllDirectories);
        }

        internal static void SaveAndPrintCandidates(List<Candidate> candidates, string type)
        {
            var file = new StreamWriter("C:/Users/Alice/Desktop/" + type + ".txt");

            foreach (var candidate in candidates)
            {
                file.WriteLine(
                               "Page title: " + candidate.PageTitle + Environment.NewLine + Environment.NewLine
                               + "Known company: " + candidate.KnownCompany.FirstOrDefault().ToString()
                               + Environment.NewLine + "Known brand: " + candidate.KnownBrand + Environment.NewLine
                               + Environment.NewLine + "Multiple brands present:"
                               + candidate.ContainsMultipleBrands.ToString() + Environment.NewLine
                               + "\r Previous Relevant Node: " + candidate.PreviousContent + Environment.NewLine
                               + "\r Html: " + candidate.CandidateHtml + Environment.NewLine + Environment.NewLine);
                Console.WriteLine(
                                  candidate.PageTitle + Environment.NewLine + ' '
                                  + candidate.KnownCompany.FirstOrDefault().ToString() + Environment.NewLine + ' '
                                  + candidate.CandidateHtml + Environment.NewLine);
            }

            file.Close();

            Console.WriteLine(candidates.Count.ToString());
        }

        protected static List<Candidate> FilterCandidatesForMultipleBrandPresence(List<Candidate> allCandidates)
        {
            return allCandidates.Where(candidate => candidate.ContainsMultipleBrands).ToList();
        }

        private static string GetPreviousRelevantNode(InitialCandidate initialcandidate)
        {
            var previousContent = "";
            var previousSibling = initialcandidate.Node.PreviousSibling;
            var parentNode = initialcandidate.Node.ParentNode;
            var grandparentNode = initialcandidate.Node.ParentNode.ParentNode;

            // Check the three preceeding previous siblings
            if (!previousSibling.OuterHtml.IsNullOrEmpty() && previousSibling.OuterHtml != "\n")
            {
                previousContent = previousSibling.OuterHtml;
            }
            else
            {
                if (previousSibling.PreviousSibling != null
                    && (!previousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                        && previousSibling.PreviousSibling.OuterHtml != "\n"))
                {
                    previousContent = previousSibling.PreviousSibling.OuterHtml;
                }
                else if (previousSibling.PreviousSibling != null
                         && (previousSibling.PreviousSibling.PreviousSibling != null
                             && (!previousSibling.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                 && previousSibling.PreviousSibling.PreviousSibling.OuterHtml != "\n")))
                {
                    previousContent = previousSibling.PreviousSibling.PreviousSibling.OuterHtml;
                }
            }

            // Check the three previous siblings of the parent sibling
            if (previousContent.IsNullOrEmpty())
            {
                if (parentNode.PreviousSibling != null
                    && (!parentNode.PreviousSibling.OuterHtml.IsNullOrEmpty()
                        && parentNode.PreviousSibling.OuterHtml != "\n"))
                {
                    previousContent = parentNode.PreviousSibling.OuterHtml;
                }
                else
                {
                    if (parentNode.PreviousSibling != null
                        && (parentNode.PreviousSibling.PreviousSibling != null
                            && (!parentNode.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                && parentNode.PreviousSibling.PreviousSibling.OuterHtml != "\n")))
                    {
                        previousContent = parentNode.PreviousSibling.PreviousSibling.OuterHtml;
                    }
                    else if (parentNode.PreviousSibling != null
                             && (parentNode.PreviousSibling.PreviousSibling != null
                                 && (parentNode.PreviousSibling.PreviousSibling.PreviousSibling != null
                                     && (!parentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml
                                                    .IsNullOrEmpty()
                                         && parentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml != "\n"))))
                    {
                        previousContent = parentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml;
                    }
                }
            }

            // Check the three previous siblings of the grandparent sibling
            if (previousContent.IsNullOrEmpty())
            {
                if (grandparentNode.PreviousSibling != null
                    && (!grandparentNode.PreviousSibling.OuterHtml.IsNullOrEmpty()
                        && grandparentNode.PreviousSibling.OuterHtml != "\n"))
                {
                    previousContent = grandparentNode.PreviousSibling.OuterHtml;
                }
                else
                {
                    if (grandparentNode.PreviousSibling != null
                        && (grandparentNode.PreviousSibling.PreviousSibling != null
                            && (!grandparentNode.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                && grandparentNode.PreviousSibling.PreviousSibling.OuterHtml != "\n")))
                    {
                        previousContent = grandparentNode.PreviousSibling.PreviousSibling.OuterHtml;
                    }
                    else if (grandparentNode.PreviousSibling != null
                             && (grandparentNode.PreviousSibling.PreviousSibling != null
                                 && (grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling != null
                                     && (!grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml
                                                         .IsNullOrEmpty()
                                         && grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml
                                         != "\n"))))
                    {
                        previousContent = grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml;
                    }
                }
            }
            return previousContent;
        }

        private static List<CompanyAndBrands> GetTestRelationships()
        {
            var relationships = new List<CompanyAndBrands>();
            // TODO get synonyms from Freebase or alter the CompanyBrandRelation class

            return relationships;
        }

        private static List<CompanyAndBrands> KnownRelationsWhereCompanyIsPresentOnPage(
                List<CompanyAndBrands> knownCompanyAndBrandsRelationships,
                HtmlNode root,
                string page)
        {
            var relationsWhereOwnerMentionedOnPage = new List<CompanyAndBrands>();
            // Check if the page or filename contains any known seed company names 
            foreach (var relation in knownCompanyAndBrandsRelationships)
            {
                foreach (var ownerCompanySynonym in relation.CompanyNames)
                {
                    if (root.OuterHtml.ToLowerInvariant().Contains(ownerCompanySynonym.ToLowerInvariant() + " ")
                        || page.ToLowerInvariant().Contains(ownerCompanySynonym.ToLowerInvariant() + " "))
                    {
                        relationsWhereOwnerMentionedOnPage.Add(relation);
                        // stop checking for synonym mentions once one synonym for a company has been identified 
                        break;
                    }
                }
            }
            return relationsWhereOwnerMentionedOnPage;
        }

        private static List<CompanyAndBrands> MapFreeBaseConsumerCompaniesToCompanyBrandRelationship(
                List<FreebaseConsumerCompany> companies)
        {
            var companyBrandRelationships = new List<CompanyAndBrands>();
            foreach (var freebaseConsumerCompany in companies)
            {
                if (freebaseConsumerCompany != null)
                {
                    var allBrandsAndProducts = new List<string>();
                    foreach (var brand in freebaseConsumerCompany.Brands)
                    {
                        allBrandsAndProducts.Add(brand.Brand);
                    }

                    foreach (var product in freebaseConsumerCompany.Products)
                    {
                        allBrandsAndProducts.Add(product.Product);
                    }

                    companyBrandRelationships.Add(
                                                  new CompanyAndBrands
                                                      {
                                                              BrandNames = allBrandsAndProducts,
                                                              CompanyNames =
                                                                      new List<String>
                                                                          {
                                                                                  freebaseConsumerCompany
                                                                                          .Name
                                                                          }
                                                      });
                }
            }
            return companyBrandRelationships;
        }

        private static List<CompanyAndBrands> MapFreeBaseRelationshipToCompanyBrandRelationship(
                List<FreebaseCompanyBrandRelationship> companies)
        {
            var companyBrandRelationships = new List<CompanyAndBrands>();
            foreach (var freebaseCompanyBrandRelationship in companies)
            {
                if (freebaseCompanyBrandRelationship != null)
                {
                    companyBrandRelationships.Add(
                                                  new CompanyAndBrands
                                                      {
                                                              BrandNames =
                                                                      new List<String>
                                                                          {
                                                                                  freebaseCompanyBrandRelationship
                                                                                          .Brand
                                                                          },
                                                              CompanyNames =
                                                                      new List<String>
                                                                          {
                                                                                  freebaseCompanyBrandRelationship
                                                                                          .Company
                                                                          },
                                                              RelationshipId =
                                                                      freebaseCompanyBrandRelationship
                                                                      .RelationshipId
                                                      });
                }
            }
            return companyBrandRelationships;
        }
    }
}