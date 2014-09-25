namespace Slurper
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using CsQuery.ExtensionMethods.Internal;

    using Html;

    using HtmlAgilityPack;

    using Newtonsoft.Json;

    using Slurper.Model;

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
                                                 CompanyName =
                                                         knownCompanyBrandRelationships[i].CompanyName,
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
                var initialCandidateSegments = InitialCandidateSegments(root);

                // For each list or table in the page
                foreach (var initialcandidate in initialCandidateSegments)
                {
                    // For each company mentioned on this page.
                    foreach (var company in companiesMentionedOnPage)
                    {
                        var previousContent = GetPreviousRelevantNode(initialcandidate);

                        var previousContentOuterHtml = previousContent == null
                                                               ? ""
                                                               : previousContent.OuterHtml;

                        var previousContentInnerText = previousContent == null
                                       ? ""
                                       : previousContent.InnerText;

                        previousContentInnerText = Regex.Replace(previousContentInnerText, company, "");

                        // Check that the owner name for the relation is present in the title, domain, list/ table or previous relevant node. This is a necessary but not sufficient condition for creating a candidate
                        // Only use one company name at the moment so a loop isn't necessary

                        var domainOrTitleContainsPotentialOwner = DomainOrTitleContainsPotentialOwner(page, company, title);

                        var candidateHtmlContainsPotentialOwner = CandidateContainsPotentialOwner(initialcandidate, company);

                        var previousContentContainsPotentialOwner = PreviousContentContainsPotentialOwner(previousContentInnerText, company);

                        // If a company name is present in the title, domain, list/ table or previous relevant node, continue to check for brand names
                        if (domainOrTitleContainsPotentialOwner
                            || candidateHtmlContainsPotentialOwner || previousContentContainsPotentialOwner)
                        {
                            var brandsPresentInInitialCandidate = new List<string>();

                            // For each brand 
                            foreach (var brand in brands)
                            {
                                var brandOnItsOwn = new Regex(@"\b" + brand.ToLowerInvariant() + @"\b");
                                if (brandOnItsOwn.IsMatch(initialcandidate.Node.InnerText.ToLowerInvariant())
                                    && company.ToLowerInvariant() != brand.ToLowerInvariant())
                                {
                                    brandsPresentInInitialCandidate.Add(brand);
                                }
                            }

                            if (brandsPresentInInitialCandidate.Count > 0)
                            {
                                var itemsWithBrandsForWholeCandidate = new List<ListOrTableItem>();

                                var allInnerSegments = AllInnerSegments(initialcandidate);

                                var wordsInPreviousContent = new List<string>();

                                if (previousContent != null)
                                {
                                    wordsInPreviousContent = previousContent.InnerText.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                                }

                                // Get any LI or TD element surrounding each brand mention
                                foreach (var brand in brandsPresentInInitialCandidate)
                                {
                                    var innerSegments = new List<HtmlNode>();

                                    var itemsWithBrand = new List<ListOrTableItem>();

                                    innerSegments.AddRange(allInnerSegments.Where(ins => ins.InnerText.ToLowerInvariant().Contains(brand.ToLowerInvariant())));

                                    foreach (var innerSegment in innerSegments)
                                    {
                                        var trimmedText = CleanInnerText(innerSegment);

                                        trimmedText = Regex.Replace(trimmedText, brand, "");

                                        var wordsInInnerSegment =
                                                trimmedText.Split(
                                                                             new char[]
                                                                                 {
                                                                                         '.',
                                                                                         '?',
                                                                                         '!',
                                                                                         ' ',
                                                                                         ';',
                                                                                         ':',
                                                                                         ','
                                                                                 },
                                                                             StringSplitOptions.RemoveEmptyEntries)
                                                            .ToList();

                                        var htmlWithoutBrand = Regex.Replace(innerSegment.OuterHtml, brand, "");

                                        var item = new ListOrTableItem
                                        {
                                            ItemHtml =
                                                    safey.Sanitize(
                                                                   htmlWithoutBrand),
                                            ItemInnerText = trimmedText,
                                            ItemWordCount = wordsInInnerSegment.Count(),
                                            WordsInItem = wordsInInnerSegment,
                                            KnownBrand = brand,
                                            ContainsBrandOnly = innerSegment.InnerText.ToLowerInvariant().Equals(brand.ToLowerInvariant())
                                        };

                                        itemsWithBrand.Add(item);
                                        itemsWithBrandsForWholeCandidate.Add(item);
                                    }

                                    // Create a candidate for each individual list item or table row containing a brand for the relation 
                                    if (itemBrandLevelCandidates)
                                    {
                                        foreach (var listOrTableItemContainingBrand in itemsWithBrand)
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
                                                                                               previousContentInnerText),
                                                                        PreviousContentWordCount = wordsInPreviousContent.Count(),
                                                                        WordsInPreviousContent = wordsInPreviousContent,
                                                                        CandidateHtml = listOrTableItemContainingBrand.ItemHtml,
                                                                        CandidateHtmlWordCount = listOrTableItemContainingBrand.ItemWordCount,
                                                                        WordsInCandidateHtml = listOrTableItemContainingBrand.WordsInItem,
                                                                        KnownCompanyName = company,
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
                                                                                        .Count > 1,
                                                                        ItemsContainBrandOnly = listOrTableItemContainingBrand.ContainsBrandOnly
                                                                };
                                            testCandidates.Add(candidate);
                                        }
                                    }
                                }

                                // Process to create a candidate for each whole list or table and all its brands
                                if (itemBrandLevelCandidates == false)
                                {
                                    var wordsInCandidateHtml = initialcandidate.Node.InnerText.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                                    // Check if every item in the candidate found to contain a brand only contains the brand
                                    bool noOtherTextExceptBrands = !itemsWithBrandsForWholeCandidate.IsNullOrEmpty() && itemsWithBrandsForWholeCandidate.TrueForAll(
                                                                                                  iwb => iwb.ContainsBrandOnly);

                                    bool someItemsHaveNoOtherTextExceptBrand = !itemsWithBrandsForWholeCandidate.IsNullOrEmpty() && itemsWithBrandsForWholeCandidate.Any(
                                                              iwb => iwb.ContainsBrandOnly);

                                    var previousContentWithoutCompany = Regex.Replace(
                                                                                      previousContentOuterHtml,
                                                                                      company,
                                                                                      "");

                                    var candidateHtmlWithoutBrands = initialcandidate.Node.OuterHtml;

                                    foreach (var brand in brandsPresentInInitialCandidate)
                                    {
                                        candidateHtmlWithoutBrands = Regex.Replace(
                                                                                   candidateHtmlWithoutBrands,
                                                                                   brand,
                                                                                   "");
                                    }

                                    // Multiple candidates must contain the company name in either the page title or the previous content section.
                                    if (previousContentContainsPotentialOwner || domainOrTitleContainsPotentialOwner)
                                    {
                                        var candidate = new Candidate
                                        {
                                            IsTableSegment = initialcandidate.Type == "table",
                                            IsListSegment = initialcandidate.Type == "list",
                                            IsItemLevelCandidate = false,
                                            PreviousContent = safey.Sanitize(previousContentWithoutCompany),
                                            PreviousContentWordCount = wordsInPreviousContent.Count(),
                                            WordsInPreviousContent = wordsInPreviousContent,
                                            CandidateHtml =
                                                    safey.Sanitize(
                                                                   candidateHtmlWithoutBrands),

                                            CandidateHtmlWordCount = wordsInCandidateHtml.Count(),
                                            WordsInCandidateHtml = wordsInCandidateHtml,
                                            KnownCompanyName = company,
                                            KnownBrands = brandsPresentInInitialCandidate,
                                            DomainOrPageTitleContainsOwner =
                                                    domainOrTitleContainsPotentialOwner,
                                            PreviousContentContainsPotentialOwner = previousContentContainsPotentialOwner,
                                            CandidateHtmlContainsPotentialOwner = candidateHtmlContainsPotentialOwner,
                                            Uri = page,
                                            PageTitle = title,
                                            ContainsMultipleBrands =
                                                    brandsPresentInInitialCandidate.Count > 1,
                                            ItemsContainBrandOnly = noOtherTextExceptBrands
                                        };

                                        // If each list or table item with a brand contains only a brand name
                                        if (someItemsHaveNoOtherTextExceptBrand)
                                        {
                                            foreach (var segment in allInnerSegments)
                                            {
                                                var cleanedSegment = CleanInnerText(segment);
                                                if (!candidate.KnownBrands.Contains(cleanedSegment))
                                                {
                                                    candidate.KnownBrands.Add(cleanedSegment);
                                                }
                                            }
                                        }

                                        testCandidates.Add(candidate);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return testCandidates;
        }

        private static List<InitialCandidate> InitialCandidateSegments(HtmlNode root)
        {
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
                                                  .Select(list => new InitialCandidate() { Node = list, Type = "list" }));
            return initialCandidateSegments;
        }

        private static List<HtmlNode> AllInnerSegments(InitialCandidate initialcandidate)
        {
            var allInnerSegments = new List<HtmlNode>();

            // Get all the list items and table rows, whether or not they contain a brand
            if (initialcandidate.Type == "list")
            {
                allInnerSegments.AddRange(initialcandidate.Node.Descendants("li").ToList());
            }
            else if (initialcandidate.Type == "table")
            {
                allInnerSegments.AddRange(initialcandidate.Node.Descendants("tr").ToList());
            }
            return allInnerSegments;
        }

        private static bool PreviousContentContainsPotentialOwner(string previousContentInnerText, string company)
        {
            bool previousContentContainsPotentialOwner = previousContentInnerText.ToLowerInvariant().Contains(company.ToLowerInvariant() + " ");
            return previousContentContainsPotentialOwner;
        }

        private static bool CandidateContainsPotentialOwner(InitialCandidate initialcandidate, string company)
        {
            bool candidateHtmlContainsPotentialOwner = initialcandidate.Node.InnerText.ToLowerInvariant().Contains(company.ToLowerInvariant() + " ");
            return candidateHtmlContainsPotentialOwner;
        }

        private static bool DomainOrTitleContainsPotentialOwner(string page, string company, string title)
        {
            bool domainOrTitleContainsPotentialOwner = page.ToLowerInvariant().Contains(company.ToLowerInvariant() + " ")
                                                       || title.ToLowerInvariant().Contains(company.ToLowerInvariant() + " ");
            return domainOrTitleContainsPotentialOwner;
        }

        private static string CleanInnerText(HtmlNode innerSegment)
        {
            var trimmedText = innerSegment.InnerText.Trim();

            const string LineBreaks = "\n";

            trimmedText = Regex.Replace(trimmedText, LineBreaks, "");
            return trimmedText;
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

                var initialCandidateSegments = InitialCandidateSegments(root);

                // For each list or table in the page
                foreach (var initialcandidate in initialCandidateSegments)
                {
                    // For each relation where an owner name is present on this page. Candidates are created at this level as they are a combination of relation + segment.
                    foreach (var relation in relationsWhereOwnerMentionedOnPage)
                    {
                        var previousContent = GetPreviousRelevantNode(initialcandidate);

                        var previousContentInnerText = previousContent == null
                                       ? ""
                                       : previousContent.InnerText;

                        previousContentInnerText = Regex.Replace(previousContentInnerText, relation.CompanyName, "");

                        // Check that the owner name for the relation is present in the title, list/ table or previous relevant node. This is a necessary but not sufficient condition for creating a candidate

                        var domainOrTitleContainsOwner = DomainOrTitleContainsPotentialOwner(page, relation.CompanyName, title);

                        var candidateHtmlContainsPotentialOwner = CandidateContainsPotentialOwner(initialcandidate, relation.CompanyName);

                        var previousContentContainsPotentialOwner = PreviousContentContainsPotentialOwner(previousContentInnerText, relation.CompanyName);
                        

                        // If the company name for the relation is present in the title, domain, list/ table or previous relevant node, continue to check for brand names
                        if (domainOrTitleContainsOwner || candidateHtmlContainsPotentialOwner || previousContentContainsPotentialOwner)
                        {
                            var knownBrandsPresent = new List<string>();

                            // For each brand owned by the company, for this relation
                            foreach (var brand in relation.BrandNames)
                            {
                                var brandOnItsOwn = new Regex(@"\b" + brand.ToLowerInvariant() + @"\b");
                                if (
                                        brandOnItsOwn.IsMatch(initialcandidate.Node.InnerText.ToLowerInvariant()) && relation.CompanyName.ToLowerInvariant() != brand.ToLowerInvariant())
                                {
                                    knownBrandsPresent.Add(brand);
                                }
                            }

                            if (knownBrandsPresent.Count > 0)
                            {
                                var itemsWithBrandsForWholeCandidate = new List<ListOrTableItem>();

                                var wordsInPreviousContent = new List<string>();

                                if (previousContent != null)
                                {
                                    wordsInPreviousContent = previousContent.InnerText.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                                }


                           // Get any LI or TD element surrounding each brand mention
                                    foreach (var brand in knownBrandsPresent)
                                    {
                                        var itemsWithBrand = new List<ListOrTableItem>();

                                        var innerSegments = InnerSegmentsWithBrand(initialcandidate, brand);

                                        foreach (var innerSegment in innerSegments)
                                    {
                                        var trimmedText = innerSegment.InnerText.Trim();

                                        const string LineBreaks = "\n";

                                        trimmedText = Regex.Replace(trimmedText, LineBreaks, "");

                                        trimmedText = Regex.Replace(trimmedText, brand, "");

                                        var wordsInInnerSegment =
                                                trimmedText.Split(
                                                                             new char[]
                                                                                 {
                                                                                         '.',
                                                                                         '?',
                                                                                         '!',
                                                                                         ' ',
                                                                                         ';',
                                                                                         ':',
                                                                                         ','
                                                                                 },
                                                                             StringSplitOptions.RemoveEmptyEntries)
                                                            .ToList();

                                        var htmlWithoutBrand = Regex.Replace(innerSegment.OuterHtml, brand, "");

                                        var itemWithBrand = new ListOrTableItem
                                        {
                                            ItemHtml =
                                                    safey.Sanitize(
                                                                   htmlWithoutBrand),
                                            ItemWordCount = wordsInInnerSegment.Count(),
                                            WordsInItem = wordsInInnerSegment,
                                            KnownBrand = brand,
                                            ContainsBrandOnly = innerSegment.InnerText.ToLowerInvariant().Equals(brand.ToLowerInvariant())
                                        };

                                        itemsWithBrand.Add(itemWithBrand);
                                        itemsWithBrandsForWholeCandidate.Add(itemWithBrand);
                                    }
                                         //Process to create item level candidates
                                        if (itemBrandLevelCandidates)
                                        {
                                            foreach (var listOrTableItemContainingBrand in itemsWithBrand)
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
                                                                                               previousContentInnerText),
                                                                        WordsInPreviousContent = wordsInPreviousContent.ToList(),
                                                                        PreviousContentWordCount = wordsInPreviousContent.Count(),
                                                                        CandidateHtml = listOrTableItemContainingBrand.ItemHtml,
                                                                        WordsInCandidateHtml = listOrTableItemContainingBrand.WordsInItem,
                                                                        CandidateHtmlWordCount = listOrTableItemContainingBrand.WordsInItem.Count,
                                                                        KnownCompanyName =
                                                                                relation.CompanyName,
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
                                                                                positiveCandidates,
                                                                        ItemsContainBrandOnly = listOrTableItemContainingBrand.ContainsBrandOnly
                                                                };
                                                candidates.Add(candidate);
                                            }
                                        }
                                    }
                                
                                    // Process to create a candidate for each whole list or table and all its brands
                                if (itemBrandLevelCandidates == false)
                                {
                                    var wordsInCandidateHtml = initialcandidate.Node.InnerText.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);

                                    // Check if every item in the candidate found to contain a brand only contains the brand
                                    bool brandsOnly = itemsWithBrandsForWholeCandidate.TrueForAll(
                                                                                                  iwb => iwb.ContainsBrandOnly);

                                    var previousContentWithoutCompany = Regex.Replace(
                                                  previousContentInnerText,
                                                  relation.CompanyName,
                                                  "");

                                    var candidateHtmlWithoutBrands = initialcandidate.Node.OuterHtml;

                                    foreach (var brand in knownBrandsPresent)
                                    {
                                        candidateHtmlWithoutBrands = Regex.Replace(
                                                                                   candidateHtmlWithoutBrands,
                                                                                   brand,
                                                                                   "");
                                    }

                                    // The owner must be present in the previous content or page for multiple candidates
                                    if (previousContentContainsPotentialOwner || domainOrTitleContainsOwner)
                                    {
                                        var candidate = new Candidate
                                        {
                                            IsTableSegment = initialcandidate.Type == "table",
                                            IsListSegment = initialcandidate.Type == "list",
                                            IsItemLevelCandidate = false,
                                            PreviousContent = safey.Sanitize(previousContentWithoutCompany),
                                            WordsInPreviousContent = wordsInPreviousContent.ToList(),
                                            PreviousContentWordCount = wordsInPreviousContent.Count(),
                                            CandidateHtml =
                                                    safey.Sanitize(
                                                                   candidateHtmlWithoutBrands),
                                            WordsInCandidateHtml = wordsInCandidateHtml.ToList(),
                                            CandidateHtmlWordCount = wordsInCandidateHtml.Count(),
                                            KnownCompanyName = relation.CompanyName,
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
                                            CompanyBrandRelationship = positiveCandidates,
                                            ItemsContainBrandOnly = brandsOnly,
                                        };
                                        candidates.Add(candidate);                                        
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return candidates;
        }

        private static List<HtmlNode> InnerSegmentsWithBrand(InitialCandidate initialcandidate, string brand)
        {
            var innerSegments = new List<HtmlNode>();

            if (initialcandidate.Type == "list")
            {
                innerSegments.AddRange(
                                       initialcandidate.Node.Descendants("li")
                                                       .ToList()
                                                       .Where(
                                                              listItem =>
                                                              listItem.InnerText.ToLowerInvariant()
                                                                      .Contains(brand.ToLowerInvariant())));
            }
            else if (initialcandidate.Type == "table")
            {
                innerSegments.AddRange(
                                       initialcandidate.Node.Descendants("tr")
                                                       .ToList()
                                                       .Where(
                                                              listItem =>
                                                              listItem.InnerText.ToLowerInvariant()
                                                                      .Contains(brand.ToLowerInvariant())));
            }
            return innerSegments;
        }

        internal static IEnumerable<string> GetPages(string path)
        {
            return Directory.GetFiles(path, "*.htm*", SearchOption.AllDirectories).Where(f => !f.Contains("Image~") && !f.Contains("Talk~")); 
        }

        internal static List<String> GetTestBrands(List<CompanyAndBrands> companyBrandRelationships)
        {
            var amazonBrands = new List<String>();

            var directory = new DirectoryInfo("C:/Users/Alice/Desktop/AmazonBrands");

            foreach (var file in directory.GetFiles())
            {
                amazonBrands.AddRange(File.ReadLines(file.FullName));
            }

            var uniqueBrands = amazonBrands.Distinct().ToList();

            var newBrands = new List<String>();

            foreach (var brand in uniqueBrands)
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

            var troublesome2LetterBrands = new List<String> { "ee", "et", "ry", "ss", "up", "ls", "oe", "se", "od", "tr", "ns", "ge", "ms", "ic", "am", "ge", "ns", "pa", "ips", "ps", "ub", "at" };
            var brandStopList = new List<String> { "bury", "ting", "may", "div", "chocolate", "fruit", "green", "mini", "his", "none", "food", "sen", "ips", "premium", "ion", "ist", "ngs", "ams", "none", "ist", "rts", "book", "official", "entity", "ref", "image", "border", "icon", "max", "bold", "default", "amp", "august", "rft", "non", "various", "ips", "computer", "other", "generic", "block", "m.c" };

            foreach (var stopListBrand in brandStopList)
            {
                newBrands.RemoveAll(nb => nb.Equals(stopListBrand));
            }

            return newBrands;
        }

        internal static List<String> GetTestCompanies(List<CompanyAndBrands> companyBrandRelationships)
        {
            var companies = FreeBaseHelpers.GetKnownCompaniesFromFreeBaseBusinessOperations();

            //var companies = FreeBaseHelpers.GetKnownCompaniesFromFreeBaseConsumerCompanies();

            //var directory = new DirectoryInfo("C:/Users/Alice/Desktop/Companies");

            //foreach (var file in directory.GetFiles())
            //{
            //    companies.AddRange(File.ReadLines(file.FullName));
            //}

            var newCompanies = new List<String>();

            foreach (var company in companies)
            {
                if (!companyBrandRelationships.Any(rel => rel.CompanyName.Equals(company)))
                {
                    newCompanies.Add(company.Trim().ToLowerInvariant());
                }
            }

            var shorterList = new List<string>();

            shorterList.AddRange(newCompanies.Take(1000));

            shorterList.Add("Cadbury");

            return shorterList;
        }

        internal static void OutputJsonResults(List<Candidate> candidates)
        {
            var jsonFile = new StreamWriter("C:/Users/Alice/Desktop/results.js");

            var classifiedRelations = new ClassifiedRelationsResponse { Relations = new List<ClassifiedRelation>() };

            foreach (var candidate in candidates)
            {
                if (candidate.KnownBrands != null)
                {
                    ClassifiedRelation classifiedRelation = MapCandidateToClassifiedRelation(candidate);

                    classifiedRelations.Relations.Add(classifiedRelation);
                }
            }
            string json = JsonConvert.SerializeObject(classifiedRelations);

            jsonFile.WriteLine(json);

            jsonFile.Close();
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
                                   + candidate.KnownCompanyName + Environment.NewLine
                                   + "Known brand: " + candidate.KnownBrand + Environment.NewLine + Environment.NewLine
                                   + "Known brands: " + String.Join(", ", candidate.KnownBrands) + Environment.NewLine + Environment.NewLine
                                   + "Multiple brands present:" + candidate.ContainsMultipleBrands
                                   + Environment.NewLine + "\r Previous Relevant Node: " + candidate.PreviousContent
                                   + Environment.NewLine + "\r Html: " + candidate.CandidateHtml + Environment.NewLine
                                   + Environment.NewLine);
                }
                Console.WriteLine(
                                  candidate.PageTitle + Environment.NewLine + ' '
                                  + candidate.KnownCompanyName + Environment.NewLine + ' '
                                  + candidate.CandidateHtml + Environment.NewLine);
            }

            file.Close();

            Console.WriteLine(candidates.Count.ToString());
        }

        private static ClassifiedRelation MapCandidateToClassifiedRelation(Candidate candidate)
        {
            var classifiedRelation = new ClassifiedRelation();
            classifiedRelation.IsRelation = candidate.CompanyBrandRelationship;
            classifiedRelation.Company = candidate.KnownCompanyName;
            classifiedRelation.Brands = new List<string>(candidate.KnownBrands);
            classifiedRelation.Brand = candidate.KnownBrand;
            classifiedRelation.Source = new RelationSource
                                            {
                                                Url = candidate.Uri,
                                                Text = candidate.PreviousContent + candidate.CandidateHtml,
                                                Date = DateTime.Now
                                            };
            return classifiedRelation;
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
            if (previousSibling != null && !previousSibling.InnerText.IsNullOrEmpty() && previousSibling.InnerText != "\n")
            {
                previousContent = previousSibling;
            }
            else
            {
                if (previousSibling != null && (previousSibling.PreviousSibling != null
                                                && (!previousSibling.PreviousSibling.InnerText.IsNullOrEmpty()
                                                    && previousSibling.PreviousSibling.InnerText != "\n")))
                {
                    previousContent = previousSibling.PreviousSibling;
                }
                else if (previousSibling != null && (previousSibling.PreviousSibling != null
                                                     && (previousSibling.PreviousSibling.PreviousSibling != null
                                                         && (!previousSibling.PreviousSibling.PreviousSibling.InnerText.IsNullOrEmpty()
                                                             && previousSibling.PreviousSibling.PreviousSibling.InnerText != "\n"))))
                {
                    previousContent = previousSibling.PreviousSibling.PreviousSibling;
                }
            }

            // Check the three previous siblings of the parent sibling
            if (previousContent == null)
            {
                if (parentNode.PreviousSibling != null
                    && (!parentNode.PreviousSibling.InnerText.IsNullOrEmpty()
                        && parentNode.PreviousSibling.InnerText != "\n"))
                {
                    previousContent = parentNode.PreviousSibling;
                }
                else
                {
                    if (parentNode.PreviousSibling != null
                        && (parentNode.PreviousSibling.PreviousSibling != null
                            && (!parentNode.PreviousSibling.PreviousSibling.InnerText.IsNullOrEmpty()
                                && parentNode.PreviousSibling.PreviousSibling.InnerText != "\n")))
                    {
                        previousContent = parentNode.PreviousSibling.PreviousSibling;
                    }
                    else if (parentNode.PreviousSibling != null
                             && (parentNode.PreviousSibling.PreviousSibling != null
                                 && (parentNode.PreviousSibling.PreviousSibling.PreviousSibling != null
                                     && (!parentNode.PreviousSibling.PreviousSibling.PreviousSibling.InnerText
                                                    .IsNullOrEmpty()
                                         && parentNode.PreviousSibling.PreviousSibling.PreviousSibling.InnerText != "\n"))))
                    {
                        previousContent = parentNode.PreviousSibling.PreviousSibling.PreviousSibling;
                    }
                }
            }

            // Check the three previous siblings of the grandparent sibling
            if (previousContent == null)
            {
                if (grandparentNode.PreviousSibling != null
                    && (!grandparentNode.PreviousSibling.InnerText.IsNullOrEmpty()
                        && grandparentNode.PreviousSibling.InnerText != "\n"))
                {
                    previousContent = grandparentNode.PreviousSibling;
                }
                else
                {
                    if (grandparentNode.PreviousSibling != null
                        && (grandparentNode.PreviousSibling.PreviousSibling != null
                            && (!grandparentNode.PreviousSibling.PreviousSibling.InnerText.IsNullOrEmpty()
                                && grandparentNode.PreviousSibling.PreviousSibling.InnerText != "\n")))
                    {
                        previousContent = grandparentNode.PreviousSibling.PreviousSibling;
                    }
                    else if (grandparentNode.PreviousSibling != null
                             && (grandparentNode.PreviousSibling.PreviousSibling != null
                                 && (grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling != null
                                     && (!grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.InnerText
                                                         .IsNullOrEmpty()
                                         && grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.InnerText
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

                if (root.OuterHtml.ToLowerInvariant().Contains(relation.CompanyName.ToLowerInvariant() + " ")
                    || page.ToLowerInvariant().Contains(relation.CompanyName.ToLowerInvariant() + " "))
                {
                    relationsWhereOwnerMentionedOnPage.Add(relation);
                }
            }
            return relationsWhereOwnerMentionedOnPage;
        }
    }
}