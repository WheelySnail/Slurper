namespace Slurper.Model
{
    #region Using Directives

    using System;
    using System.Collections.Generic;

    using numl.Model;

    using sun.tools.tree;

    #endregion

    public class Candidate
    {
        [Label]
        public bool CompanyBrandRelationship { get; set; }

        [Feature]
        public String CandidateHtmlWithoutCandidateEntities { get; set; }

        [Feature]
        public string PreviousContentWithoutCandidateEntities { get; set; }

        [Feature]
        public string NearestHeadingAbove { get; set; }

        public String CandidateHtml { get; set; }

        public string PreviousContent { get; set; }

        [Feature]
        public int CandidateHtmlWordCount { get; set; }

        [Feature]
        public int PreviousContentWordCount { get; set; }

        [Feature]
        public bool ContainsMultipleBrands { get; set; }

        [Feature]
        public bool ItemsContainBrandOnly { get; set; }

        [Feature]
        public bool DomainOrPageTitleContainsOwner { get; set; }

        [Feature]
        public bool PreviousContentContainsPotentialOwner { get; set; }

        [Feature]
        public bool CandidateHtmlContainsPotentialOwner { get; set; }

        [Feature]
        public bool IsListSegment { get; set; }

        [Feature]
        public bool IsTableSegment { get; set; }

        public bool IsItemLevelCandidate { get; set; }

        public string KnownBrand { get; set; }

        public List<String> KnownBrands { get; set; }

        public CompanyAndBrands KnownCompanyAndBrands { get; set; }

        public string KnownCompanyName { get; set; }

        public String PageTitle { get; set; }

        public string Uri { get; set; }

        public List<string> WordsInPreviousContent { get; set; }

        public List<string> WordsInCandidateHtml { get; set; }

        [Feature]
        public bool CaptionsContainOwner { get; set; }

        [Feature]
        public int NumberOfItemsWithLanguages { get; set; }


        [Feature]
        public bool Since { get; set; }

        [Feature]
        public bool Own { get; set; }

        [Feature]
        public bool Products { get; set; }

        [Feature]
        public bool Product { get; set; }

        [Feature]
        public bool Became { get; set; }

        [Feature]
        public bool Merged { get; set; }

        [Feature]
        public bool Sold { get; set; }

        [Feature]
        public bool Other { get; set; }

        [Feature]
        public bool Recent { get; set; }

        [Feature]
        public bool Further { get; set; }

        [Feature]
        public bool Information { get; set; }

        [Feature]
        public bool Tax { get; set; }

        [Feature]
        public bool External { get; set; }

        [Feature]
        public bool Links { get; set; }

        [Feature]
        public bool Revenue { get; set; }

        [Feature]
        public bool Services { get; set; }

        [Feature]
        public bool Income { get; set; }

        [Feature]
        public bool Outside { get; set; }

        [Feature]
        public bool Environmental { get; set; }

        [Feature]
        public bool Reading { get; set; }

        [Feature]
        public bool Former { get; set; }

        [Feature]
        public bool Consulting { get; set; }

        [Feature]
        public bool Staff { get; set; }

        [Feature]
        public bool Acquisitions { get; set; }

        [Feature]
        public bool Licence { get; set; }

        [Feature]
        public bool Creative { get; set; }

        [Feature]
        public bool Commons { get; set; }

        [Feature]
        public bool Appointment { get; set; }

        [Feature]
        public bool By { get; set; }

        [Feature]
        public bool Core { get; set; }

        [Feature]
        public bool Major { get; set; }

        [Feature]
        public bool Brands { get; set; }

        [Feature]
        public bool License { get; set; }

        [Feature]
        public int NumberOfLocationNames { get; set; }

        [Feature]
        public int NumberOfPersonNames { get; set; }

        [Feature]
        public int NumberOfOrganisationNames { get; set; }

        public void MapWordsToWordFeatures()
        {
            this.Acquisitions = WordsInPreviousContent.Contains("acquisitions");
            this.Became = WordsInPreviousContent.Contains("became");
            this.Consulting = WordsInPreviousContent.Contains("consulting");
            this.Environmental = WordsInPreviousContent.Contains("environmental");
            this.External = WordsInPreviousContent.Contains("external");
            this.Former = WordsInPreviousContent.Contains("former");
            this.Further = WordsInPreviousContent.Contains("further");
            this.Income = WordsInPreviousContent.Contains("income");
            this.Information = WordsInPreviousContent.Contains("information");
            this.Links = WordsInPreviousContent.Contains("links");
            this.Merged= WordsInPreviousContent.Contains("merged");
            this.Other = WordsInPreviousContent.Contains("other");
            this.Outside = WordsInPreviousContent.Contains("outside");
            this.Own = WordsInPreviousContent.Contains("own");
            this.Product = WordsInPreviousContent.Contains("product");
            this.Products = WordsInPreviousContent.Contains("products");
            this.Reading = WordsInPreviousContent.Contains("reading");
            this.Recent = WordsInPreviousContent.Contains("recent");
            this.Revenue = WordsInPreviousContent.Contains("revenue");
            this.Services = WordsInPreviousContent.Contains("services");
            this.Since = WordsInPreviousContent.Contains("since");
            this.Sold = WordsInPreviousContent.Contains("sold");
            this.Staff = WordsInPreviousContent.Contains("staff");
            this.Licence = WordsInPreviousContent.Contains("licence");
            this.License = WordsInPreviousContent.Contains("license");
            this.Creative = WordsInPreviousContent.Contains("creative");
            this.Commons = WordsInPreviousContent.Contains("commons");
            this.Appointment = WordsInPreviousContent.Contains("appointment");
            this.By = WordsInPreviousContent.Contains("by");
            this.Core = WordsInPreviousContent.Contains("core");
            this.Major = WordsInPreviousContent.Contains("major");
            this.Brands = WordsInPreviousContent.Contains("brands");
        }
    }
}