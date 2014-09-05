namespace CandidateParsingAgilityPack
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
        public static List<CompanyAndBrands> CreateKnownCompanyBrandNonRelationships(
                List<CompanyAndBrands> knownCompanyBrandRelationships)
        {
            var nonRelationships = new List<CompanyAndBrands>();

            // Create giant list of all brand names
            var allBrands = new List<string>();

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

        public static List<CompanyAndBrands> FilterKnownCompanyBrandRelationshipsForMultipleBrandRelationshipsOnly(
                List<CompanyAndBrands> knownCandidates)
        {
            // TODO combine candidates where the company name is the same
            return knownCandidates.Where(r => r.BrandNames.Count() > 1).ToList();
        }

        public static List<Candidate> GetTestCandidatesFromPages(
                IEnumerable<string> pages,
                List<string> companies,
                List<string> brands,
                bool itemBrandLevelCandidates)
        {
            var testCandidates = new List<Candidate>();

            var doc = new HtmlDocument();

            var safey = new HtmlSanitizer();

            foreach (var page in pages)
            {
                doc.Load(page);

                var root = doc.DocumentNode;

                // The company names mentioned in this page
                var companiesMentionedOnPage = GetCompaniesPresentOnPage(companies, root, page);

                // Skip to next page if the current page doesn't contain any seed company names
                if (companiesMentionedOnPage.Count == 0)
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
                    // For each company mentioned on this page.
                    foreach (var company in companiesMentionedOnPage)
                    {
                        var domainOrTitleContainsPotentialOwner = false;

                        var initialCandidateOrPreviousSiblingContainsPotentialOwner = false;

                        var previousContent = GetPreviousRelevantNode(initialcandidate);

                        // Check that the owner name for the relation is present in the title, domain, list/ table or previous relevant node. This is a necessary but not sufficient condition for creating a candidate
                        // Only use one company name at the moment so a loop isn't necessary

                        if (page.ToLowerInvariant().Contains(company.ToLowerInvariant() + " ")
                            || title.ToLowerInvariant().Contains(company.ToLowerInvariant() + " "))
                        {
                            domainOrTitleContainsPotentialOwner = true;
                        }
                        if (initialcandidate.Node.OuterHtml.ToLowerInvariant()
                                            .Contains(company.ToLowerInvariant() + " ")
                            || previousContent.ToLowerInvariant().Contains(company.ToLowerInvariant() + " "))
                        {
                            initialCandidateOrPreviousSiblingContainsPotentialOwner = true;
                        }

                        // If a company name is present in the title, domain, list/ table or previous relevant node, continue to check for brand names
                        if (domainOrTitleContainsPotentialOwner
                            || initialCandidateOrPreviousSiblingContainsPotentialOwner)
                        {
                            var brandsPresentInInitialCandidate = new List<string>();

                            // For each brand 
                            foreach (var brand in brands)
                            {
                                // The space at the end helps to stop token fragments from being picked up as brand name instances
                                if (
                                        initialcandidate.Node.OuterHtml.ToLowerInvariant()
                                                        .Contains(brand.ToLowerInvariant() + " "))
                                {
                                    brandsPresentInInitialCandidate.Add(brand);
                                }
                            }

                            if (brandsPresentInInitialCandidate.Count > 0)
                            {
                                // Process to create a candidate for each individual list item or table row containing a brand for the relation
                                if (itemBrandLevelCandidates)
                                {
                                    foreach (var brand in brandsPresentInInitialCandidate)
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
                                                                        KnownCompany = company,
                                                                        KnownBrands =
                                                                                brandsPresentInInitialCandidate,
                                                                        KnownBrand = brand,
                                                                        DomainOrPageTitleContainsOwner =
                                                                                domainOrTitleContainsPotentialOwner,
                                                                        Uri = page,
                                                                        PageTitle = title,
                                                                        ContainsMultipleBrands =
                                                                                brandsPresentInInitialCandidate
                                                                                        .Count > 1
                                                                };
                                            testCandidates.Add(candidate);
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
                                                                KnownCompany = company,
                                                                KnownBrands = brandsPresentInInitialCandidate,
                                                                DomainOrPageTitleContainsOwner =
                                                                        domainOrTitleContainsPotentialOwner,
                                                                Uri = page,
                                                                PageTitle = title,
                                                                ContainsMultipleBrands =
                                                                        brandsPresentInInitialCandidate.Count > 1,
                                                        };
                                    testCandidates.Add(candidate);
                                }
                            }
                        }
                    }
                }
            }

            return testCandidates;
        }

        public static List<Candidate> GetTrainingCandidatesFromPages(
                IEnumerable<string> pages,
                List<CompanyAndBrands> knownCompanyAndBrandsRelationships,
                bool itemBrandLevelCandidates,
                bool positiveCandidates)
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
                        GetKnownRelationsWhereCompanyIsPresentOnPage(knownCompanyAndBrandsRelationships, root, page);

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
                            var knownBrandsPresent = new List<string>();

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
                                                                        KnownCompanyNames =
                                                                                relation.CompanyNames,
                                                                        KnownBrands = knownBrandsPresent,
                                                                        KnownBrand = brand,
                                                                        KnownCompanyAndBrands = relation,
                                                                        DomainOrPageTitleContainsOwner =
                                                                                domainOrTitleContainsOwner,
                                                                        Uri = page,
                                                                        PageTitle = title,
                                                                        ContainsMultipleBrands =
                                                                                knownBrandsPresent.Count > 1,
                                                                        CompanyBrandRelationship =
                                                                                positiveCandidates
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
                                                                KnownCompanyNames = relation.CompanyNames,
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

        internal static IEnumerable<string> GetPages(string path)
        {
            return Directory.GetFiles(path, "*.htm*", SearchOption.AllDirectories);
        }

        internal static List<String> GetTestBrands(List<CompanyAndBrands> companyBrandRelationships)
        {
            // TODO get brands from other sources, e.g. supermarket site, Amazon
            // http://stackoverflow.com/questions/1595624/amazon-products-api-looking-for-basic-overview-and-information
            var knownBrands = FreeBaseHelpers.GetKnownBrands();

            var newBrands = new List<String>();

            foreach (var brand in knownBrands.Brands)
            {
                if (!companyBrandRelationships.Any(rel => rel.BrandNames.Contains(brand.Name)))
                {
                    newBrands.Add(brand.Name);
                }
            }

            return newBrands;
        }

        internal static List<String> GetTestCompanies(List<CompanyAndBrands> companyBrandRelationships)
        {
            // TODO retrieve all companies from Freebase, OpenCorporates or others, not just consumer companies
            //var knownCompanies = GetKnownCompanies();

            var newCompanies = new List<String>();

            //foreach (var company in knownCompanies)
            //{
            //    if (!companyBrandRelationships.Any(rel => rel.Contains(company.Name)))
            //    {
            //        newCompanies.Add(company.Name);
            //    }
            //}

            return newCompanies;
        }

        internal static void OutputCandidates(List<Candidate> candidates, string type)
        {
            var file = new StreamWriter("C:/Users/Alice/Desktop/" + type + ".txt");

            foreach (var candidate in candidates)
            {
                file.WriteLine(
                               "Contains company/brand relationship? " + candidate.CompanyBrandRelationship.ToString()
                               + Environment.NewLine + Environment.NewLine + "Page title: " + candidate.PageTitle
                               + Environment.NewLine + Environment.NewLine + "Known company: "
                               + candidate.KnownCompanyNames.FirstOrDefault().ToString() + Environment.NewLine
                               + "Known brand: " + candidate.KnownBrand + Environment.NewLine + Environment.NewLine
                               + "Multiple brands present:" + candidate.ContainsMultipleBrands.ToString()
                               + Environment.NewLine + "\r Previous Relevant Node: " + candidate.PreviousContent
                               + Environment.NewLine + "\r Html: " + candidate.CandidateHtml + Environment.NewLine
                               + Environment.NewLine);
                Console.WriteLine(
                                  candidate.PageTitle + Environment.NewLine + ' '
                                  + candidate.KnownCompanyNames.FirstOrDefault().ToString() + Environment.NewLine + ' '
                                  + candidate.CandidateHtml + Environment.NewLine);
            }

            file.Close();

            Console.WriteLine(candidates.Count.ToString());
        }

        protected static List<Candidate> FilterCandidatesForMultipleBrandPresence(List<Candidate> allCandidates)
        {
            return allCandidates.Where(candidate => candidate.ContainsMultipleBrands).ToList();
        }

        private static List<string> GetCompaniesPresentOnPage(List<string> companies, HtmlNode root, string page)
        {
            var companiesMentionedOnPage = new List<string>();
            // Check if the page or filename contains any known seed company names 
            foreach (var company in companies)
            {
                if (root.OuterHtml.ToLowerInvariant().Contains(company.ToLowerInvariant() + " ")
                    || page.ToLowerInvariant().Contains(company.ToLowerInvariant() + " "))
                {
                    companiesMentionedOnPage.Add(company);
                }
            }
            return companiesMentionedOnPage;
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

        private static List<CompanyAndBrands> GetKnownRelationsWhereCompanyIsPresentOnPage(
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
    }
}