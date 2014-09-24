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

                        var previousContentInnerText = previousContent == null
                                       ? ""
                                       : previousContent.InnerText;

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
                        if (previousContentInnerText.ToLowerInvariant().Contains(company.ToLowerInvariant() + " "))
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
                                        brandOnItsOwn.IsMatch(initialcandidate.Node.InnerText.ToLowerInvariant()) && company.ToLowerInvariant() != brand.ToLowerInvariant())
                                {
                                    brandsPresentInInitialCandidate.Add(brand);
                                }

                            }

                            if (brandsPresentInInitialCandidate.Count > 0)
                            {
                                var itemsWithBrandsForWholeCandidate = new List<ListOrTableItem>();

                                var allInnerSegments = new List<HtmlNode>();

                                // Get all the list items and table rows, whether or not they contain a brand
                                if (initialcandidate.Type == "list")
                                {
                                        allInnerSegments.AddRange(
                                                               initialcandidate.Node.Descendants("li")
                                                                               .ToList());
                                }
                                else if (initialcandidate.Type == "table")
                                {
                                        allInnerSegments.AddRange(
                                                               initialcandidate.Node.Descendants("tr")
                                                                               .ToList());
                                }

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

                                        var item = new ListOrTableItem
                                        {
                                            ItemHtml =
                                                    safey.Sanitize(
                                                                   innerSegment
                                                                           .OuterHtml),
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
                                                                                               previousContentOuterHtml),
                                                                        PreviousContentWordCount = wordsInPreviousContent.Count(),
                                                                        WordsInPreviousContent = wordsInPreviousContent,
                                                                        CandidateHtml = listOrTableItemContainingBrand.ItemHtml,
                                                                        CandidateHtmlWordCount = listOrTableItemContainingBrand.ItemWordCount,
                                                                        WordsInCandidateHtml = listOrTableItemContainingBrand.WordsInItem,
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
                                                                                        .Count > 1,
                                                                        ItemsContainBrandOnly = listOrTableItemContainingBrand.ContainsBrandOnly
                                                                };
                                            candidate.KnownCompanyNames.Add(company);
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

                                    // Multiple candidates must contain the company name in either the page title or the previous content section.
                                    if (previousContentContainsPotentialOwner || domainOrTitleContainsPotentialOwner)
                                    {
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

                                        candidate.KnownCompanyNames.Add(company);
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

                        var previousContentInnerText = previousContent == null
                                       ? ""
                                       : previousContent.InnerText;

                        // Check that the owner name for the relation is present in the title, list/ table or previous relevant node. This is a necessary but not sufficient condition for creating a candidate
                        // Only use one company name at the moment so a loop isn't necessary
                        foreach (var ownerSynonym in relation.CompanyNames)
                        {
                            if (page.ToLowerInvariant().Contains(ownerSynonym.ToLowerInvariant() + " ")
                                || title.ToLowerInvariant().Contains(ownerSynonym.ToLowerInvariant() + " "))
                            {
                                domainOrTitleContainsOwner = true;
                            }
                            if (
                                    initialcandidate.Node.InnerText.ToLowerInvariant()
                                                    .Contains(ownerSynonym.ToLowerInvariant() + " "))
                            {
                                candidateHtmlContainsPotentialOwner = true;
                            }
                            if (
                                    previousContentInnerText.ToLowerInvariant()
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
                                var namedEntitiesInCandidate = new List<string>();

                                

                                var brandOnItsOwn = new Regex(@"\b" + brand.ToLowerInvariant() + @"\b");
                                if (
                                        brandOnItsOwn.IsMatch(initialcandidate.Node.InnerText.ToLowerInvariant()) && relation.CompanyNames.FirstOrDefault().ToLowerInvariant() != brand.ToLowerInvariant())
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
                                        var innerSegments = new List<HtmlNode>();

                                        var itemsWithBrand = new List<ListOrTableItem>();

                                        if (initialcandidate.Type == "list")
                                        {
                                            innerSegments.AddRange(
                                                                   initialcandidate.Node.Descendants("li")
                                                                                   .ToList()
                                                                                   .Where(
                                                                                          listItem =>
                                                                                          listItem.InnerText
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
                                                                                          listItem.InnerText
                                                                                                  .ToLowerInvariant()
                                                                                                  .Contains(
                                                                                                            brand
                                                                                                                    .ToLowerInvariant
                                                                                                                    ())));
                                        }

                                    foreach (var innerSegment in innerSegments)
                                    {
                                        var trimmedText = innerSegment.InnerText.Trim();

                                        const string LineBreaks = "\n";

                                        Regex.Replace(trimmedText, LineBreaks, "");

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

                                        var itemWithBrand = new ListOrTableItem
                                        {
                                            ItemHtml =
                                                    safey.Sanitize(
                                                                   innerSegment
                                                                           .OuterHtml),
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

                                    // The owner must be present in the previous content or page for multiple candidates
                                    if (previousContentContainsPotentialOwner || domainOrTitleContainsOwner)
                                    {
                                        var candidate = new Candidate
                                        {
                                            IsTableSegment = initialcandidate.Type == "table",
                                            IsListSegment = initialcandidate.Type == "list",
                                            IsItemLevelCandidate = false,
                                            PreviousContent = safey.Sanitize(previousContentInnerText),
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
            var brandStopList = new List<String> { "bury", "ting", "may", "div", "chocolate", "fruit", "green", "mini", "his", "none", "food", "sen", "ips", "premium", "ion", "ist", "ngs", "ams", "none", "ist", "rts", "book", "official", "entity", "ref", "image", "border", "icon", "max", "bold", "default", "amp", "august", "rft", "non", "various", "ips" };

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