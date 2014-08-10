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

            foreach (var page in pages)
            {
                doc.Load(page);

                var root = doc.DocumentNode;

                bool containsKnownRelationship = false;

                // The relations containing an owner company mentioned in this page
                var relationsWhereOwnerMentionedOnPage = new List<CompanyBrandRelationship>();

                // Check if the page contains any known seed company names
                foreach (var relation in knownCompanyBrandRelationships)
                {
                    foreach (var ownerCompanySynonym in relation.OwnerNames)
                    {
                        if (root.OuterHtml.Contains(ownerCompanySynonym))
                        {
                            containsKnownRelationship = true;
                            // TODO need some way of de-duping the list. Use a dictionary?
                            relationsWhereOwnerMentionedOnPage.Add(relation);
                        }
                    }
                }

                // Skip to next page if the current page doesn't contain a seed company name
                if (!containsKnownRelationship)
                {
                    continue;
                }

                // Gather the page title which is an attribute of all candidates
                var title = root.Descendants("title").SingleOrDefault();

                if (title != null)
                {
                    Console.WriteLine("Page title: {0}", title.InnerText);
                }

                // Gather all tables, lists and paragraphs
                var tables = root.Descendants("table").ToList();

                var lists = root.Descendants("ul").ToList();

                var paragraphs = root.Descendants("p").ToList();

                foreach (var table in tables)
                {
                    // Need a new candidate for each known company, even for the same page segment. Use the subset of relations where the owner is mentioned on the page
                    foreach (var relation in relationsWhereOwnerMentionedOnPage)
                    {
                        foreach (var brandSynonym in relation.BrandNames)
                        {
                            // If the document title contains the relation name, the presence of the brand name alone in the segment is sufficient
                            // TODO loop through owner names
                            if (title.OuterHtml.Contains(relation.OwnerNames.First()))
                            {
                                if (table.OuterHtml.Contains(brandSynonym))
                                {
                                    // TODO Previous sibling where it's an h1, h2, h3, h4, h5, or <p><strong>
                                    //var previousHeading = 
                                    var candidate = new Candidate
                                                        {
                                                                IsTableSegment = true,
                                                                IsListSegment = false,
                                                                IsTextSegment = false,
                                                                //NearestHeading = previousHeading  //var nodes = root.Descendants().Where(d => d.InnerText.Contains(company));,
                                                                CandidateHtmlAndText =
                                                                        table.PreviousSibling + table.OuterHtml,
                                                                KnownCompany = relation.OwnerNames,
                                                                KnownBrand = brandSynonym,
                                                                KnownCompanyBrandRelationship = relation,
                                                                TitleContainsOwner = true
                                                        };

                                    candidate.Uri = page;

                                    if (title != null)
                                    {
                                        candidate.PageTitle = title.ToString();
                                    }

                                    candidates.Add(candidate);
                                }
                            }

                                    // If the document title doesn't contain the relation name, it needs to be present in the table, or it's previous sibling
                            else if (table.OuterHtml.Contains(brandSynonym)
                                     && (table.OuterHtml.Contains(relation.OwnerNames.First())
                                         || table.PreviousSibling.OuterHtml.Contains(relation.OwnerNames.First())))
                            {
                                //var previousHeading = 
                                var candidate = new Candidate
                                                    {
                                                            IsTableSegment = true,
                                                            IsListSegment = false,
                                                            IsTextSegment = false,
                                                            //NearestHeading = previousHeading  //var nodes = root.Descendants().Where(d => d.InnerText.Contains(company));,
                                                            CandidateHtmlAndText =
                                                                    table.PreviousSibling + table.OuterHtml,
                                                            KnownCompany = relation.OwnerNames,
                                                            KnownBrand = brandSynonym,
                                                            KnownCompanyBrandRelationship = relation,
                                                            TitleContainsOwner = false
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
                }

                foreach (var list in lists)
                    {
                    // Need a new candidate for each known company, even for the same page segment. Use the subset of relations where the owner is mentioned on the page
                    foreach (var relation in relationsWhereOwnerMentionedOnPage)
                    {
                        foreach (var brandSynonym in relation.BrandNames)
                        {
                            // If the document title contains the relation name, the presence of the brand name alone in the segment is sufficient
                            // TODO loop through owner names
                            if (title.OuterHtml.Contains(relation.OwnerNames.First()))
                            {
                                if (list.OuterHtml.Contains(brandSynonym))
                                {
                                    // TODO Previous sibling where it's an h1, h2, h3, h4, h5, or <p><strong>
                                    //var previousHeading = 
                                    var candidate = new Candidate
                                                        {
                                                                IsTableSegment = true,
                                                                IsListSegment = false,
                                                                IsTextSegment = false,
                                                                //NearestHeading = previousHeading  //var nodes = root.Descendants().Where(d => d.InnerText.Contains(company));,
                                                                CandidateHtmlAndText =
                                                                        list.PreviousSibling + list.OuterHtml,
                                                                KnownCompany = relation.OwnerNames,
                                                                KnownBrand = brandSynonym,
                                                                KnownCompanyBrandRelationship = relation,
                                                                TitleContainsOwner = true
                                                        };

                                    candidate.Uri = page;

                                    if (title != null)
                                    {
                                        candidate.PageTitle = title.ToString();
                                    }

                                    candidates.Add(candidate);
                                }
                            }

                                    // If the document title doesn't contain the relation name, it needs to be present in the table, or it's previous sibling
                            else if (list.OuterHtml.Contains(brandSynonym)
                                     && (list.OuterHtml.Contains(relation.OwnerNames.First())
                                         || list.PreviousSibling.OuterHtml.Contains(relation.OwnerNames.First())))
                            {
                                //var previousHeading = 
                                var candidate = new Candidate
                                                    {
                                                            IsTableSegment = false,
                                                            IsListSegment = true,
                                                            IsTextSegment = false,
                                                            //NearestHeading = previousHeading  //var nodes = root.Descendants().Where(d => d.InnerText.Contains(company));,
                                                            CandidateHtmlAndText =
                                                                    list.PreviousSibling + list.OuterHtml,
                                                            KnownCompany = relation.OwnerNames,
                                                            KnownBrand = brandSynonym,
                                                            KnownCompanyBrandRelationship = relation,
                                                            TitleContainsOwner = false
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
                    }

                    foreach (var paragraph in paragraphs)
                    {
                        var candidate = new Candidate { IsTableSegment = true };

                        if (title != null)
                        {
                            candidate.PageTitle = title.ToString();
                        }

                        candidate.CandidateHtmlAndText = paragraph.OuterHtml;

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