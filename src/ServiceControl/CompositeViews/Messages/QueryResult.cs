namespace ServiceControl.CompositeViews.Messages
{
    using System;

    public class QueryResult
    {
        protected QueryResult(object results, QueryStatsInfo queryStatsInfo)
        {
            DynamicResults = results;
            QueryStats = queryStatsInfo;
        }

        public object DynamicResults { get; }

        public QueryStatsInfo QueryStats { get; }
    }

    public class QueryResult<TOut> : QueryResult
        where TOut: class 
    {
        public QueryResult(TOut results, QueryStatsInfo queryStatsInfo) : base(results, queryStatsInfo)
        {
            Results = results;
        }

        public TOut Results { get; }

        public static QueryResult<TOut> Empty = new QueryResult<TOut>(null, QueryStatsInfo.Zero);
    }
}