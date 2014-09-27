namespace Slurper.Model
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using numl.Model;

    using sun.tools.tree;

    #endregion

    public class Candidate
    {
        [Label]
        public bool CompanyBrandRelationship { get; set; }

        public string CandidateHtmlWithoutCandidateEntities { get; set; }

        public string PreviousContentWithoutCandidateEntities { get; set; }

        public string NearestHeadingAbove { get; set; }

        public string CandidateHtml { get; set; }

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

        public List<string> KnownBrands { get; set; }

        public CompanyAndBrands KnownCompanyAndBrands { get; set; }

        public string KnownCompanyName { get; set; }

        public string PageTitle { get; set; }

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
        public bool Core { get; set; }

        [Feature]
        public bool Major { get; set; }

        [Feature]
        public bool Brands { get; set; }

        [Feature]
        public bool License { get; set; }

        [Feature]
        public bool Contents { get; set; }

        [Feature]
        public int NumberOfLocationNames { get; set; }

        [Feature]
        public int NumberOfPersonNames { get; set; }

        [Feature]
        public int NumberOfOrganisationNames { get; set; }

        public void MapWordsToWordFeatures()
        {
            this.Acquisitions = WordsInPreviousContent.Any(w => w.Equals("acquisitions", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("acquisitions");
            this.Became = WordsInPreviousContent.Any(w => w.Equals("became", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("became"); 
            this.Consulting = WordsInPreviousContent.Any(w => w.Equals("consulting", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("consulting"); 
            this.Environmental = WordsInPreviousContent.Any(w => w.Equals("environmental", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("environmental"); 
            this.External = WordsInPreviousContent.Any(w => w.Equals("external", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("external"); 
            this.Former = WordsInPreviousContent.Any(w => w.Equals("former", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("former"); 
            this.Further = WordsInPreviousContent.Any(w => w.Equals("further", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("further"); 
            this.Income = WordsInPreviousContent.Any(w => w.Equals("income", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("income"); 
            this.Information = WordsInPreviousContent.Any(w => w.Equals("information", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("information"); 
            this.Links = WordsInPreviousContent.Any(w => w.Equals("links", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("links"); 
            this.Merged = WordsInPreviousContent.Any(w => w.Equals("merged", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("merged"); 
            this.Other = WordsInPreviousContent.Any(w => w.Equals("other", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("other"); 
            this.Outside = WordsInPreviousContent.Any(w => w.Equals("outside", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("outside"); 
            this.Own = WordsInPreviousContent.Any(w => w.Equals("own", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("own"); 
            this.Product = WordsInPreviousContent.Any(w => w.Equals("product", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("product"); 
            this.Products = WordsInPreviousContent.Any(w => w.Equals("products", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("products");
            this.Reading = WordsInPreviousContent.Any(w => w.Equals("reading", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("reading"); 
            this.Recent = WordsInPreviousContent.Any(w => w.Equals("recent", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("recent"); 
            this.Revenue = WordsInPreviousContent.Any(w => w.Equals("revenue", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("revenue"); 
            this.Services = WordsInPreviousContent.Any(w => w.Equals("services", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("services"); 
            this.Since = WordsInPreviousContent.Any(w => w.Equals("since", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("since"); 
            this.Sold = WordsInPreviousContent.Any(w => w.Equals("sold", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("sold"); 
            this.Staff = WordsInPreviousContent.Any(w => w.Equals("staff", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("staff"); 
            this.Licence = WordsInPreviousContent.Any(w => w.Equals("licence", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("licence"); 
            this.License = WordsInPreviousContent.Any(w => w.Equals("license", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("license"); 
            this.Creative = WordsInPreviousContent.Any(w => w.Equals("creative", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("creative"); 
            this.Commons = WordsInPreviousContent.Any(w => w.Equals("commons", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("commons"); 
            this.Appointment = WordsInPreviousContent.Any(w => w.Equals("appointment", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("appointment"); 
            this.Core = WordsInPreviousContent.Any(w => w.Equals("core", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("acquisitions"); 
            this.Major = WordsInPreviousContent.Any(w => w.Equals("major", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("core"); 
            this.Brands = WordsInPreviousContent.Any(w => w.Equals("brands", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("brands"); 
            this.Tax = WordsInPreviousContent.Any(w => w.Equals("tax", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("tax");
            this.Contents = WordsInPreviousContent.Any(w => w.Equals("contents", StringComparison.InvariantCultureIgnoreCase)) || NearestHeadingAbove.ToLowerInvariant().Contains("contents"); 
        }
    }
}