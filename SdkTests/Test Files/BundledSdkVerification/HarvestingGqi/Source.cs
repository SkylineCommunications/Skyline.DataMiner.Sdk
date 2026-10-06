namespace HarvestingGqi
{
    using System;

    using Skyline.DataMiner.Analytics.GenericInterface;

    [GQIMetaData(Name = "HarvestingGqi")]
    public sealed class Source : IGQIDataSource
    {
        public GQIColumn[] GetColumns()
        {
            _ = Harvesting.Probe.Entry.GetValue();
            return Array.Empty<GQIColumn>();
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            return new GQIPage(Array.Empty<GQIRow>()) { HasNextPage = false };
        }
    }
}
