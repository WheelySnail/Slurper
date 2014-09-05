namespace CandidateParsingAgilityPack
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;

    using CandidateParsingAgilityPack.Model;

    using Newtonsoft.Json;

    internal class FreeBaseHelpers
    {
        private const string API_KEY = "";

        private const String url = "https://www.googleapis.com/freebase/v1/mqlread";

        internal static FreeBaseBrandResponse GetKnownBrands()
        {
            string companiesQuery = "?query=[{\"type\":\"/business/brand\",\"name\": null,\"limit\":8780}]&key="
                                    + API_KEY;

            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage reponse = client.GetAsync(companiesQuery).Result;

            if (reponse.IsSuccessStatusCode)
            {
                var responseString = reponse.Content.ReadAsStringAsync().Result;
                var brands = JsonConvert.DeserializeObject<FreeBaseBrandResponse>(responseString);
                return brands;
            }
            else
            {
                throw new Exception();
            }
        }

        internal static List<CompanyAndBrands> GetKnownCompanyBrandRelationshipsFromConsumerCompanies()
        {
            // TODO this query is returning company brand relationships where the company has BOTH products and brands - only 241 of 2119 possible relations. Query both and combine
            string companiesQuery =
                    "?query=[{\"type\":\"/business/consumer_company\",\"id\": null,\"name\": null,\"brands\":[{\"brand\": null}],\"products\":[{\"consumer_product\": null}],\"limit\":2119}]&key="
                    + API_KEY;

            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage reponse = client.GetAsync(companiesQuery).Result;

            if (reponse.IsSuccessStatusCode)
            {
                var responseString = reponse.Content.ReadAsStringAsync().Result;
                var brands = JsonConvert.DeserializeObject<FreeBaseConsumerCompanyResponse>(responseString);
                return MapFreeBaseConsumerCompaniesToCompanyBrandRelationship(brands.Companies);
            }
            else
            {
                throw new Exception();
            }
        }

        private static List<CompanyAndBrands> MapFreeBaseConsumerCompaniesToCompanyBrandRelationship(
                List<FreebaseConsumerCompany> companies)
        {
            var companyBrandRelationships = new List<CompanyAndBrands>();
            foreach (var freebaseConsumerCompany in companies)
            {
                if (freebaseConsumerCompany != null)
                {
                    var allBrandsAndProducts = new List<string>();
                    foreach (var brand in freebaseConsumerCompany.Brands)
                    {
                        allBrandsAndProducts.Add(brand.Brand);
                    }

                    foreach (var product in freebaseConsumerCompany.Products)
                    {
                        allBrandsAndProducts.Add(product.Product);
                    }

                    companyBrandRelationships.Add(
                                                  new CompanyAndBrands
                                                      {
                                                              BrandNames = allBrandsAndProducts,
                                                              CompanyNames =
                                                                      new List<string>
                                                                          {
                                                                                  freebaseConsumerCompany
                                                                                          .Name
                                                                          }
                                                      });
                }
            }
            return companyBrandRelationships;
        }
    }
}