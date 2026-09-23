using System;

namespace CalculateFunding.Services.Publishing.FundingManagement.SqlModels
{
    public class LatestFundingGroupVersionInProviderChannel
    {
        public Guid FundingGroupId { get; set; }

        public Guid FundingGroupVersionId { get; set; }
        public Guid ReleasedProviderVersionChannelId { get; set; }

        public string PrviderId { get; set; }

        public string FundingStreamCode { get; set; }

        public string FundingPeriodCode { get; set; }

        public string GroupingReasonCode { get; set; }

        public string OrganisationGroupTypeCode { get; set; }

        public string OrganisationGroupIdentifierValue { get; set; }

        public int MajorVersion { get; set; }

        public int ChannelVersion { get; set; }

        public int ChannelId { get; set; }

        public string GroupFundingId { get; set; }

        public string ProviderFundingId { get; set; }

        public decimal GroupFundingTotal { get;set; }

        public DateTime StatusChangedDate { get;set; }

        public DateTime EarliestPaymentAvailableDate { get;set; }

        public DateTime ExternalPublicationDate { get;set; }


    }
}
