namespace CandidateParsingAgilityPack
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Web.UI.WebControls;

    using CandidateParsingAgilityPack.Model;

    using CsQuery.Engine.PseudoClassSelectors;
    using CsQuery.EquationParser.Implementation;
    using CsQuery.ExtensionMethods.Internal;

    using Html;

    using HtmlAgilityPack;

    using Newtonsoft.Json;

    #endregion

    internal class Helpers
    {

        //foreach: Page
        //  foreach: List and table segment on the page
        //     foreach: Relation where company present anywhere on page
        //         foreach: Brand name in this relation
        //             IF the brand name is present in the list or table segment	
        //             foreach: Company name synonym 
        //                 IF the company name is present in the list or table segment, or the page title
        //                 Make a candidate!! 
        internal static List<Candidate> GetCandidatesFromPages(IEnumerable<string> pages, List<CompanyAndBrands> knownCompanyAndBrandsRelationships, bool requireMultipleBrands = false)
        {
            var doc = new HtmlDocument();

            var candidates = new List<Candidate>();

            var safey = new HtmlSanitizer();

            foreach (var page in pages)
            {
                doc.Load(page);

                var root = doc.DocumentNode;

                // The relations containing an owner company mentioned in this page
                var relationsWhereOwnerMentionedOnPage = KnownRelationsWhereCompanyIsPresentOnPage(knownCompanyAndBrandsRelationships, root, page);

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
                                        innerSegments.AddRange(initialcandidate.Node.Descendants("li")
                                                    .ToList()
                                                    .Where(listItem => listItem.OuterHtml.ToLowerInvariant().Contains(brandName.ToLowerInvariant()))); 
                                    }
                                    else if (initialcandidate.Type == "table")
                                    {
                                        innerSegments.AddRange(initialcandidate.Node.Descendants("tr")
                                                    .ToList()
                                                    .Where(listItem => listItem.OuterHtml.ToLowerInvariant().Contains(brandName.ToLowerInvariant()))); 
                                    }

                                    foreach (var innerSegment in innerSegments)
                                    {
                                        numberOfBrandsfromRelationshipPresentInSegment++;

                                        var candidate = new Candidate
                                                            {
                                                                    IsTableSegment =
                                                                            initialcandidate.Type == "table",
                                                                    IsListSegment =
                                                                            initialcandidate.Type == "list",
                                                                    PreviousContent =
                                                                            safey.Sanitize(previousContent),
                                                                    CandidateHtml =
                                                                            safey.Sanitize(
                                                                                           innerSegment.OuterHtml),
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
                                    candidate.containsMultipleBrands = true;
                                }
                            }
                            candidates.AddRange(candidatesForCompany);
                        }
                    }
                }
            }
            if (requireMultipleBrands)
            {
                return candidates.Where(candidate => candidate.containsMultipleBrands == true).ToList();
            }
            else
            {
                return candidates;
            }
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

        // The html and text for the candidate should include the preceeding html node if the candidate is a table or list. 
        // GO UP THROUGH ALL PREVIOUS SIBLINGS, FIND ONE WHERE INNER HTML NOT EMPTY OR "\N"
        // IF NONE, CHECK PREVIOUS SIBLINGS OF PARENT ELEMENT
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
                    && (!parentNode.PreviousSibling.OuterHtml.IsNullOrEmpty() && parentNode.PreviousSibling.OuterHtml != "\n"))
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
                                     && (!parentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
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
                                     && (!grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty
                                                  ()
                                         && grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml != "\n"))))
                    {
                        previousContent = grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml;
                    }
                }
            }
            return previousContent;
        }

        //foreach: Page
        //  foreach: List and table segment on the page
        //     foreach: Relation where company present anywhere on page
        //         int brandsPresent = 0;
        //         foreach: Brand name in this relation
        //             IF the brand name is present in the list or table segment	
        //             foreach: Company name synonym 
        //                 IF the company name is present in the list or table segment, or the page title
        //                      brandsPresent ++
        //                          If brandsPresent > 1 Make a candidate!! & skip to next relation? Will mean fewer candidates

        // Return lists/ tables + company + a list of brand names for the company present in the list/ table
        public static List<Candidate> GetCandidatesWithMultipleBrandsFromPages(IEnumerable<string> pages, List<CompanyAndBrands> knownCompanyAndBrandsRelationships)
        {
            var doc = new HtmlDocument();

            var candidates = new List<Candidate>();

            var safey = new HtmlSanitizer();

            foreach (var page in pages)
            {
                doc.Load(page);

                var root = doc.DocumentNode;

                // The relations containing an owner company mentioned in this page
                var relationsWhereOwnerMentionedOnPage = KnownRelationsWhereCompanyIsPresentOnPage(knownCompanyAndBrandsRelationships, root, page);

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

                            var knownBrandsPresent = new List<String>();
                            var numberOfBrandsfromRelationshipPresentInSegment = 0;

                            // For each brand owned by the company
                            foreach (var brand in relation.BrandNames)
                            {
                                if (initialcandidate.Node.OuterHtml.ToLowerInvariant()
                                                    .Contains(brand.ToLowerInvariant()))
                                {
                                    knownBrandsPresent.Add(brand);
                                    numberOfBrandsfromRelationshipPresentInSegment ++;
                                }
                            }

                            if (knownBrandsPresent.Count > 0)
                            {
                                var candidate = new Candidate
                                    {
                                        IsTableSegment =
                                                initialcandidate.Type == "table",
                                        IsListSegment =
                                                initialcandidate.Type == "list",
                                        PreviousContent =
                                                safey.Sanitize(previousContent),
                                        CandidateHtml =
                                                safey.Sanitize(
                                                               initialcandidate.Node.OuterHtml),
                                        KnownCompany = relation.CompanyNames,
                                        KnownBrands = knownBrandsPresent,
                                        KnownCompanyAndBrands = relation,
                                        DomainOrPageTitleContainsOwner =
                                                domainOrTitleContainsOwner,
                                        Uri = page,
                                        PageTitle = title,
                                        containsMultipleBrands = true
                                    };
                                candidates.Add(candidate);
                            }
                        }
                    }
                }
            }
            return candidates;
        }

        internal static List<CompanyAndBrands> GetKnownCompanyBrandRelationships()
        {
            string API_KEY = "AIzaSyAnlfYJbox67a_jRXUv_9SbGHcfvG0ldbU";
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

        internal static List<CompanyAndBrands> GetKnownCompanyBrandRelationshipsFromConsumerCompanies()
        {
            string API_KEY = "AIzaSyAnlfYJbox67a_jRXUv_9SbGHcfvG0ldbU";
            String url = "https://www.googleapis.com/freebase/v1/mqlread";

            string companiesQuery =
                    "?query=[{\"type\":\"/business/consumer_company\",\"id\": null,\"name\": null,\"brands\":[{\"brand\": null}],\"products\":[{\"consumer_product\": null}],\"limit\":10}]&key=" + API_KEY;

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
                                                              
                                                              BrandNames = allBrandsAndProducts
     ,
                                                              CompanyNames =
                                                                      new List<String>
                                                                          {
                                                                                  freebaseConsumerCompany.Name
                                                                          }
                                                      });
                }
            }
            return companyBrandRelationships;
        }

        public static List<CompanyAndBrands> GetKnownCompanyBrandRelationshipsWithMultipleBrands(
                List<CompanyAndBrands> knownCandidates)
        {
            // TODO combine candidates where the company name is the same
            // Loop through, add to dictionary string/ list? 
            return knownCandidates.Where(r => r.BrandNames.Count() > 1).ToList();
        }

        internal static IEnumerable<string> GetPages(string path)
        {
            return Directory.GetFiles(path, "*.htm*", SearchOption.AllDirectories);
        }

        private static List<CompanyAndBrands> GetTestRelationships()
        {
            var relationships = new List<CompanyAndBrands>();
            // TODO get synonyms from Freebase or alter the CompanyBrandRelation class

            return relationships;
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

        internal static void SaveAndPrintCandidates(List<Candidate> candidates)
        {
            var file = new StreamWriter("C:/Users/Alice/Desktop/PositiveTrainingCandidates.txt");

            foreach (var candidate in candidates)
            {
                file.WriteLine(
                               "Page title: " + candidate.PageTitle + Environment.NewLine + Environment.NewLine
                               + "Known company: " + candidate.KnownCompany.FirstOrDefault().ToString()
                               + Environment.NewLine + "Known brand: " + candidate.KnownBrand + Environment.NewLine
                               + Environment.NewLine + "Multiple brands present:" + candidate.containsMultipleBrands.ToString()
                               + Environment.NewLine + "\r Previous Relevant Node: " + candidate.PreviousContent + Environment.NewLine
                               + "\r Html: " + candidate.CandidateHtml + Environment.NewLine + Environment.NewLine);
                Console.WriteLine(
                                  candidate.PageTitle + Environment.NewLine + ' '
                                  + candidate.KnownCompany.FirstOrDefault().ToString() + Environment.NewLine + ' '
                                  + candidate.CandidateHtml + Environment.NewLine);
            }

            file.Close();

            Console.WriteLine(candidates.Count.ToString());

            Console.ReadLine();
        }

        public static List<CompanyAndBrands> GetKnownCompanyBrandNonRelationships()
        {
            string API_KEY = "AIzaSyAnlfYJbox67a_jRXUv_9SbGHcfvG0ldbU";
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

        public static List<CompanyAndBrands> GetKnownCompanyBrandNonRelationships(
                List<CompanyAndBrands> knownCompanyBrandRelationships)
        {
            // Jumble up the list, creating new relationships where there are none. 
            // Should really use all brands not belonging to the company but this would result in huge amounts of processing? 
            // These ones are going to be hard to find, one way or another
            // Use a supermarket site? 
            var nonRelationships = new List<CompanyAndBrands>();

            for (int i = 0; i < knownCompanyBrandRelationships.Count - 1; i++)
            {
                nonRelationships.Add(
                                     new CompanyAndBrands
                                         {
                                                 CompanyNames =
                                                         knownCompanyBrandRelationships[i]
                                                         .CompanyNames,
                                                                              BrandNames =
                                                                              knownCompanyBrandRelationships
                                                                              [i+1].BrandNames
                                         });
            }

            return nonRelationships;

        }
    }
}