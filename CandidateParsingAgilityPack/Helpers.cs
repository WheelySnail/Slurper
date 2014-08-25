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
        internal static List<Candidate> GetCandidatesFromPages(
                IEnumerable<string> pages,
                List<CompanyBrandRelationship> knownCompanyBrandRelationships)
        {
            var doc = new HtmlDocument();

            var candidates = new List<Candidate>();

            var safey = new HtmlSanitizer();

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
                    foreach (var ownerCompanySynonym in relation.CompanyNames)
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
                                foreach (var ownerSynonym in relation.CompanyNames)
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
//                                    // TODO limit this to paragraphs only? Or to the previous sentence only? 
                                       //GO UP THROUGH ALL PREVIOUS SIBLINGS, FIND ONE WHERE INNER HTML NOT EMPTY OR "\N"
                                       //IF NONE, CHECK PREVIOUS SIBLINGS OF PARENT ELEMENT
                                        //... what about case 2 where it's the parent's inner text that's needed? 


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
                                        if (previousSibling.PreviousSibling != null && (!previousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                                                                        && previousSibling.PreviousSibling.OuterHtml != "\n"))
                                        {
                                            previousContent = previousSibling.PreviousSibling.OuterHtml;
                                        }
                                        else if (previousSibling.PreviousSibling != null && (previousSibling.PreviousSibling.PreviousSibling != null && (!previousSibling.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty() && previousSibling.PreviousSibling.PreviousSibling.OuterHtml != "\n")))
                                        {
                                            previousContent = previousSibling.PreviousSibling.PreviousSibling.OuterHtml;
                                        }}

                                    // Check the three previous siblings of the parent sibling
                                    if (previousContent.IsNullOrEmpty())
                                    {
                                        if (parentNode.PreviousSibling != null && (!parentNode.PreviousSibling.OuterHtml.IsNullOrEmpty() && parentNode.PreviousSibling.OuterHtml != "\n"))
                                        {
                                            previousContent = parentNode.PreviousSibling.OuterHtml;
                                        }
                                        else
                                        {
                                            if (parentNode.PreviousSibling != null && (parentNode.PreviousSibling.PreviousSibling != null && (!parentNode.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                                                                       && parentNode.PreviousSibling.PreviousSibling.OuterHtml != "\n")))
                                            {
                                                previousContent = parentNode.PreviousSibling.PreviousSibling.OuterHtml;
                                            }
                                            else if (parentNode.PreviousSibling != null && (parentNode.PreviousSibling.PreviousSibling != null && (parentNode.PreviousSibling.PreviousSibling.PreviousSibling != null && (!parentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                                                                                                                                                                          && parentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml != "\n"))))
                                            {
                                                previousContent = parentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml;

                                            }
                                        }
                                    }

                                    // Check the three previous siblings of the grandparent sibling
                                    if (previousContent.IsNullOrEmpty())
                                    {
                                        if (grandparentNode.PreviousSibling != null && (!grandparentNode.PreviousSibling.OuterHtml.IsNullOrEmpty() && grandparentNode.PreviousSibling.OuterHtml != "\n"))
                                        {
                                            previousContent = grandparentNode.PreviousSibling.OuterHtml;
                                        }
                                        else
                                        {
                                            if (grandparentNode.PreviousSibling != null && (grandparentNode.PreviousSibling.PreviousSibling != null && (!grandparentNode.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                                                                       && grandparentNode.PreviousSibling.PreviousSibling.OuterHtml != "\n")))
                                            {
                                                previousContent = grandparentNode.PreviousSibling.PreviousSibling.OuterHtml;
                                            }
                                            else if (grandparentNode.PreviousSibling != null && (grandparentNode.PreviousSibling.PreviousSibling != null && (grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling != null && (!grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml.IsNullOrEmpty()
                                                                                                                                                                                          && grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml != "\n"))))
                                            {
                                                previousContent = grandparentNode.PreviousSibling.PreviousSibling.PreviousSibling.OuterHtml;

                                            }
                                        }
                                    }


                                    var candidateHtmlAndText = previousContent + initialcandidate.Node.OuterHtml;

                                    // TODO Find previous sibling which is an h1, h2, h3, h4, h5, or <p><strong>
                                    //var previousHeading = root.Descendants().Where(d => d.InnerText.Contains(company));

                                    var candidate = new Candidate
                                                        {
                                                                IsTableSegment = initialcandidate.Type == "table",
                                                                IsListSegment = initialcandidate.Type == "list",
                                                                //NearestHeading = previousHeading,
                                                                PreviousContent = safey.Sanitize(previousContent),
                                                                CandidateHtml =
                                                                        safey.Sanitize(initialcandidate.Node.OuterHtml),
                                                                KnownCompany = relation.CompanyNames,
                                                                // TODO will there be one candidate per brand synonym or one per brand? 
                                                                KnownBrand = brandSynonym,
                                                                KnownCompanyBrandRelationship = relation,
                                                                DomainOrPageTitleContainsOwner =
                                                                        domainOrTitleContainsOwner,
                                                                Uri = page,
                                                                PageTitle = title
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

        internal static List<CompanyBrandRelationship> GetKnownCompanyBrandRelationships()
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

        public static List<CompanyBrandRelationship> GetKnownCompanyBrandRelationshipsWithMultipleBrands(List<CompanyBrandRelationship> knownCandidates)
        {
            return knownCandidates.Where(r => r.BrandNames.Count() > 1).ToList();
        }

        internal static IEnumerable<string> GetPages(string path)
        {
            return Directory.GetFiles(path, "*.htm*", SearchOption.AllDirectories);
        }

        private static List<CompanyBrandRelationship> GetTestRelationships()
        {
            var relationships = new List<CompanyBrandRelationship>();
            // TODO get synonyms from Freebase or alter the CompanyBrandRelation class

            return relationships;
        }

        private static List<CompanyBrandRelationship> MapFreeBaseRelationshipToCompanyBrandRelationship(
                List<FreebaseCompanyBrandRelationship> relationships)
        {
            var companyBrandRelationships = new List<CompanyBrandRelationship>();
            foreach (var freebaseCompanyBrandRelationship in relationships)
            {
                if (freebaseCompanyBrandRelationship != null)
                {
                    companyBrandRelationships.Add(
                                                  new CompanyBrandRelationship
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
                               + "Known company: " + candidate.KnownCompany.FirstOrDefault().ToString() + Environment.NewLine
                               + "Known brand: " + candidate.KnownBrand + Environment.NewLine + Environment.NewLine
                               + "\r Html: " + candidate.PreviousContent + Environment.NewLine + "\r Html: "
                               + candidate.CandidateHtml + Environment.NewLine + Environment.NewLine);
                Console.WriteLine(
                                  candidate.PageTitle + Environment.NewLine + ' '
                                  + candidate.KnownCompany.FirstOrDefault().ToString() + Environment.NewLine + ' '
                                  + candidate.CandidateHtml + Environment.NewLine);
            }

            file.Close();

            Console.WriteLine(candidates.Count.ToString());

            Console.ReadLine();
        }
    }
}