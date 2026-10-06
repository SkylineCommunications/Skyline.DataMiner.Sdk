namespace MyAdHocDataSource
{
    using System;

    using Skyline.DataMiner.Analytics.GenericInterface;

    [GQIMetaData(Name = "MyAdHocDataSource")]
    public sealed class Source : IGQIDataSource
    {
        public GQIColumn[] GetColumns()
        {
            return Array.Empty<GQIColumn>();
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            return new GQIPage(Array.Empty<GQIRow>()) { HasNextPage = false };
        }
    }
}
