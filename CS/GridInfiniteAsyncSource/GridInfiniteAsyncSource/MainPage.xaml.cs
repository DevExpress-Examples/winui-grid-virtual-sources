using DevExpress.Data.Filtering;
using DevExpress.WinUI.Grid;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

namespace GridInfiniteAsyncSource {
    public sealed partial class MainPage : Page {
        public MainPage() {
            this.InitializeComponent();

            var source = new InfiniteAsyncSource() {
                ElementType = typeof(IssueData)
            };
            Unloaded += (o, e) => {
                source.Dispose();
            };
            source.FetchRows += (o, e) => {
                e.Result = FetchRowsAsync(e);
            };
            source.GetUniqueValues += (o, e) => {
                if (e.PropertyName == "Priority") {
                    var values = Enum.GetValues(typeof(Priority)).Cast<object>().ToArray();
                    e.Result = Task.FromResult(values);
                }
            };
            source.GetTotalSummaries += (o, e) => {
                e.Result = GetTotalSummariesAsync(e);
            };

            grid.ItemsSource = source;
        }

        static async Task<FetchRowsResult> FetchRowsAsync(FetchRowsAsyncEventArgs e) {
            IssueSortOrder sortOrder = GetIssueSortOrder(e.SortOrder);
            IssueFilter filter = MakeIssueFilter(e.Filter);
            var take = 30; // The number of rows that the GridControl loads in one portion.
            var issues = await IssuesService.GetIssuesAsync(
                skip: e.Skip,
                take: take,
                sortOrder: sortOrder,
                filter: filter);
            return new FetchRowsResult(issues, hasMoreRows: issues.Length == take);
        }

        static IssueSortOrder GetIssueSortOrder(SortDefinition[] sortOrder) {
            if (sortOrder.Length > 0) {
                var sort = sortOrder.Single();
                if (sort.PropertyName == "Created") {
                    if (sort.Direction != ListSortDirection.Descending)
                        throw new InvalidOperationException();
                    return IssueSortOrder.CreatedDescending;
                }
                if (sort.PropertyName == "Votes") {
                    return sort.Direction == ListSortDirection.Ascending
                        ? IssueSortOrder.VotesAscending
                        : IssueSortOrder.VotesDescending;
                }
            }
            return IssueSortOrder.Default;
        }

        static IssueFilter MakeIssueFilter(CriteriaOperator filter) {
            return filter.Match(
                binary: (propertyName, value, type) => {
                    if (propertyName == "Votes" && type == BinaryOperatorType.GreaterOrEqual)
                        return new IssueFilter(minVotes: (int)value);

                    if (propertyName == "Priority" && type == BinaryOperatorType.Equal)
                        return new IssueFilter(priority: (Priority)value);

                    if (propertyName == "Created") {
                        if (type == BinaryOperatorType.GreaterOrEqual)
                            return new IssueFilter(createdFrom: (DateTime)value);
                        if (type == BinaryOperatorType.Less)
                            return new IssueFilter(createdTo: (DateTime)value);
                    }

                    throw new InvalidOperationException();
                },
                and: filters => {
                    return new IssueFilter(
                        createdFrom: filters.Select(x => x.CreatedFrom).SingleOrDefault(x => x != null),
                        createdTo: filters.Select(x => x.CreatedTo).SingleOrDefault(x => x != null),
                        minVotes: filters.Select(x => x.MinVotes).SingleOrDefault(x => x != null),
                        priority: filters.Select(x => x.Priority).SingleOrDefault(x => x != null)
                    );
                },
                @null: default(IssueFilter)
            );
        }

        static async Task<object[]> GetTotalSummariesAsync(GetSummariesAsyncEventArgs e) {
            IssueFilter filter = MakeIssueFilter(e.Filter);
            var summaryValues = await IssuesService.GetSummariesAsync(filter);
            return e.Summaries.Select(x => {
                if (x.SummaryType == SummaryType.Count)
                    return (object)summaryValues.Count;
                if (x.SummaryType == SummaryType.Max && x.PropertyName == "Created")
                    return summaryValues.LastCreated;
                throw new InvalidOperationException();
            }).ToArray();
        }
    }
}
