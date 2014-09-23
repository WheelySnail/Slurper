namespace CandidateParsingAgilityPack
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using CandidateParsingAgilityPack.Model;

    using com.sun.tools.javac.util;

    using CsQuery.ExtensionMethods.Internal;

    using Html;

    using HtmlAgilityPack;

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

                        var previousContentContainsPotentialOwner = false;

                        var candidateHtmlContainsPotentialOwner = false;

                        var previousContent = GetPreviousRelevantNode(initialcandidate);

                        var previousContentOuterHtml = previousContent == null
                                                               ? ""
                                                               : previousContent.OuterHtml;

                        // Check that the owner name for the relation is present in the title, domain, list/ table or previous relevant node. This is a necessary but not sufficient condition for creating a candidate
                        // Only use one company name at the moment so a loop isn't necessary

                        if (page.ToLowerInvariant().Contains(company.ToLowerInvariant() + " ")
                            || title.ToLowerInvariant().Contains(company.ToLowerInvariant() + " "))
                        {
                            domainOrTitleContainsPotentialOwner = true;
                        }
                        if (initialcandidate.Node.OuterHtml.ToLowerInvariant()
                                            .Contains(company.ToLowerInvariant() + " "))
                        {
                            candidateHtmlContainsPotentialOwner = true;
                        }
                        if (previousContentOuterHtml.ToLowerInvariant().Contains(company.ToLowerInvariant() + " "))
                        {
                            previousContentContainsPotentialOwner = true;
                        }

                        // If a company name is present in the title, domain, list/ table or previous relevant node, continue to check for brand names
                        if (domainOrTitleContainsPotentialOwner
                            || candidateHtmlContainsPotentialOwner || previousContentContainsPotentialOwner)
                        {
                            var brandsPresentInInitialCandidate = new List<string>();

                            // For each brand 
                            foreach (var brand in brands)
                            {
                                var brandOnItsOwn = new Regex(@"\b" + brand.ToLowerInvariant() + @"\b");
                                if (
                                        brandOnItsOwn.IsMatch(initialcandidate.Node.OuterHtml.ToLowerInvariant()))
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
                                                                                               previousContentOuterHtml),
                                                                        CandidateHtml =
                                                                                safey.Sanitize(
                                                                                               innerSegment
                                                                                                       .OuterHtml),
                                                                        KnownCompanyNames = new List<string>(),
                                                                        KnownBrands =
                                                                                brandsPresentInInitialCandidate,
                                                                        KnownBrand = brand,
                                                                        DomainOrPageTitleContainsOwner =
                                                                                domainOrTitleContainsPotentialOwner,
                                                                        PreviousContentContainsPotentialOwner = previousContentContainsPotentialOwner,
                                                                        CandidateHtmlContainsPotentialOwner = candidateHtmlContainsPotentialOwner,
                                                                        Uri = page,
                                                                        PageTitle = title,
                                                                        ContainsMultipleBrands =
                                                                                brandsPresentInInitialCandidate
                                                                                        .Count > 1
                                                                };
                                            candidate.KnownCompanyNames.Add(company);
                                            testCandidates.Add(candidate);
                                        }
                                    }
                                }

                                        // Process to create a candidate for each whole list or table and all its brands
                                else
                                {
                                    var wordsInPreviousContent = new List<string>();
                                    if (previousContent != null)
                                    {
                                        wordsInPreviousContent = previousContent.InnerText.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                                    }

                                    var wordsInCandidateHtml = initialcandidate.Node.InnerText.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                                    var candidate = new Candidate
                                                        {
                                                                IsTableSegment = initialcandidate.Type == "table",
                                                                IsListSegment = initialcandidate.Type == "list",
                                                                IsItemLevelCandidate = false,
                                                                PreviousContent = safey.Sanitize(previousContentOuterHtml),
                                                                PreviousContentWordCount = wordsInPreviousContent.Count(),
                                                                WordsInPreviousContent = wordsInPreviousContent,
                                                                CandidateHtml =
                                                                        safey.Sanitize(
                                                                                       initialcandidate.Node
                                                                                                       .OuterHtml),
                                                                
                                                                CandidateHtmlWordCount = wordsInCandidateHtml.Count(),
                                                                WordsInCandidateHtml = wordsInCandidateHtml,
                                                                KnownCompanyNames = new List<String>(),
                                                                KnownBrands = brandsPresentInInitialCandidate,
                                                                DomainOrPageTitleContainsOwner =
                                                                        domainOrTitleContainsPotentialOwner,
                                                                PreviousContentContainsPotentialOwner = previousContentContainsPotentialOwner,
                                                                CandidateHtmlContainsPotentialOwner = candidateHtmlContainsPotentialOwner,
                                                                Uri = page,
                                                                PageTitle = title,
                                                                ContainsMultipleBrands =
                                                                        brandsPresentInInitialCandidate.Count > 1,
                                                        };
                                    candidate.KnownCompanyNames.Add(company);
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

                        var candidateHtmlContainsPotentialOwner = false;

                        var previousContentContainsPotentialOwner = false;

                        var previousContent = GetPreviousRelevantNode(initialcandidate);

                        var previousContentOuterHtml = previousContent == null
                                                               ? ""
                                                               : previousContent.OuterHtml;

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
                                                    .Contains(ownerSynonym.ToLowerInvariant() + " "))
                            {
                                candidateHtmlContainsPotentialOwner = true;
                            }
                            if (
                                    previousContentOuterHtml.ToLowerInvariant()
                                                            .Contains(ownerSynonym.ToLowerInvariant() + " "))
                            {
                                previousContentContainsPotentialOwner = true;
                            }
                        }

                        // If the company name for the relation is present in the title, domain, list/ table or previous relevant node, continue to check for brand names
                        if (domainOrTitleContainsOwner || candidateHtmlContainsPotentialOwner || previousContentContainsPotentialOwner)
                        {
                            var knownBrandsPresent = new List<string>();

                            // For each brand owned by the company, for this relation
                            foreach (var brand in relation.BrandNames)
                            {
                                var brandOnItsOwn = new Regex(@"\b" + brand.ToLowerInvariant() + @"\b");
                                if (
                                        brandOnItsOwn.IsMatch(initialcandidate.Node.OuterHtml.ToLowerInvariant()))
                                {
                                    knownBrandsPresent.Add(brand);
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
                                                                                               previousContentOuterHtml),
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
                                                                        PreviousContentContainsPotentialOwner = previousContentContainsPotentialOwner,
                                                                        CandidateHtmlContainsPotentialOwner = candidateHtmlContainsPotentialOwner,
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
                                    var wordsInPreviousContent = new List<string>();

                                    if (previousContent != null)
                                    {
                                        wordsInPreviousContent = previousContent.InnerText.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                                    }

                                    var wordsInCandidateHtml = initialcandidate.Node.InnerText.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);

                                    var candidate = new Candidate
                                                        {
                                                                IsTableSegment = initialcandidate.Type == "table",
                                                                IsListSegment = initialcandidate.Type == "list",
                                                                IsItemLevelCandidate = false,
                                                                PreviousContent = safey.Sanitize(previousContentOuterHtml),
                                                                WordsInPreviousContent = wordsInPreviousContent.ToList(),
                                                                PreviousContentWordCount = wordsInPreviousContent.Count(),
                                                                CandidateHtml =
                                                                        safey.Sanitize(
                                                                                       initialcandidate.Node
                                                                                                       .OuterHtml),
                                                                WordsInCandidateHtml = wordsInCandidateHtml.ToList(),
                                                                CandidateHtmlWordCount = wordsInCandidateHtml.Count(),
                                                                KnownCompanyNames = relation.CompanyNames,
                                                                KnownBrands = knownBrandsPresent,
                                                                KnownCompanyAndBrands = relation,
                                                                DomainOrPageTitleContainsOwner =
                                                                        domainOrTitleContainsOwner,
                                                                PreviousContentContainsPotentialOwner = previousContentContainsPotentialOwner,
                                                                CandidateHtmlContainsPotentialOwner = candidateHtmlContainsPotentialOwner,
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
            var amazonBrands = new List<String>();

            // TODO get brands from API http://stackoverflow.com/questions/1595624/amazon-products-api-looking-for-basic-overview-and-information
            var directory = new DirectoryInfo("C:/Users/Alice/Desktop/AmazonBrands");

            foreach (var file in directory.GetFiles())
            {
                amazonBrands.AddRange(File.ReadLines(file.FullName));
            }

            var newBrands = new List<String>();

            foreach (var brand in amazonBrands)
            {
                var trimmedBrand = brand.Trim();

                if ((!companyBrandRelationships.Any(rel => rel.BrandNames.Contains(trimmedBrand))) && trimmedBrand.Length > 2)
                {
                    newBrands.Add(trimmedBrand.ToLowerInvariant());
                }
                else
                {
                    var i = 0;
                }
            }

            var troublesomeBrands = new List<String> { "ee", "bury", "et", "ion", "ist", "ngs", "ry", "ss", "ting", "up", "ls", "oe", "may", "div", "se", "od", "tr", "ns", "ge", "ms", "ic", "am", "chocolate", "fruit", "green", "mini", "his", "ge", "ns", "pa", "ams", "none", "sen", "ips", "premium", "ps", "ub", "at" };
            var brandStopList = new List<String> { "bury", "ting", "may", "div", "chocolate", "fruit", "green", "mini", "his", "none", "food", "sen", "ips", "premium", "ion", "ist", "ngs", "ams", "none", "ist", "rts", "book", "official", "entity", "ref", "image", "border", "icon", "max", "bold", "default", "amp", "august", "rft", "non" };

            foreach (var stopListBrand in brandStopList)
            {
                newBrands.RemoveAll(nb => nb.Equals(stopListBrand));
            }

            return newBrands;
        }

        internal static List<String> GetTestCompanies(List<CompanyAndBrands> companyBrandRelationships)
        {
            // TODO retrieve all companies from Freebase, OpenCorporates or others, not just consumer companies

            var companiesHouseCompanies = new List<String>();

            var directory = new DirectoryInfo("C:/Users/Alice/Desktop/Companies");

            foreach (var file in directory.GetFiles())
            {
                companiesHouseCompanies.AddRange(File.ReadLines(file.FullName));
            }

            var newCompanies = new List<String>();

            foreach (var company in companiesHouseCompanies)
            {
                if (!companyBrandRelationships.Any(rel => rel.CompanyNames.Contains(company)))
                {
                    newCompanies.Add(company.Trim().ToLowerInvariant());
                }
            }

            var shorterList = new List<string>();

            shorterList.AddRange(newCompanies.Take(1000));

            shorterList.Add("Cadbury");

            return shorterList;
        }

        internal static void OutputCandidates(List<Candidate> candidates, string type)
        {
            var file = new StreamWriter("C:/Users/Alice/Desktop/" + type + ".txt");

            foreach (var candidate in candidates)
            {
                if (candidate.KnownBrands != null)
                {
                    file.WriteLine(
                                   "Contains company/brand relationship? " + candidate.CompanyBrandRelationship
                                   + Environment.NewLine + Environment.NewLine + "Page title: " + candidate.PageTitle
                                   + Environment.NewLine + Environment.NewLine + "Known company: "
                                   + candidate.KnownCompanyNames.FirstOrDefault() + Environment.NewLine
                                   + "Known brand: " + candidate.KnownBrand + Environment.NewLine + Environment.NewLine
                                   + "Known brands: " + String.Join(", ", candidate.KnownBrands) + Environment.NewLine + Environment.NewLine
                                   + "Multiple brands present:" + candidate.ContainsMultipleBrands
                                   + Environment.NewLine + "\r Previous Relevant Node: " + candidate.PreviousContent
                                   + Environment.NewLine + "\r Html: " + candidate.CandidateHtml + Environment.NewLine
                                   + Environment.NewLine);
                }
                Console.WriteLine(
                                  candidate.PageTitle + Environment.NewLine + ' '
                                  + candidate.KnownCompanyNames.FirstOrDefault() + Environment.NewLine + ' '
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

        private static HtmlNode GetPreviousRelevantNode(InitialCandidate initialcandidate)
        {
            HtmlNode previousContent = null;
            var previousSibling = initialcandidate.Node.PreviousSibling;
            var parentNode = initialcandidate.Node.ParentNode;
            var grandparentNode = initialcandidate.Node.ParentNode.ParentNode;

            // Check the three preceeding previous siblings
            if (!previousSibling.OuterHtml.IsNullOrEmpty() && previousSibling.OuterHtml != "\n")
            {
                previousContent = previousSibling;
            }
            else
            {
                if (previousSibling.PreviousSibling != null
                    && (!previousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                        && previousSibling.PreviousSibling.OuterHtml != "\n"))
                {
                    previousContent = previousSibling.PreviousSibling;
                }
                else if (previousSibling.PreviousSibling != null
                         && (previousSibling.PreviousSibling.PreviousSibling != null
                             && (!previousSibling.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                 && previousSibling.PreviousSibling.PreviousSibling.OuterHtml != "\n")))
                {
                    previousContent = previousSibling.PreviousSibling.PreviousSibling;
                }
            }

            // Check the three previous siblings of the parent sibling
            if (previousContent == null || previousContent.OuterHtml.IsNullOrEmpty())
            {
                if (parentNode.PreviousSibling != null
                    && (!parentNode.PreviousSibling.OuterHtml.IsNullOrEmpty()
                        && parentNode.PreviousSibling.OuterHtml != "\n"))
                {
                    previousContent = parentNode.PreviousSibling;
                }
                else
                {
                    if (parentNode.PreviousSibling != null
                        && (parentNode.PreviousSibling.PreviousSibling != null
                            && (!parentNode.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                && parentNode.PreviousSibling.PreviousSibling.OuterHtml != "\n")))
                    {
                        previousContent = parentNode.PreviousSibling.PreviousSibling;
                    }
                    else if (parentNode.PreviousSibling != null
                             && (parentNode.PreviousSibling.PreviousSibling != null
                                 && (parentNode.PreviousSibling.PreviousSibling.PreviousSibling != null
                                     && (!parentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml
                                                    .IsNullOrEmpty()
                                         && parentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml != "\n"))))
                    {
                        previousContent = parentNode.PreviousSibling.PreviousSibling.PreviousSibling;
                    }
                }
            }

            // Check the three previous siblings of the grandparent sibling
            if (previousContent == null || previousContent.OuterHtml.IsNullOrEmpty())
            {
                if (grandparentNode.PreviousSibling != null
                    && (!grandparentNode.PreviousSibling.OuterHtml.IsNullOrEmpty()
                        && grandparentNode.PreviousSibling.OuterHtml != "\n"))
                {
                    previousContent = grandparentNode.PreviousSibling;
                }
                else
                {
                    if (grandparentNode.PreviousSibling != null
                        && (grandparentNode.PreviousSibling.PreviousSibling != null
                            && (!grandparentNode.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                && grandparentNode.PreviousSibling.PreviousSibling.OuterHtml != "\n")))
                    {
                        previousContent = grandparentNode.PreviousSibling.PreviousSibling;
                    }
                    else if (grandparentNode.PreviousSibling != null
                             && (grandparentNode.PreviousSibling.PreviousSibling != null
                                 && (grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling != null
                                     && (!grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml
                                                         .IsNullOrEmpty()
                                         && grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml
                                         != "\n"))))
                    {
                        previousContent = grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling;
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