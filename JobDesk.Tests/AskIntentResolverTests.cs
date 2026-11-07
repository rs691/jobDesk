using JobDesk.Services.Databricks;
using Xunit;

namespace JobDesk.Tests;

public class AskIntentResolverTests
{
    [Theory]
    [InlineData("Work orders by status", AskIntent.WorkOrdersByStatus)]
    [InlineData("overdue open work by crew", AskIntent.OverdueOpenWorkByCrew)]
    [InlineData("what is our outstanding invoice balance", AskIntent.OutstandingInvoiceBalance)]
    [InlineData("blocked jobs explained", AskIntent.BlockedJobsExplained)]
    [InlineData("next seven days crew load", AskIntent.NextSevenDaysCrewLoad)]
    public void Resolves_known_questions(string question, AskIntent expected)
    {
        Assert.Equal(expected, AskIntentResolver.Resolve(question));
    }

    [Fact]
    public void Unknown_question_returns_null()
    {
        Assert.Null(AskIntentResolver.Resolve("drop table customers"));
    }

    [Fact]
    public void HelpText_lists_all_intents()
    {
        var help = AskIntentResolver.HelpText();
        Assert.Contains("Work orders by status", help);
        Assert.Contains("No arbitrary SQL", help);
    }
}
