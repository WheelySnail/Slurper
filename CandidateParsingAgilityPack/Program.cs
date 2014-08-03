namespace CandidateParsingAgilityPack
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;

    using HtmlAgilityPack;

    #endregion

    internal class Program
    {
        private static List<Candidate> GetCandidatesFromPages(IEnumerable<string> pages, List<string> knownCompanies)
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
            // For each structural candidate, does it contain the known company and known brand for the company?
            // If the known company is present in the page metadata, presence of known brand is sufficient


            foreach (var page in pages)
            {
                doc.Load(page);

                var root = doc.DocumentNode;

                bool containsKnownCompany = false;

                List<string> knownCompaniesPresent = new List<string>();

                List<HtmlNode> nodesContainingKnownCompany = new List<HtmlNode>();

                // Check if the page contains any known seed company names
                foreach (var knownCompany in knownCompanies)
                {
                    string company = knownCompany;

                    if (root.OuterHtml.Contains(knownCompany))
                    {
                        containsKnownCompany = true;
                        knownCompaniesPresent.Add(knownCompany);
                    }

                    //var nodes = root.Descendants().Where(d => d.InnerText.Contains(company));
                    //foreach (var node in nodes)
                    //{
                    //    nodesContainingKnownCompany.Add(node);
                    //    Console.WriteLine("NEXT" + node.OriginalName + node.InnerText);
                    //}
                }

                // Skip document if it doesn't contain a seed company name
                if (!containsKnownCompany)
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
                    foreach (var knownCompany in knownCompaniesPresent)
                    {
                        if (table.OuterHtml.Contains(knownCompany))
                        {
                            var candidate = new Candidate { IsTableSegment = true, CandidateHtmlAndText = table.OuterHtml, KnownCompany = knownCompany};

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

        private static List<string> GetKnownCompanies()
        {
            return new List<string> { "Nestlé", "Cadbury", "Bayer" };

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

            var knownCompanies = GetKnownCompanies();

            var pages = GetPages("C:/Users/Alice/Desktop/TestDocuments");

            var candidates = GetCandidatesFromPages(pages, knownCompanies);

            var file = new System.IO.StreamWriter("C:/Users/Alice/Desktop/Candidates.txt");

            foreach (var candidate in candidates)
            {
                file.WriteLine("Page title: " + candidate.PageTitle + "\r Known company: " + candidate.KnownCompany + "\r Html & Text: " + candidate.CandidateHtmlAndText + "\r ");
                Console.WriteLine(candidate.PageTitle + ' ' + candidate.KnownCompany + ' ' + candidate.CandidateHtmlAndText);
            }

            file.Close();

            Console.ReadLine();
        }
    }
}