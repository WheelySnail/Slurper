namespace CandidateParsingAgilityPack
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;

    using CandidateParsingAgilityPack.Model;

    using Newtonsoft.Json;

    using sun.net.www.content.image;

    internal class FreeBaseHelpers
    {
        private const string API_KEY = "AIzaSyAnlfYJbox67a_jRXUv_9SbGHcfvG0ldbU";

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
            var brandsResponse = new FreeBaseConsumerCompanyResponse();
            var productsResponse = new FreeBaseConsumerCompanyResponse();

            // TODO this query is returning company brand relationships where the company has BOTH products and brands - only 241 of 2119 possible relations. Query both and combine
            string companiesWithBrandsQuery =
                    "?query=[{\"type\":\"/business/consumer_company\",\"id\": null,\"name\": null,\"brands\":[{\"brand\": null}],\"limit\":20}]&key="
                    + API_KEY;

            string companiesWithProductsQuery =
                    "?query=[{\"type\":\"/business/consumer_company\",\"id\": null,\"name\": null,\"products\":[{\"consumer_product\": null}],\"limit\":20}]&key="
                    + API_KEY;

            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage BrandsResponseMessage = client.GetAsync(companiesWithBrandsQuery).Result;

            if (BrandsResponseMessage.IsSuccessStatusCode)
            {
                var responseString = BrandsResponseMessage.Content.ReadAsStringAsync().Result;
                brandsResponse = JsonConvert.DeserializeObject<FreeBaseConsumerCompanyResponse>(responseString);
            }
            else
            {
                throw new Exception();
            }

            HttpResponseMessage productsReponseMessage = client.GetAsync(companiesWithProductsQuery).Result;

            if (productsReponseMessage.IsSuccessStatusCode)
            {
                var responseString = productsReponseMessage.Content.ReadAsStringAsync().Result;
                productsResponse = JsonConvert.DeserializeObject<FreeBaseConsumerCompanyResponse>(responseString);
            }
            else
            {
                throw new Exception();
            }

            var deDupedCompanyAndBrandsList = new List<CompanyAndBrands>();

            var companiesAndBrands = MapFreeBaseConsumerCompaniesToCompanyBrandRelationship(brandsResponse.Companies); 
            var companiesAndProducts = MapFreeBaseConsumerCompaniesToCompanyBrandRelationship(productsResponse.Companies); 

            // Add product names to companies already present
            foreach (var companyAndBrands in companiesAndBrands)
            {
                deDupedCompanyAndBrandsList.Add(companyAndBrands);

                foreach (var companyAndProducts in companiesAndProducts)
                {
                    if (companyAndBrands.CompanyNames.FirstOrDefault() == companyAndProducts.CompanyNames.FirstOrDefault())
                    {
                        companyAndBrands.BrandNames.AddRange(companyAndProducts.BrandNames);
                    }
                }
            }

            foreach (var companyAndProduct in companiesAndProducts)
            {
                if (
                        !deDupedCompanyAndBrandsList.Exists(
                                                           cb =>
                                                           cb.CompanyNames.FirstOrDefault()
                                                           == companyAndProduct.CompanyNames.FirstOrDefault()))
                {
                    deDupedCompanyAndBrandsList.Add(companyAndProduct);
                }
            }

            // Remove any brands which were returned from FreeBase as 'null'

            foreach (var companyAndBrands in deDupedCompanyAndBrandsList)
            {
                companyAndBrands.BrandNames.RemoveAll(bn => bn == null);
            }

            return deDupedCompanyAndBrandsList;
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
                    if (freebaseConsumerCompany.Brands != null)
                    {
                        foreach (var brand in freebaseConsumerCompany.Brands)
                        {
                            allBrandsAndProducts.Add(brand.Brand);
                        }
                    }

                    if (freebaseConsumerCompany.Products != null)
                    {
                        foreach (var product in freebaseConsumerCompany.Products)
                        {
                            allBrandsAndProducts.Add(product.Product);
                        }
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