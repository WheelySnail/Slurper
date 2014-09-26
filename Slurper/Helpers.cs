namespace Slurper
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using com.sun.org.apache.bcel.@internal.generic;
    using com.sun.org.apache.xpath.@internal.operations;

    using CsQuery.ExtensionMethods.Internal;

    using edu.stanford.nlp.ie.crf;
    using edu.stanford.nlp.parser.lexparser;
    using edu.stanford.nlp.tagger.maxent;

    using Html;

    using HtmlAgilityPack;

    using Newtonsoft.Json;

    using Slurper.Model;

    using String = System.String;

    #endregion

    internal class Helpers
    {
        public static List<CompanyAndBrands> CreateKnownCompanyBrandNonRelationships(
                List<CompanyAndBrands> knownCompanyBrandRelationships)
        {
            var nonRelationships = new List<CompanyAndBrands>();

            // Create list of all brand names
            var allBrands = new List<string>();

            foreach (var knownCompanyBrandRelationship in knownCompanyBrandRelationships)
            {
                allBrands.AddRange(knownCompanyBrandRelationship.BrandNames);
            }

            // Remove certain items where there's a real overlap in ownership between two companies, which could lead to incorrect negative examples
            allBrands.RemoveAll(nr => nr == "Ambi Pur" || nr == "Dolce Gusto" || nr == "Persil" || nr == "sunlight");

            // For each entry, make new local copy of the list
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

            // Remove certain items where there's a real overlap in ownership between two companies, which could lead to incorrect negative examples
            nonRelationships.RemoveAll(nr => nr.CompanyName == "Cadbury" || nr.CompanyName == "Kraft Foods");

            return nonRelationships;
        }

        public static List<CompanyAndBrands> FilterKnownCompanyBrandRelationshipsForMultipleBrandRelationshipsOnly(
                List<CompanyAndBrands> knownCandidates)
        {
            // TODO combine candidates where the company name is the same
            return knownCandidates.Where(r => r.BrandNames.Count() > 1).ToList();
        }

        public static List<Candidate> GetTestCandidatesFromPages(IEnumerable<string> pages, List<string> companies, List<string> brands, bool itemBrandLevelCandidates, CRFClassifier classifier, MaxentTagger tagger)
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

                        var nearestHeadingAbove = "";

                        if (root.Descendants("h1").Any() || root.Descendants("h2").Any())
                        {
                            nearestHeadingAbove = GetNearestHeadingAbove(initialcandidate);
                        }

                        var previousContentOuterHtml = previousContent == null
                                                               ? ""
                                                               : previousContent.OuterHtml;
                        
                        var previousContentWithoutCompany = Regex.Replace(previousContentOuterHtml,
                                                                                      company,
                                                                                      "");

                        var previousContentInnerText = previousContent == null
                                       ? ""
                                       : previousContent.InnerText;

                        var previousContentInnerTextWithoutEntity = Regex.Replace(previousContentInnerText, company, "");


                        var domainOrTitleContainsPotentialOwner = DomainOrTitleContainsPotentialOwner(page, company, title);

                        var candidateHtmlContainsPotentialOwner = CandidateContainsPotentialOwner(initialcandidate, company);

                        var previousContentContainsPotentialOwner = PreviousContentContainsPotentialOwner(previousContentInnerText, company);

                        var nearestHeadingIndicatesNoRelation =
                                nearestHeadingAbove.ToLowerInvariant().Contains("external links") || nearestHeadingAbove.ToLowerInvariant().Contains("references") || nearestHeadingAbove.ToLowerInvariant().Contains("by appointment to") || nearestHeadingAbove.ToLowerInvariant().Contains("further reading");

                        var previousContentIndicatesNoRelation =
        previousContentInnerText.ToLowerInvariant().Contains("external links") || previousContentInnerText.ToLowerInvariant().Contains("references") || previousContentInnerText.ToLowerInvariant().Contains("by appointment to") || previousContentInnerText.ToLowerInvariant().Contains("further reading");

                        var nerTaggedInnerText = classifier.classifyToString(initialcandidate.Node.InnerText);

                        var numberOfPeople = GetNumberOfPersonNames(nerTaggedInnerText);

                        var numberOfOrganisations = GetNumberOfOrganisationNames(nerTaggedInnerText);

                        var numberOfLocations = GetNumberOfLocations(nerTaggedInnerText);

                        var numberOfLanguageNames = NumberOfItemsContainingALanguageName(initialcandidate);

                        // If a company name is present in the title, domain, list/ table or previous relevant node, continue to check for brand names
                        if (!nearestHeadingIndicatesNoRelation && (!previousContentIndicatesNoRelation && (numberOfLanguageNames < 5 && (domainOrTitleContainsPotentialOwner
                            || candidateHtmlContainsPotentialOwner || previousContentContainsPotentialOwner))))
                        {
                            var captions = initialcandidate.Node.SelectNodes("caption");

                            var captionsContainOwner = captions != null && captions.Any(c => c.InnerText.Contains(company));
                            
                            var brandsPresentInInitialCandidate = new List<string>();

                            // For each brand 
                            foreach (var brand in brands)
                            {
                                var brandOnItsOwn = new Regex(@"\b" + brand.ToLowerInvariant() + @"\b");
                                if (brandOnItsOwn.IsMatch(initialcandidate.Node.InnerText.ToLowerInvariant())
                                    && company.ToLowerInvariant() != brand.ToLowerInvariant())
                                {
                                    if (ContainsNounPhrase(brand, tagger))
                                    {
                                        var nounPhrasesInCandidate =
                                                GetFullNounPhrasesFromString(initialcandidate.Node.InnerText, tagger);
                                        if (nounPhrasesInCandidate.Count > 0)
                                        {
                                            foreach (var nounPhrase in nounPhrasesInCandidate)
                                            {
                                                if (brand.ToLowerInvariant() == nounPhrase.ToLowerInvariant())
                                                {
                                                    brandsPresentInInitialCandidate.Add(brand);
                                                } 
                                            }
                                        }
                                    }

                                }
                            }

                            brandsPresentInInitialCandidate = brandsPresentInInitialCandidate.Distinct().ToList();

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

                                    // Make sure inner segment length is less than 300 chars as heuristic against layout tables
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
                                                                   innerSegment.OuterHtml),
                                            ItemHtmlWithoutBrand =
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
                                                                        PreviousContent = safey.Sanitize(previousContentOuterHtml),
                                                                        PreviousContentWithoutCandidateEntities =
                                                                                safey.Sanitize(
                                                                                               previousContentWithoutCompany),
                                                                        PreviousContentWordCount = wordsInPreviousContent.Count(),
                                                                        WordsInPreviousContent = wordsInPreviousContent,
                                                                        CandidateHtml = listOrTableItemContainingBrand.ItemHtml,
                                                                        CandidateHtmlWithoutCandidateEntities = listOrTableItemContainingBrand.ItemHtmlWithoutBrand,
                                                                        CandidateHtmlWordCount = listOrTableItemContainingBrand.ItemWordCount,
                                                                        WordsInCandidateHtml = listOrTableItemContainingBrand.WordsInItem,
                                                                        NearestHeadingAbove = nearestHeadingAbove,
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
                                                                        ItemsContainBrandOnly = listOrTableItemContainingBrand.ContainsBrandOnly,
                                                                        CaptionsContainOwner = captionsContainOwner,
                                                                        NumberOfItemsWithLanguages = numberOfLanguageNames,
                                                                        NumberOfLocationNames = numberOfLocations,
                                                                        NumberOfOrganisationNames = numberOfOrganisations,
                                                                        NumberOfPersonNames = numberOfPeople
                                                                };
                                            candidate.MapWordsToWordFeatures();
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
                                            PreviousContent =  safey.Sanitize(previousContentOuterHtml),
                                            PreviousContentWithoutCandidateEntities = safey.Sanitize(previousContentWithoutCompany),
                                            PreviousContentWordCount = wordsInPreviousContent.Count(),
                                            WordsInPreviousContent = wordsInPreviousContent,
                                            CandidateHtml = safey.Sanitize(initialcandidate.Node.OuterHtml),
                                            CandidateHtmlWithoutCandidateEntities =
                                                    safey.Sanitize(
                                                                   candidateHtmlWithoutBrands),

                                            CandidateHtmlWordCount = wordsInCandidateHtml.Count(),
                                            WordsInCandidateHtml = wordsInCandidateHtml,
                                            NearestHeadingAbove = nearestHeadingAbove,
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
                                            ItemsContainBrandOnly = noOtherTextExceptBrands,
                                            CaptionsContainOwner = captionsContainOwner,
                                            NumberOfItemsWithLanguages = numberOfLanguageNames,
                                            NumberOfLocationNames = numberOfLocations,
                                            NumberOfOrganisationNames = numberOfOrganisations,
                                            NumberOfPersonNames = numberOfPeople
                                        };

                                        // If each list or table item with a brand contains only a brand name
                                        if (someItemsHaveNoOtherTextExceptBrand)
                                        {
                                            foreach (var segment in allInnerSegments)
                                            {
                                                var cleanedSegment = CleanInnerText(segment);
                                                if (!candidate.KnownBrands.Contains(cleanedSegment) && ContainsNounPhrase(cleanedSegment, tagger))
                                                {
                                                    candidate.KnownBrands.Add(cleanedSegment);
                                                }
                                            }
                                        }
                                        candidate.MapWordsToWordFeatures();
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

        private static string GetNearestHeadingAbove(InitialCandidate initialcandidate)
        {
            var elementToCheck = initialcandidate.Node;

            var totalToCheck = 16;

            while ((totalToCheck > 0))
            {
                if ((elementToCheck.OuterHtml.Contains("<h1>") || elementToCheck.OuterHtml.Contains("<h2>")) && elementToCheck.InnerText.Length < 1000)
                {
                    return elementToCheck.InnerText;
                }
                else
                {
                    if (totalToCheck == 12 || totalToCheck == 8 || totalToCheck == 4)
                    {
                        if (elementToCheck.ParentNode != null && elementToCheck.ParentNode.PreviousSibling != null)
                        {
                            elementToCheck = elementToCheck.ParentNode.PreviousSibling;
                        }
                    }
                    else
                    {
                        if (elementToCheck.PreviousSibling != null)
                        {
                            elementToCheck = elementToCheck.PreviousSibling;
                        }
                    }
                }
                totalToCheck--;
            }
            return "";
        }

        private static List<InitialCandidate> InitialCandidateSegments(HtmlNode root)
        {
            var initialCandidateSegments =
                    root.Descendants("table")
                        .ToList()
                        .Where(table => !table.OuterHtml.Contains("<ul>") && !table.ChildNodes.Any(cn => cn.InnerText.Length > 300) && !(table.Descendants("tr").ToList().Count > 100))
                        .Select(table => new InitialCandidate() { Node = table, Type = "table" })
                        .ToList();

            initialCandidateSegments.AddRange(
                                              root.Descendants("ul")
                                                  .ToList()
                                                  .Where(list => !list.InnerHtml.Contains("<ul>") && !list.ChildNodes.Any(cn => cn.InnerText.Length > 300) && !(list.Descendants("li").ToList().Count > 100))
                                                  .Select(list => new InitialCandidate() { Node = list, Type = "list" }));
            // Length check as a heuristic to prevent uptake of entire pages using a table as layout
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

        public static List<Candidate> GetTrainingCandidatesFromPages(IEnumerable<string> pages, List<CompanyAndBrands> knownCompanyAndBrandsRelationships, bool itemBrandLevelCandidates, bool positiveCandidates, CRFClassifier classifier, MaxentTagger tagger)
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

                        var previousContentOuterHtml = previousContent == null
                                       ? ""
                                       : previousContent.InnerText;

                        var previousContentWithoutCompany = Regex.Replace(previousContentOuterHtml, relation.CompanyName, "");
                        
                        var previousContentInnerText = previousContent == null
                                       ? ""
                                       : previousContent.InnerText;

                        previousContentInnerText = Regex.Replace(previousContentInnerText, relation.CompanyName, "");

                        var nearestHeadingAbove = "";

                        if (root.Descendants("h1").Any() || root.Descendants("h2").Any())
                        {
                            nearestHeadingAbove = GetNearestHeadingAbove(initialcandidate);
                        }

                        var domainOrTitleContainsOwner = DomainOrTitleContainsPotentialOwner(page, relation.CompanyName, title);

                        var candidateHtmlContainsPotentialOwner = CandidateContainsPotentialOwner(initialcandidate, relation.CompanyName);

                        var previousContentContainsPotentialOwner = PreviousContentContainsPotentialOwner(previousContentInnerText, relation.CompanyName);

                        var nerTaggedInnerText = classifier.classifyToString(initialcandidate.Node.InnerText);

                        var numberOfPeople = GetNumberOfPersonNames(nerTaggedInnerText);

                        var numberOfOrganisations = GetNumberOfOrganisationNames(nerTaggedInnerText);

                        var numberOfLocations = GetNumberOfLocations(nerTaggedInnerText);

                        var numberOfLanguageNames = NumberOfItemsContainingALanguageName(initialcandidate);

                        // If the company name for the relation is present in the title, domain, list/ table or previous relevant node, continue to check for brand names
                        if (domainOrTitleContainsOwner || candidateHtmlContainsPotentialOwner || previousContentContainsPotentialOwner)
                        {
                            var knownBrandsPresent = new List<string>();

                            var captions = initialcandidate.Node.SelectNodes("caption");

                            var captionsContainOwner = captions != null && captions.Any(c => c.InnerText.Contains(relation.CompanyName));

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
                                            ItemHtml = safey.Sanitize(innerSegment.OuterHtml),
                                            ItemHtmlWithoutBrand = safey.Sanitize(htmlWithoutBrand),
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
                                                                        PreviousContent = safey.Sanitize(previousContentOuterHtml),
                                                                        PreviousContentWithoutCandidateEntities = safey.Sanitize(previousContentWithoutCompany),
                                                                        WordsInPreviousContent = wordsInPreviousContent.ToList(),
                                                                        PreviousContentWordCount = wordsInPreviousContent.Count(),
                                                                        CandidateHtml = listOrTableItemContainingBrand.ItemHtml,
                                                                        CandidateHtmlWithoutCandidateEntities = listOrTableItemContainingBrand.ItemHtmlWithoutBrand,
                                                                        WordsInCandidateHtml = listOrTableItemContainingBrand.WordsInItem,
                                                                        CandidateHtmlWordCount = listOrTableItemContainingBrand.WordsInItem.Count,
                                                                        NearestHeadingAbove = nearestHeadingAbove,
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
                                                                        ItemsContainBrandOnly = listOrTableItemContainingBrand.ContainsBrandOnly,
                                                                        CaptionsContainOwner = captionsContainOwner,
                                                                        NumberOfItemsWithLanguages = numberOfLanguageNames,
                                                                        NumberOfLocationNames = numberOfLocations,
                                                                        NumberOfOrganisationNames = numberOfOrganisations,
                                                                        NumberOfPersonNames = numberOfPeople
                                                                };
                                                candidate.MapWordsToWordFeatures();
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
                                            PreviousContent = safey.Sanitize(previousContentOuterHtml),
                                            PreviousContentWithoutCandidateEntities = safey.Sanitize(previousContentWithoutCompany),
                                            WordsInPreviousContent = wordsInPreviousContent.ToList(),
                                            PreviousContentWordCount = wordsInPreviousContent.Count(),
                                            CandidateHtml = safey.Sanitize(initialcandidate.Node.OuterHtml),
                                            CandidateHtmlWithoutCandidateEntities =
                                                    safey.Sanitize(
                                                                   candidateHtmlWithoutBrands),
                                            WordsInCandidateHtml = wordsInCandidateHtml.ToList(),
                                            CandidateHtmlWordCount = wordsInCandidateHtml.Count(),
                                            NearestHeadingAbove = nearestHeadingAbove,
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
                                            CaptionsContainOwner = captionsContainOwner,
                                            NumberOfItemsWithLanguages = numberOfLanguageNames,
                                            NumberOfLocationNames = numberOfLocations,
                                            NumberOfOrganisationNames = numberOfOrganisations,
                                            NumberOfPersonNames = numberOfPeople
                                        };
                                        candidate.MapWordsToWordFeatures();
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

        private static int GetNumberOfLocations(string nerTaggedInnerText)
        {
            var locationPattern = new Regex(@"ORGANIZATION\s+");
            int numberOfLocations = locationPattern.Matches(nerTaggedInnerText).Count;
            return numberOfLocations;
        }

        private static int GetNumberOfOrganisationNames(string nerTaggedInnerText)
        {
            var organisationPattern = new Regex(@"LOCATION\s+");
            int numberOfOrganisations = organisationPattern.Matches(nerTaggedInnerText).Count;
            return numberOfOrganisations;
        }

        private static int GetNumberOfPersonNames(string nerTaggedInnerText)
        {
            var personPattern = new Regex(@"PERSON\s+");
            int numberOfPeople = personPattern.Matches(nerTaggedInnerText).Count;
            return numberOfPeople;
        }

        private static int NumberOfItemsContainingALanguageName(InitialCandidate initialcandidate)
        {
            var itemsWithALanguage = new List<HtmlNode>();

            var items = new List<HtmlNode>();

            items.AddRange(initialcandidate.Node.Descendants("li"));
            items.AddRange(initialcandidate.Node.Descendants("tr")); 

            var languages = GetLanguages();

            foreach (var language in languages)
            {
                var languageOnItsOwn = new Regex(@"\b" + language.ToLowerInvariant() + @"\b");

                foreach (var item in items)
                {
                    if (languageOnItsOwn.IsMatch(item.InnerText.ToLowerInvariant()))
                    {
                        itemsWithALanguage.Add(item);
                    }

                }       
            }

            return itemsWithALanguage.Count;;
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

        internal static List<string> GetTestBrands(List<CompanyAndBrands> companyBrandRelationships, MaxentTagger tagger)
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

                if ((!companyBrandRelationships.Any(rel => rel.BrandNames.Contains(trimmedBrand)))
                    && trimmedBrand.Length > 2 && ContainsNounPhrase(trimmedBrand.ToLowerInvariant(), tagger))
                {
                    newBrands.Add(trimmedBrand.ToLowerInvariant());
                }
            }

            var troublesome2LetterBrands = new List<String> { "ee", "et", "ry", "ss", "up", "ls", "oe", "se", "od", "tr", "ns", "ge", "ms", "ic", "am", "ge", "ns", "pa", "ips", "ps", "ub", "at" };
            var brandStopList = new List<String> { "bury", "ting", "may", "div", "chocolate", "fruit", "green", "mini", "his", "none", "food", "sen", "ips", "premium", "ion", "ist", "ngs", "ams", "none", "ist", "rts", "book", "official", "entity", "ref", "image", "border", "icon", "max", "bold", "default", "amp", "august", "rft", "non", "various", "ips", "computer", "other", "generic", "block", "m.c", "billion"};

            foreach (var stopListBrand in brandStopList)
            {
                newBrands.RemoveAll(nb => nb.Equals(stopListBrand));
            }

            return newBrands;
        }

        internal static bool ContainsNounPhrase(String text, MaxentTagger tagger)
        {
            var taggedText = tagger.tagString(text);

            return (taggedText.Contains("_NN") || taggedText.Contains("_NNS") || taggedText.Contains("_NNP")
                    || taggedText.Contains("_NNS"));
        }

        internal static bool TaggedTextContainsNounPhrase(String text)
        {
            return (text.Contains("_NN") || text.Contains("_NNS") || text.Contains("_NNP")
                    || text.Contains("_NNS"));
        }

        internal static List<String> GetTestCompanies(List<CompanyAndBrands> companyBrandRelationships)
        {
            var companies = FreeBaseHelpers.GetKnownCompaniesFromFreeBaseBusinessOperations();

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

            return shorterList;
        }

        internal static List<string> GetLanguages()
        {
            var languages = new List<String>();

            var directory = new DirectoryInfo("C:/Users/Alice/Desktop/languages");

            foreach (var file in directory.GetFiles())
            {
                languages.AddRange(File.ReadLines(file.FullName));
            }

            return languages.Select(language => language.Trim().ToLowerInvariant()).ToList();
        }

        internal static void OutputJsonResults(List<Candidate> candidates)
        {
            var jsonFile = new StreamWriter("C:/Users/Alice/Desktop/results.js");

            var classifiedRelations = new List<ClassifiedRelation>();

            foreach (var candidate in candidates)
            {
                if (candidate.KnownBrands != null || candidate.KnownBrand != null)
                {
                    classifiedRelations.AddRange(MapCandidateToClassifiedRelation(candidate));
                }
            }

            var dedupedClassifiedRelations = new List<ClassifiedRelation>();

            var uniqueGroup =
                    classifiedRelations.GroupBy(cr => new { cr.Company, cr.Brand}).Where(g => g.Count() <= 1);

            foreach (var unique in uniqueGroup)
            {
                foreach (var classifiedRelation in unique)
                {
                    dedupedClassifiedRelations.Add(classifiedRelation);
                }
            }

            var duplicateGroups =
                    classifiedRelations.GroupBy(cr => new { cr.Company, cr.Brand}).Where(g => g.Count() > 1);

            foreach (var duplicateGroup in duplicateGroups)
            {
                var trueInstances = duplicateGroup.Where(cr => cr.IsRelation);
                var falseInstances = duplicateGroup.Where(cr => !cr.IsRelation);
                var isRelation = trueInstances.Count() >= falseInstances.Count();
                var text = !trueInstances.Any() ? "" : trueInstances.FirstOrDefault().Source.Text;
                dedupedClassifiedRelations.Add(new ClassifiedRelation
                                                   {
                                                           IsRelation = isRelation,
                                                           Occurrences = trueInstances.Count(),
                                                           Company = duplicateGroup.Key.Company,
                                                           Brand = duplicateGroup.Key.Brand,
                                                           Source = new RelationSource
                                                                        {
                                                                                Text = text
                                                                        }
                                                   });
            }

            // Restrict all responses to those with the value true
            dedupedClassifiedRelations = dedupedClassifiedRelations.FindAll(cr => cr.IsRelation == true);

            var classifiedRelationsResponse = new ClassifiedRelationsResponse { Relations = dedupedClassifiedRelations };

            string json = JsonConvert.SerializeObject(classifiedRelationsResponse);

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
                                   + Environment.NewLine + Environment.NewLine + "Domain/ Title contain owner: " + candidate.DomainOrPageTitleContainsOwner
                                   + Environment.NewLine + Environment.NewLine + "Known company: " + candidate.KnownCompanyName + Environment.NewLine
                                   + "Known brand: " + candidate.KnownBrand + Environment.NewLine + Environment.NewLine
                                   + "Known brands: " + String.Join(", ", candidate.KnownBrands) + Environment.NewLine + Environment.NewLine
                                   + "Previous heading: " + String.Join(", ", candidate.NearestHeadingAbove) + Environment.NewLine + Environment.NewLine
                                   + "Location names: " + String.Join(", ", candidate.NumberOfLocationNames) + Environment.NewLine + Environment.NewLine
                                   + "Person names: " + String.Join(", ", candidate.NumberOfPersonNames) + Environment.NewLine + Environment.NewLine
                                   + "Organisation names: " + String.Join(", ", candidate.NumberOfOrganisationNames) + Environment.NewLine + Environment.NewLine
                                   + "Contains multiple language names: " + String.Join(", ", candidate.NumberOfItemsWithLanguages) + Environment.NewLine + Environment.NewLine
                                   + "Candidate HTML word count: " + String.Join(", ", candidate.CandidateHtmlWordCount) + Environment.NewLine + Environment.NewLine
                                   + "Previous content word count: " + String.Join(", ", candidate.PreviousContentWordCount) + Environment.NewLine + Environment.NewLine
                                   + "Previous content contains potential owner: " + String.Join(", ", candidate.PreviousContentContainsPotentialOwner) + Environment.NewLine + Environment.NewLine
                                   + "Candidate HTML contains potential owner: " + String.Join(", ", candidate.CandidateHtmlContainsPotentialOwner) + Environment.NewLine + Environment.NewLine
                                   + "Captions contain owner: " + String.Join(", ", candidate.CaptionsContainOwner) + Environment.NewLine + Environment.NewLine
                                   + "Candidate HTML contains potential owner: " + String.Join(", ", candidate.CandidateHtmlContainsPotentialOwner) + Environment.NewLine + Environment.NewLine
                                   + "Some items contain brand only: " + String.Join(", ", candidate.ItemsContainBrandOnly) + Environment.NewLine + Environment.NewLine
                                   + "Contains Multiple brands?" + candidate.ContainsMultipleBrands
                                   + Environment.NewLine + "\r Previous Relevant Node: " + candidate.PreviousContent
                                   + Environment.NewLine + "\r Html: " + candidate.CandidateHtml + Environment.NewLine
                                   + Environment.NewLine);
                }
                Console.WriteLine(
                                  candidate.PageTitle + Environment.NewLine + ' '
                                  + candidate.KnownCompanyName + Environment.NewLine + ' '
                                  + candidate.CandidateHtmlWithoutCandidateEntities + Environment.NewLine);
            }

            file.Close();

            Console.WriteLine(candidates.Count.ToString());
        }

        private static List<ClassifiedRelation> MapCandidateToClassifiedRelation(Candidate candidate)
        {
            var classifiedRelations = new List<ClassifiedRelation>();

            // If item level, use brand
            if (candidate.IsItemLevelCandidate)
            {
                var classifiedRelation = new ClassifiedRelation();
                classifiedRelation.IsRelation = candidate.CompanyBrandRelationship;
                classifiedRelation.Company = candidate.KnownCompanyName.ToLowerInvariant();
                classifiedRelation.Brand = candidate.KnownBrand.ToLowerInvariant();
                classifiedRelation.Occurrences = 1;
                classifiedRelation.Source = new RelationSource
                                                {
                                                        Url = candidate.Uri,
                                                        Text =
                                                                candidate.PreviousContent
                                                                + candidate.CandidateHtml,
                                                        Date = DateTime.Now
                                                };
                classifiedRelations.Add(classifiedRelation);
            }

            // If its a table or list level candidate, loop through the brands
            if (!candidate.IsItemLevelCandidate)
            {
                foreach (var brand in candidate.KnownBrands)
                {
                    var classifiedRelation = new ClassifiedRelation();
                    classifiedRelation.IsRelation = candidate.CompanyBrandRelationship;
                    classifiedRelation.Company = candidate.KnownCompanyName.ToLowerInvariant();
                    classifiedRelation.Brand = brand.ToLowerInvariant();
                    classifiedRelation.Occurrences = 1;
                    classifiedRelation.Source = new RelationSource
                                                    {
                                                            Url = candidate.Uri,
                                                            Text =
                                                                    candidate.PreviousContent
                                                                    + candidate.CandidateHtml,
                                                            Date = DateTime.Now
                                                    };
                    classifiedRelations.Add(classifiedRelation);
                }
            }

            return classifiedRelations;
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

            // Prevent very large sections of previous content as these tend to be less informative
            if (previousContent !=null && previousContent.InnerText.Length > 20000)
            {
                previousContent = null;
            }
            return previousContent;
        }

        internal static List<string> GetFullNounPhrasesFromString(string text, MaxentTagger tagger)
        {
            var nounPhrases = new List<String>();
            // Split into new lines before tagging - don't want to require that names on different lines are all matched by one brand
            string[] lines = Regex.Split(text, @"(?<=[\n])");
            foreach (var line in lines)
            {
                var taggedText = tagger.tagString(line);
                var tokens = taggedText.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                var pattern = @"_NNPS|_NNP|_NNS|_NN";
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (TaggedTextContainsNounPhrase(tokens[i]) && (i + 1) != tokens.Count && TaggedTextContainsNounPhrase(tokens[i + 1]))
                    {
                        var plainToken1 = Regex.Replace(tokens[i], pattern, "");
                        var plainToken2 = Regex.Replace(tokens[i + 1], pattern, "");
                        nounPhrases.Add(plainToken1 + " " + plainToken2);
                    }
                    else
                    {
                        if (TaggedTextContainsNounPhrase(tokens[i]))
                        {
                            var plainToken1 = Regex.Replace(tokens[i], pattern, "");
                            nounPhrases.Add(plainToken1);
                        }
                    }
                }
            }
            return nounPhrases;
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

                if (relation.CompanyName != null && (root.OuterHtml.ToLowerInvariant().Contains(relation.CompanyName.ToLowerInvariant() + " ")
                                                     || page.ToLowerInvariant().Contains(relation.CompanyName.ToLowerInvariant() + " ")))
                {
                    relationsWhereOwnerMentionedOnPage.Add(relation);
                }
            }
            return relationsWhereOwnerMentionedOnPage;
        }
    }

    static class Extensions
    {
        public static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable
        {
            return listToClone.Select(item => (T)item.Clone()).ToList();
        }
    }

}