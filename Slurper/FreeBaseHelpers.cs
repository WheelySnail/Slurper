namespace Slurper
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;

    using Newtonsoft.Json;

    using Slurper.Model;

    #endregion

    internal class FreeBaseHelpers
    {
        private const string API_KEY = "";

        private const String url = "https://www.googleapis.com/freebase/v1/mqlread";

        internal static FreeBaseBrandResponse GetKnownBrands()
        {
            string companiesQuery = "?query=[{\"type\":\"/business/brand\",\"name\": null,\"limit\":10}]&key=" + API_KEY;

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

        internal static List<String> GetKnownCompaniesFromFreeBaseConsumerCompanies()
        {
            string companiesQuery = "?query=[{\"type\":\"/business/consumer_company\",\"name\": null,\"limit\":10}]&key=" + API_KEY;

            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage reponse = client.GetAsync(companiesQuery).Result;

            if (reponse.IsSuccessStatusCode)
            {
                var responseString = reponse.Content.ReadAsStringAsync().Result;
                var companies = JsonConvert.DeserializeObject<FreeBaseConsumerCompanyResponse>(responseString);
                return GetCompanyNamesFromFreeBaseConsumerCompanyResponse(companies);
            }
            else
            {
                throw new Exception();
            }
        }

        internal static List<String> GetKnownCompaniesFromFreeBaseBusinessOperations()
        {
            string companiesQuery = "?query=[{\"type\":\"/business/business_operation\",\"name\": null,\"limit\":100}]&key=" + API_KEY;

            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage reponse = client.GetAsync(companiesQuery).Result;

            if (reponse.IsSuccessStatusCode)
            {
                var responseString = reponse.Content.ReadAsStringAsync().Result;
                var businesses = JsonConvert.DeserializeObject<FreeBaseBusinessOperationResponse>(responseString);
                return GetCompanyNamesFromFreeBaseBusinessOperationResponse(businesses);
            }
            else
            {
                throw new Exception();
            }
        }

        private static List<string> GetCompanyNamesFromFreeBaseBusinessOperationResponse(FreeBaseBusinessOperationResponse businesses)
        {
            var businessNames = new List<string>();
            foreach (var business in businesses.Businesses)
            {
                business.Name = StripIncAndLtd(business.Name);
                businessNames.Add(business.Name);
            }
            return businessNames;
        }

        private static string StripIncAndLtd(string name)
        {
            const string Pattern = @"(Limited|Incorporated|,\sLtd|,\sInc|,\s\.Ltd|,\s\.Inc|\.Ltd|\.Inc|Ltd\.|Inc\.|Ltd|Inc)$";
            name = Regex.Replace(name, Pattern, "", RegexOptions.IgnoreCase);
            return name.Trim();
        }

        private static List<string> GetCompanyNamesFromFreeBaseConsumerCompanyResponse(FreeBaseConsumerCompanyResponse companies)
        {
            var companyNames = new List<string>();
            foreach (var company in companies.Companies)
            {
                company.Name = StripIncAndLtd(company.Name);
                companyNames.Add(company.Name);
            }
            return companyNames;
        }

        internal static List<CompanyAndBrands> GetKnownCompanyBrandRelationshipsFromConsumerCompanies()
        {
            var brandsResponse = new FreeBaseConsumerCompanyResponse();
            var productsResponse = new FreeBaseConsumerCompanyResponse();

            string companiesWithBrandsQuery =
                    "?query=[{\"type\":\"/business/consumer_company\",\"id\": null,\"name\": null,\"brands\":[{\"brand\": null}],\"limit\":100}]&key="
                    + API_KEY;

            string companiesWithProductsQuery =
                    "?query=[{\"type\":\"/business/consumer_company\",\"id\": null,\"name\": null,\"products\":[{\"consumer_product\": null}],\"limit\":100}]&key="
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
            var companiesAndProducts = MapFreeBaseConsumerCompaniesToCompanyBrandRelationship(
                                                                                              productsResponse.Companies);

            // Add product names to companies already present
            foreach (var companyAndBrands in companiesAndBrands)
            {
                deDupedCompanyAndBrandsList.Add(companyAndBrands);

                foreach (var companyAndProducts in companiesAndProducts)
                {
                    if (companyAndBrands.CompanyName
                        == companyAndProducts.CompanyName)
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
                                                            cb.CompanyName
                                                            == companyAndProduct.CompanyName))
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
                                                              CompanyName = freebaseConsumerCompany.Name
                                                      });
                }
            }
            return companyBrandRelationships;
        }
    }
}