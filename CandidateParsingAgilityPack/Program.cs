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

            var knownCompanyBrandRelationships = GetKnownCompanyBrandRelationships();

            var pages = GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidates = GetCandidatesFromPages(pages, knownCompanyBrandRelationships);

            var file = new StreamWriter("C:/Users/Alice/Desktop/Candidates.txt");

            foreach (var candidate in candidates)
            {
                file.WriteLine(
                               "Page title: " + candidate.PageTitle + "\r Known company: " + candidate.KnownCompany
                               + "\r Html & Text: " + candidate.CandidateHtmlAndText + "\r ");
                Console.WriteLine(
                                  candidate.PageTitle + ' ' + candidate.KnownCompany + ' '
                                  + candidate.CandidateHtmlAndText);
            }

            file.Close();

            Console.ReadLine();
        }

        private static List<Candidate> GetCandidatesFromPages(
                IEnumerable<string> pages,
                List<CompanyBrandRelationship> knownCompanyBrandRelationships)
        {
            var doc = new HtmlDocument();

            var candidates = new List<Candidate>();

            // For each page:
            // Does it contain a known company and its related known brand in metadata or text? (Quickly discard ones that don't without much processing) If so which company?
            // If so, which known companies?
            // Exit if it doesn't

            // Option 1:
            // For each known company present in the doc, find the individual paragraph, ul or table nodes containing the brand name or a node that contains the brand name.            
            // Check the previous element (s?) for mention of company name, include in the candidate if it does.

            // Option 2: 
            // Find all structural candidates:
            //      paragraphs
            //      lists + paragraph above
            //      tables + paragraph above
            // For each known brand for the known company for the page:
            //      Find candidates which contain the brand and add to the list of candidates, noting the company + brand for that candidate
            //      For lists & tables, include the previous paragraph if there is one. For paragraphs, check the 3 sibling paragraphs above & below for a mention of the company, include if one is found.

            // 

            foreach (var page in pages)
            {
                doc.Load(page);

                var root = doc.DocumentNode;

                bool containsKnownRelationship = false;

                var knownOwnerCompaniesPresent = new List<string>();

                // List<HtmlNode> nodesContainingKnownCompany = new List<HtmlNode>();

                // Check if the page contains any known seed company names
                foreach (var knownRelationship in knownCompanyBrandRelationships)
                {
                    var companyName = knownRelationship.OwnerNames.First();

                    if (root.OuterHtml.Contains(knownRelationship.OwnerNames.First()))
                    {
                        containsKnownRelationship = true;
                        knownOwnerCompaniesPresent.Add(knownRelationship.OwnerNames.First());
                    }

                    //var nodes = root.Descendants().Where(d => d.InnerText.Contains(company));
                    //foreach (var node in nodes)
                    //{
                    //    nodesContainingKnownCompany.Add(node);
                    //    Console.WriteLine("NEXT" + node.OriginalName + node.InnerText);
                    //}
                }

                // Skip document if it doesn't contain a seed company name
                if (!containsKnownRelationship)
                {
                    continue;
                }

                // Gather the title which is an attribute of all candidates
                var title = root.Descendants("title").SingleOrDefault();

                if (title != null)
                {
                    Console.WriteLine("Page title: {0}", title.InnerText);
                }

                var tables = root.Descendants("table").ToList();

                var lists = root.Descendants("ul").ToList();

                var paragraphs = root.Descendants("p").ToList();

                foreach (var table in tables)
                {
                    // Need a new candidate for each known company, even for the same page segment.
                    foreach (var knownCompany in knownOwnerCompaniesPresent)
                    {
                        if (table.OuterHtml.Contains(knownCompany))
                        {
                            var candidate = new Candidate
                                                {
                                                        IsTableSegment = true,
                                                        CandidateHtmlAndText = table.OuterHtml,
                                                        KnownCompany = knownCompany
                                                };

                            candidate.Uri = page;

                            if (title != null)
                            {
                                candidate.PageTitle = title.ToString();
                            }

                            candidates.Add(candidate);
                        }
                    }
                }

                foreach (var list in lists)
                {
                    var candidate = new Candidate { IsTableSegment = true };

                    if (title != null)
                    {
                        candidate.PageTitle = title.ToString();
                    }

                    candidate.CandidateHtmlAndText = list.OuterHtml;

                    //Console.WriteLine("Table text: {0}", list.InnerText.Trim());
                }

                foreach (var paragraph in paragraphs)
                {
                    var candidate = new Candidate { IsTableSegment = true };

                    if (title != null)
                    {
                        candidate.PageTitle = title.ToString();
                    }

                    candidate.CandidateHtmlAndText = paragraph.OuterHtml;

                    //Console.WriteLine("Table text: {0}", list.InnerText.Trim());
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
                                          OwnerNames = new List<string>() { "Nestle, Nestlé" },
                                          BrandNames =
                                                  new List<string>()
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
                                          OwnerNames = new List<string>() { "Nestle, Nestlé" },
                                          BrandNames = new List<string>() { "Kitkat" }
                                  });
            relationships.Add(
                              new CompanyBrandRelationship
                                  {
                                          OwnerNames = new List<string>() { "Nestle, Nestlé" },
                                          BrandNames = new List<string>() { "Nespresso" }
                                  });

            relationships.Add(
                              new CompanyBrandRelationship
                                  {
                                          OwnerNames = new List<string>() { "Cadbury, Cadburies" },
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
                                          BrandNames =
                                                  new List<string>()
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
    }
}