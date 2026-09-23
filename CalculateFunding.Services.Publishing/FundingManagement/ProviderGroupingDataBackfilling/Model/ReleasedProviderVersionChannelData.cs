using System;

namespace CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling.Model
{
    public class ReleasedProviderVersionChannelData
    {
        public Guid ReleasedProviderVersionChannelId { get; set; }

        public Guid ReleasedProviderVersionId { get; set; }

        public int ChannelId { get; set; }

        public DateTime StatusChangedDate { get; set; }

        public int ChannelVersion { get; set; }

        public string FundingId { get; set; }
    }
}
