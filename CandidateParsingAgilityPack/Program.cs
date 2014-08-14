namespace CandidateParsingAgilityPack
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using HtmlAgilityPack;

    #endregion

    internal class Program
    {
        private static void Main(string[] args)
        {
            // When scraping:

            //var doc = new HtmlDocument();
            //using (var client = new WebClient())
            //{
            //    var filename = Path.GetTempFileName();
            //    client.DownloadFile("http://en.wikipedia.org/wiki/Nestl%C3%A9", filename);
            //    doc.Load(filename);
            // Move rest of the method in here
            //}

            // When using local files: 

            GetTrainingCandidates();

            GetTestCandidates();
        }
        
        // Get training examples which contain a known company/ brand relationship
        private static List<Candidate> GetCandidatesFromPages(
                IEnumerable<string> pages,
                List<CompanyBrandRelationship> knownCompanyBrandRelationships)
        {
            var doc = new HtmlDocument();

            var candidates = new List<Candidate>();

            foreach (var page in pages)
            {
                doc.Load(page);

                var root = doc.DocumentNode;

                bool containsKnownRelationship = false;

                // The relations containing an owner company mentioned in this page
                var relationsWhereOwnerMentionedOnPage = new List<CompanyBrandRelationship>();

                // Check if the page or filename contains any known seed company names
                // There will be some duplication of effort here as the same company can be present in multiple relations...
                // Work out later when am retrieving real relations from API
                foreach (var relation in knownCompanyBrandRelationships)
                {
                    foreach (var ownerCompanySynonym in relation.OwnerNames)
                    {
                        if (root.OuterHtml.ToLowerInvariant().Contains(ownerCompanySynonym.ToLowerInvariant())
                            || page.ToLowerInvariant().Contains(ownerCompanySynonym.ToLowerInvariant()))
                        {
                            containsKnownRelationship = true;
                            relationsWhereOwnerMentionedOnPage.Add(relation);
                            // stop checking for synonym mentions once one synonym for a relation has been identified - move to the next iteration of the outer loop
                            break;
                        }
                    }
                }

                // Skip to next page if the current page doesn't contain a seed company name
                if (!containsKnownRelationship)
                {
                    continue;
                }

                // Gather the page title which is an attribute of all candidates
                var title = doc.DocumentNode.SelectSingleNode("//head/title").InnerText;

                // Gather all tables, lists and paragraphs. Doesn't seem to be a way to retrieve a node's html element type after it's been stored so add this to an initial candidate object

                var initialCandidateSegments =
                        root.Descendants("table")
                            .ToList()
                            .Select(table => new InitialCandidate() { Node = table, Type = "table" })
                            .ToList();

                initialCandidateSegments.AddRange(
                                                  root.Descendants("ul")
                                                      .ToList()
                                                      .Select(
                                                              list =>
                                                              new InitialCandidate() { Node = list, Type = "list" }));

                initialCandidateSegments.AddRange(
                                                  root.Descendants("p")
                                                      .ToList()
                                                      .Select(
                                                              paragraph =>
                                                              new InitialCandidate()
                                                                  {
                                                                          Node = paragraph,
                                                                          Type = "paragraph"
                                                                  }));

                // For each paragraph, list or table in the page
                foreach (var initialcandidate in initialCandidateSegments)
                {
                    // For each relation where an owner name is present on this page. Candidates are created at this level as they are a combination of relation + segment.
                    foreach (var relation in relationsWhereOwnerMentionedOnPage)
                    {
                        // For each variation of the brand name in this relation
                        foreach (var brandSynonym in relation.BrandNames)
                        {
                            // Check if a brand synonym is present in the initial candidate. This is a necessary but not sufficient condition for creating a candidate
                            if (
                                    initialcandidate.Node.OuterHtml.ToLowerInvariant()
                                                    .Contains(brandSynonym.ToLowerInvariant()))
                            {
                                var domainOrTitleContainsOwner = false;
                                var initialCandidateOrPreviousSiblingContainOwner = false;

                                // If the document domain/ filename or title contains the relation name, the presence of the brand name alone in the segment is sufficient
                                foreach (var ownerSynonym in relation.OwnerNames)
                                {
                                    if (page.ToLowerInvariant().Contains(ownerSynonym.ToLowerInvariant())
                                        || title.ToLowerInvariant().Contains(ownerSynonym.ToLowerInvariant()))
                                    {
                                        domainOrTitleContainsOwner = true;
                                    }
                                    if (
                                            initialcandidate.Node.OuterHtml.ToLowerInvariant()
                                                            .Contains(ownerSynonym.ToLowerInvariant())
                                            || initialcandidate.Node.PreviousSibling.OuterHtml.ToLowerInvariant()
                                                               .Contains(ownerSynonym.ToLowerInvariant()))
                                    {
                                        initialCandidateOrPreviousSiblingContainOwner = true;
                                    }
                                }

                                if (domainOrTitleContainsOwner || initialCandidateOrPreviousSiblingContainOwner)
                                {
                                    // The html and text for the candidate should include the preceeding html node if the candidate is a table or list. 
                                    // TODO limit this to paragraphs only? Or to the previous sentence only? 
                                    var candidateHtmlAndText = initialcandidate.Type == "paragraph"
                                                                       ? initialcandidate.Node.OuterHtml
                                                                       : initialcandidate.Node.PreviousSibling.OuterHtml
                                                                         + initialcandidate.Node.OuterHtml;

                                    // TODO Find previous sibling which is an h1, h2, h3, h4, h5, or <p><strong>
                                    //var previousHeading = root.Descendants().Where(d => d.InnerText.Contains(company));

                                    var candidate = new Candidate
                                                        {
                                                                IsTableSegment = initialcandidate.Type == "table",
                                                                IsListSegment = initialcandidate.Type == "list",
                                                                IsParagraphSegment =
                                                                        initialcandidate.Type == "paragraph",
                                                                //NearestHeading = previousHeading,
                                                                CandidateHtmlAndText = candidateHtmlAndText,
                                                                KnownCompany = relation.OwnerNames,
                                                                // TODO will there be one candidate per brand synonym or one per brand? 
                                                                KnownBrand = brandSynonym,
                                                                KnownCompanyBrandRelationship = relation,
                                                                DomainOrPageTitleContainsOwner =
                                                                        domainOrTitleContainsOwner,
                                                                Uri = page,
                                                                PageTitle = title.ToString()
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

        private static List<CompanyBrandRelationship> GetKnownCompanyBrandRelationships()
        {
            var relationships = new List<CompanyBrandRelationship>();
            relationships.Add(
                              new CompanyBrandRelationship
                                  {
                                          OwnerId = "1",
                                          OwnerNames = new List<string>() { "Nestle", "Nestlé" },
                                          BrandNames = new List<string>()
                                                           {
                                                                   "Buxton",
                                                                   //"Kit Kat",
                                                                   //"Nescafé",
                                                                   //"Smarties",
                                                                   //"Nesquik",
                                                                   //"Stouffer's",
                                                                   //"Maggi",
                                                                   //"L'Oreal"
                                                           }
                                  });
            relationships.Add(
                              new CompanyBrandRelationship
                                  {
                                          OwnerId = "1",
                                          OwnerNames = new List<string>() { "Nestle", "Nestlé" },
                                          BrandNames = new List<string>() { "Kitkat" }
                                  });
            relationships.Add(
                              new CompanyBrandRelationship
                                  {
                                          OwnerId = "1",
                                          OwnerNames = new List<string>() { "Nestle", "Nestlé" },
                                          BrandNames = new List<string>() { "Nespresso" }
                                  });

            relationships.Add(
                              new CompanyBrandRelationship
                                  {
                                          OwnerId = "2",
                                          OwnerNames = new List<string>() { "Cadbury", "Cadburies" },
                                          BrandNames = new List<string>()
                                                           {
                                                                   "Dairy Milk",
                                                                   //"Creme Egg",
                                                                   //"Roses"
                                                           }
                                  });

            relationships.Add(
                              new CompanyBrandRelationship
                                  {
                                          OwnerId = "3",
                                          OwnerNames =
                                                  new List<string>()
                                                      {
                                                              "Bayer",
                                                              "Bayer AG",
                                                              "Bayer CropScience",
                                                              "Bayer BioScience",
                                                              "Bayer Pharma",
                                                              "Bayer Consumer Care",
                                                              "Bayer Animal Health",
                                                      },
                                          BrandNames = new List<string>()
                                                           {
                                                                   //"Miles Laboratories",
                                                                   //"Miles Canada",
                                                                   //"Cutter Laboratories",
                                                                   //"Alka-Seltzer",
                                                                   //"Flintstones vitamins",
                                                                   //"One-A-Day vitamins",
                                                                   //"Cutter insect repellent",
                                                                   //"Bomac Group",
                                                                   //"Xofigo",
                                                                   //"Aventis",
                                                                   //"Sanofi",
                                                                   "LibertyLink",
                                                                   //"Jatropha",
                                                                   //"Yasmin",
                                                                   //"Nexavar",
                                                                   //"Kogenate"
                                                           }
                                  });

            return relationships;

            // https://www.googleapis.com/freebase/v1/search?query=bob&key=<YOUR_API_KEY>

            // https://www.googleapis.com/freebase/v1/search?domain=business&type=company_brand_relationship&key=<YOUR_API_KEY> ?

            // id: /business/company_brand_relationship 

            // /query?type=/business/company_brand_relationship

            // http://www.freebase.com/business/company_brand_relationship?instances=

            // https://api.opencorporates.com/companies/search?q=barclays+bank

            // https://api.opencorporates.com/companies/gb/01320086/network
        }

        private static IEnumerable<string> GetPages(string path)
        {
            return Directory.GetFiles(path, "*.htm*", SearchOption.AllDirectories);
        }

        private static void GetTestCandidates()
        {
            // TODO 
            Console.ReadLine();
        }

        private static void GetTrainingCandidates()
        {
            // TODO must sanitise this data, as it's user generated

            var knownCompanyBrandRelationships = GetKnownCompanyBrandRelationships();

            var pages = GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidates = GetCandidatesFromPages(pages, knownCompanyBrandRelationships);

            var file = new StreamWriter("C:/Users/Alice/Desktop/Candidates.txt");

            foreach (var candidate in candidates)
            {
                file.WriteLine(
                               "Page title: " + candidate.PageTitle + Environment.NewLine + Environment.NewLine
                               + "Known company: " + candidate.KnownCompany.ToList() + Environment.NewLine
                               + Environment.NewLine + "\r Html & Text: " + candidate.CandidateHtmlAndText
                               + Environment.NewLine + Environment.NewLine);
                Console.WriteLine(
                                  candidate.PageTitle + Environment.NewLine + ' '
                                  + candidate.KnownCompany.ToList().ToString() + Environment.NewLine + ' '
                                  + candidate.CandidateHtmlAndText + Environment.NewLine);
            }

            file.Close();

            Console.WriteLine(candidates.Count.ToString());

            Console.ReadLine();
        }
    }
}